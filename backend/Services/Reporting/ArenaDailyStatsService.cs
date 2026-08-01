using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.Shared.Reporting;

namespace Arena.API.Services.Reporting;

/// <summary>
/// Builds the one-UTC-day slice of Arena engagement for the daily operator report — the
/// Arena-side counterpart to Civic's <c>DailyStatsBuilder</c>, producing the same
/// <see cref="DailyStatsDto"/> shape so the email can render both apps identically.
///
/// Two things this deliberately separates, because conflating them makes a quiet day look
/// busy: what PEOPLE did (votes, reactions, predictions…) and what the PLATFORM did on its
/// own (bot-created debates, generated turns). Anonymous users are counted as volume but
/// never as "active users" — an auto-created anonymous row is a browser, not a person we know.
///
/// COUNTS ONLY — no user ids or other PII leave this service.
/// </summary>
public class ArenaDailyStatsService
{
    private readonly ArenaDbContext _db;

    public ArenaDailyStatsService(ArenaDbContext db) => _db = db;

    public const string People = "People";
    public const string Platform = "Platform";

    private static readonly string[] AreaOrder = { People, Platform };

    /// <summary>EF-projectable (user, timestamp) row. UserId is null for platform-generated
    /// activity that has no human behind it.</summary>
    private sealed class EventRow
    {
        public Guid? UserId { get; set; }
        public DateTime At { get; set; }
    }

    private sealed record MetricDef(string Key, string Label, string Area, Func<IQueryable<EventRow>> Build);

    private IReadOnlyList<MetricDef> Metrics() => new MetricDef[]
    {
        new("vote", "Debate votes cast", People,
            () => _db.Votes.Select(x => new EventRow { UserId = (Guid?)x.UserId, At = x.CreatedAt })),
        new("reaction", "Reactions on debates & turns", People,
            () => _db.Reactions.Select(x => new EventRow { UserId = (Guid?)x.UserId, At = x.CreatedAt })),
        new("prediction", "Winner predictions", People,
            () => _db.Predictions.Select(x => new EventRow { UserId = (Guid?)x.UserId, At = x.CreatedAt })),
        new("intervention", "Crowd questions submitted", People,
            () => _db.Interventions.Select(x => new EventRow { UserId = (Guid?)x.UserId, At = x.CreatedAt })),
        new("topic_proposal", "Topics proposed", People,
            () => _db.TopicProposals.Select(x => new EventRow { UserId = (Guid?)x.ProposedByUserId, At = x.CreatedAt })),
        new("topic_vote", "Votes on proposed topics", People,
            () => _db.TopicVotes.Select(x => new EventRow { UserId = (Guid?)x.UserId, At = x.CreatedAt })),
        new("debate_started", "Debates started by a person", People,
            () => _db.Debates.Where(x => x.StartedByUserId != null)
                .Select(x => new EventRow { UserId = x.StartedByUserId, At = x.CreatedAt })),

        new("debate_created", "Debates created (incl. bot)", Platform,
            () => _db.Debates.Select(x => new EventRow { UserId = null, At = x.CreatedAt })),
        new("turn_generated", "Debate turns generated", Platform,
            () => _db.Turns.Select(x => new EventRow { UserId = null, At = x.CreatedAt })),
    };

    public async Task<DailyStatsDto> BuildAsync(DateOnly date, CancellationToken ct = default)
    {
        // Npgsql demands UTC-kind DateTimes for `timestamp with time zone` comparisons, so
        // every boundary is stamped explicitly rather than inheriting Unspecified.
        var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var yesterdayStart = dayStart.AddDays(-1);
        var baselineStart = dayStart.AddDays(-7);

        // Load first, classify second: one pass over the 8-day window per metric, executed
        // sequentially because a DbContext is not thread-safe.
        var loaded = new List<(MetricDef Def, List<EventRow> Rows, int Total)>();
        foreach (var m in Metrics())
        {
            var rows = await m.Build()
                .Where(e => e.At >= baselineStart && e.At < dayEnd)
                .ToListAsync(ct);

            var total = await m.Build().Where(e => e.At < dayEnd).CountAsync(ct);
            loaded.Add((m, rows, total));
        }

        // Which of the users seen in the window are anonymous? Resolved in one query over
        // just those ids — the day's distinct actors, not the whole user table.
        var seenIds = loaded
            .SelectMany(l => l.Rows)
            .Where(r => r.UserId.HasValue)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToList();

        var anonymousIds = seenIds.Count == 0
            ? new HashSet<Guid>()
            : (await _db.Users
                .Where(u => seenIds.Contains(u.Id) && u.IsAnonymous)
                .Select(u => u.Id)
                .ToListAsync(ct)).ToHashSet();

        bool IsKnown(Guid? id) => id.HasValue && !anonymousIds.Contains(id.Value);

        var activities = new List<DailyMetricDto>();
        var activeToday = new HashSet<Guid>();
        var activeYesterday = new HashSet<Guid>();
        var anonEventsToday = 0;

        foreach (var (def, rows, total) in loaded)
        {
            var today = rows.Where(r => r.At >= dayStart).ToList();
            var yesterday = rows.Where(r => r.At >= yesterdayStart && r.At < dayStart).ToList();

            anonEventsToday += today.Count(r => r.UserId.HasValue && anonymousIds.Contains(r.UserId!.Value));

            var knownToday = today.Where(r => IsKnown(r.UserId)).Select(r => r.UserId!.Value).ToHashSet();
            foreach (var u in knownToday) activeToday.Add(u);
            foreach (var r in yesterday.Where(r => IsKnown(r.UserId))) activeYesterday.Add(r.UserId!.Value);

            activities.Add(new DailyMetricDto
            {
                Key = def.Key,
                Label = def.Label,
                Area = def.Area,
                Today = today.Count,
                UsersToday = knownToday.Count,
                Yesterday = yesterday.Count,
                Avg7 = Math.Round(rows.Count(r => r.At < dayStart) / 7.0, 2),
                Total = total,
            });
        }

        activities = activities
            .OrderBy(a => Array.IndexOf(AreaOrder, a.Area))
            .ThenByDescending(a => a.Today)
            .ThenBy(a => a.Label)
            .ToList();

        var known = _db.Users.Where(u => !u.IsAnonymous);

        return new DailyStatsDto
        {
            App = "arena",
            Date = date,
            GeneratedAt = DateTime.UtcNow,
            Audience = new DailyAudienceDto
            {
                Signups = await known.CountAsync(u => u.CreatedAt >= dayStart && u.CreatedAt < dayEnd, ct),
                SignupsVerified = await known.CountAsync(
                    u => u.CreatedAt >= dayStart && u.CreatedAt < dayEnd && u.EmailVerified, ct),
                AnonymousArrivals = await _db.Users.CountAsync(
                    u => u.IsAnonymous && u.CreatedAt >= dayStart && u.CreatedAt < dayEnd, ct),
                ActiveUsers = activeToday.Count,
                ActiveUsersYesterday = activeYesterday.Count,
                AnonymousEvents = anonEventsToday,
                TotalKnownUsers = await known.CountAsync(u => u.CreatedAt < dayEnd, ct),
                SignupsLast7 = await known.CountAsync(
                    u => u.CreatedAt >= dayEnd.AddDays(-7) && u.CreatedAt < dayEnd, ct),
            },
            Activities = activities,
        };
    }
}
