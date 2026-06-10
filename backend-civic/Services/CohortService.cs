using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Services;

public interface ICohortService
{
    Task<CohortDto> GetOrCreateForUserAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Builds and serves a user's weekly cohort: up to 50 people who work the week's bills together.
/// Seeded from the user's league (friends), then topped up with other people / agents. The
/// matching is deliberately simple (league + random fill) — a placeholder we can improve later.
/// </summary>
public class CohortService : ICohortService
{
    public const int TargetSize = 50;

    private readonly CivicDbContext _db;

    public CohortService(CivicDbContext db) => _db = db;

    /// <summary>Monday (UTC) of the given instant's week: a stable key + the window start.</summary>
    public static (string Key, DateTime Start) WeekOf(DateTime utcNow)
    {
        var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7; // Mon=0 … Sun=6
        var monday = DateTime.SpecifyKind(utcNow.Date.AddDays(-daysSinceMonday), DateTimeKind.Utc);
        return (monday.ToString("yyyy-MM-dd"), monday);
    }

    public async Task<CohortDto> GetOrCreateForUserAsync(string userId, CancellationToken ct = default)
    {
        var (weekKey, weekStart) = WeekOf(DateTime.UtcNow);

        var existing = await _db.CohortMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.WeekKey == weekKey, ct);

        Cohort cohort = existing is not null
            ? await LoadCohortAsync(existing.CohortId, ct)
            : await AssignAsync(userId, weekKey, weekStart, ct);

        return await BuildDtoAsync(cohort, userId, weekStart, ct);
    }

    private async Task<Cohort> LoadCohortAsync(Guid cohortId, CancellationToken ct) =>
        await _db.Cohorts.Include(c => c.Members).FirstAsync(c => c.Id == cohortId, ct);

    private async Task<Cohort> AssignAsync(string userId, string weekKey, DateTime weekStart, CancellationToken ct)
    {
        // The user's "primary" league = one they own, else the earliest they joined.
        var myMemberships = await _db.LeagueMembers
            .Where(m => m.UserId == userId)
            .Select(m => new { m.LeagueId, m.Role, m.JoinedAt })
            .ToListAsync(ct);
        Guid? primaryLeagueId = myMemberships
            .OrderByDescending(m => m.Role == Models.LeagueMemberRole.Owner)
            .ThenBy(m => m.JoinedAt)
            .Select(m => (Guid?)m.LeagueId)
            .FirstOrDefault();

        // If friends already spun up this league's cohort for the week, join it.
        if (primaryLeagueId is not null)
        {
            var leagueCohort = await _db.Cohorts
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.WeekKey == weekKey && c.AnchorLeagueId == primaryLeagueId, ct);
            if (leagueCohort is not null)
            {
                if (leagueCohort.Members.All(m => m.UserId != userId) && leagueCohort.Members.Count < TargetSize)
                {
                    leagueCohort.Members.Add(NewMember(leagueCohort.Id, weekKey, userId,
                        await DisplayNameAsync(userId, ct), IsAgentId(userId), "self"));
                    await SaveIgnoringDuplicateAsync(ct);
                }
                return await LoadCohortAsync(leagueCohort.Id, ct);
            }
        }

        // Otherwise build a fresh cohort: league friends first, then random fill.
        var cohort = new Cohort
        {
            Id = Guid.NewGuid(),
            WeekKey = weekKey,
            WeekStart = weekStart,
            AnchorLeagueId = primaryLeagueId,
            TargetSize = TargetSize,
        };
        _db.Cohorts.Add(cohort);

        // Users already placed in some cohort this week are off-limits (one cohort per week).
        var assigned = (await _db.CohortMembers.Where(m => m.WeekKey == weekKey).Select(m => m.UserId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var picked = new HashSet<string>(StringComparer.Ordinal);
        void Add(string uid, string source)
        {
            if (string.IsNullOrWhiteSpace(uid) || assigned.Contains(uid) || !picked.Add(uid)) return;
            cohort.Members.Add(NewMember(cohort.Id, weekKey, uid, displayNames.GetValueOrDefault(uid) ?? Pretty(uid), IsAgentId(uid), source));
        }

        // 1) Seed with the caller's league friends.
        if (primaryLeagueId is not null)
        {
            var friends = await _db.LeagueMembers
                .Where(m => m.LeagueId == primaryLeagueId)
                .Select(m => new { m.UserId, m.DisplayName })
                .ToListAsync(ct);
            foreach (var f in friends) displayNames[f.UserId] = string.IsNullOrWhiteSpace(f.DisplayName) ? Pretty(f.UserId) : f.DisplayName;
            Add(userId, "self");
            foreach (var f in friends.Where(f => f.UserId != userId)) Add(f.UserId, "league");
        }
        else
        {
            displayNames[userId] = await DisplayNameAsync(userId, ct);
            Add(userId, "self");
        }

        // 2) Random fill: other real people first, then agents, up to the target.
        if (cohort.Members.Count < TargetSize)
        {
            var realPool = await RealUserPoolAsync(ct);
            foreach (var uid in Shuffle(realPool))
            {
                if (cohort.Members.Count >= TargetSize) break;
                Add(uid, "random");
            }
        }
        if (cohort.Members.Count < TargetSize)
        {
            var agentPool = await AgentPoolAsync(ct);
            foreach (var uid in Shuffle(agentPool))
            {
                if (cohort.Members.Count >= TargetSize) break;
                Add(uid, "random");
            }
        }

        await SaveIgnoringDuplicateAsync(ct);
        return await LoadCohortAsync(cohort.Id, ct);
    }

    private async Task<List<string>> RealUserPoolAsync(CancellationToken ct)
    {
        var fromLeagues = await _db.LeagueMembers.Select(m => m.UserId).Distinct().ToListAsync(ct);
        var fromProfiles = await _db.UserProfiles.Select(p => p.UserId).Distinct().ToListAsync(ct);
        var fromParticipants = await _db.CoalitionParticipants
            .Where(p => !p.IsAgent).Select(p => p.UserId).Distinct().ToListAsync(ct);
        return fromLeagues.Concat(fromProfiles).Concat(fromParticipants)
            .Where(u => !string.IsNullOrWhiteSpace(u) && !IsAgentId(u) && u != "anonymous")
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<string>> AgentPoolAsync(CancellationToken ct) =>
        (await _db.CoalitionParticipants.Where(p => p.IsAgent).Select(p => p.UserId).Distinct().ToListAsync(ct))
        .Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal).ToList();

    private async Task<CohortDto> BuildDtoAsync(Cohort cohort, string userId, DateTime weekStart, CancellationToken ct)
    {
        var memberIds = cohort.Members.Select(m => m.UserId).ToList();

        // Weekly coalition points per member (reasoning + scarce), within this week's window.
        var actRows = await _db.CoalitionActs
            .Where(a => a.CreatedAt >= weekStart && memberIds.Contains(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(a => a.Points) })
            .ToListAsync(ct);
        var pointsByUser = actRows.ToDictionary(r => r.UserId, r => r.Points, StringComparer.Ordinal);

        var weekStartDay = DateOnly.FromDateTime(weekStart);
        var dayRows = await _db.CoalitionActivityDays
            .Where(d => d.Day >= weekStartDay && memberIds.Contains(d.UserId))
            .GroupBy(d => d.UserId)
            .Select(g => new { UserId = g.Key, Days = g.Count() })
            .ToListAsync(ct);
        var daysByUser = dayRows.ToDictionary(r => r.UserId, r => r.Days, StringComparer.Ordinal);

        var ranked = cohort.Members
            .Select(m => new CohortStandingDto
            {
                UserId = m.UserId,
                DisplayName = string.IsNullOrWhiteSpace(m.DisplayName) ? Pretty(m.UserId) : m.DisplayName,
                IsAgent = m.IsAgent,
                IsMe = m.UserId == userId,
                IsFriend = m.Source == "league" || m.Source == "self",
                WeeklyPoints = pointsByUser.GetValueOrDefault(m.UserId),
                ActiveDays = daysByUser.GetValueOrDefault(m.UserId),
            })
            .OrderByDescending(s => s.WeeklyPoints)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var i = 0; i < ranked.Count; i++) ranked[i].Rank = i + 1;

        var me = ranked.FirstOrDefault(s => s.IsMe);
        string? leagueName = cohort.AnchorLeagueId is null
            ? null
            : await _db.Leagues.Where(l => l.Id == cohort.AnchorLeagueId).Select(l => l.Name).FirstOrDefaultAsync(ct);

        return new CohortDto
        {
            CohortId = cohort.Id,
            WeekKey = cohort.WeekKey,
            WeekStart = cohort.WeekStart,
            MemberCount = cohort.Members.Count,
            TargetSize = cohort.TargetSize,
            LeagueName = leagueName,
            FriendsCount = cohort.Members.Count(m => m.Source == "league" || m.Source == "self"),
            YourRank = me?.Rank ?? 0,
            YourWeeklyPoints = me?.WeeklyPoints ?? 0,
            Leaderboard = ranked,
            GeneratedAt = DateTime.UtcNow,
        };
    }

    private async Task<string> DisplayNameAsync(string userId, CancellationToken ct)
    {
        var name = await _db.LeagueMembers
            .Where(m => m.UserId == userId && m.DisplayName != "")
            .OrderByDescending(m => m.IdentityRefreshedAt)
            .Select(m => m.DisplayName)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(name) ? Pretty(userId) : name;
    }

    private static CohortMember NewMember(Guid cohortId, string weekKey, string userId, string displayName, bool isAgent, string source) => new()
    {
        Id = Guid.NewGuid(),
        CohortId = cohortId,
        WeekKey = weekKey,
        UserId = userId,
        DisplayName = displayName,
        IsAgent = isAgent,
        Source = source,
    };

    private async Task SaveIgnoringDuplicateAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // A friend raced us to create the league cohort, or a member got placed elsewhere
            // this week. The unique indexes protect us; drop our pending inserts and move on.
            foreach (var e in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                e.State = EntityState.Detached;
        }
    }

    private static bool IsAgentId(string userId) => userId.StartsWith("agent:", StringComparison.OrdinalIgnoreCase);

    private static string Pretty(string userId)
    {
        if (IsAgentId(userId))
        {
            var tail = userId["agent:".Length..];
            return tail.Length == 0 ? "Agent" : char.ToUpperInvariant(tail[0]) + tail[1..] + " (agent)";
        }
        var shortId = userId.Length <= 6 ? userId : userId[..6];
        return $"Member {shortId}";
    }

    private static IEnumerable<string> Shuffle(List<string> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }
}
