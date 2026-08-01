using Microsoft.EntityFrameworkCore;
using Arena.Shared.Reporting;
using Civic.API.Data;

namespace Civic.API.Services;

/// <summary>
/// Builds the one-UTC-day slice of Civic engagement that the Arena daily report emails.
/// Reads the same <see cref="EngagementCatalog"/> the admin dashboard uses, so the report
/// and the dashboard can never disagree about what counts as engagement.
///
/// COUNTS ONLY — no user ids or other PII leave the server.
/// </summary>
public class DailyStatsBuilder
{
    private readonly CivicDbContext _db;

    public DailyStatsBuilder(CivicDbContext db) => _db = db;

    public async Task<DailyStatsDto> BuildAsync(DateOnly date, CancellationToken ct = default)
    {
        // Npgsql demands UTC-kind DateTimes for `timestamp with time zone` comparisons, so
        // every boundary is stamped explicitly rather than inheriting Unspecified.
        var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var yesterdayStart = dayStart.AddDays(-1);
        var baselineStart = dayStart.AddDays(-7);

        var activities = new List<DailyMetricDto>();
        var activeToday = new HashSet<string>();
        var activeYesterday = new HashSet<string>();
        var anonEventsToday = 0;

        // Sequential by necessity: every query closes over the same DbContext.
        foreach (var f in EngagementCatalog.For(_db))
        {
            // One pass over the 8-day window (baseline + reported day) covers today,
            // yesterday and the 7-day mean; the cumulative total needs its own count
            // because it reaches back over all history.
            var rows = await f.Build()
                .Where(e => e.UserId != "" && e.At >= baselineStart && e.At < dayEnd)
                .Select(e => new { e.UserId, e.At })
                .ToListAsync(ct);

            var total = await f.Build()
                .Where(e => e.UserId != "" && e.At < dayEnd)
                .CountAsync(ct);

            var today = rows.Where(r => r.At >= dayStart).ToList();
            var yesterday = rows.Count(r => r.At >= yesterdayStart && r.At < dayStart);
            var baseline = rows.Count(r => r.At < dayStart);

            anonEventsToday += today.Count(r => r.UserId == EngagementCatalog.AnonymousUserId);

            var knownToday = today
                .Where(r => r.UserId != EngagementCatalog.AnonymousUserId)
                .Select(r => r.UserId)
                .ToHashSet();
            foreach (var u in knownToday) activeToday.Add(u);

            foreach (var r in rows.Where(r =>
                         r.At >= yesterdayStart && r.At < dayStart &&
                         r.UserId != EngagementCatalog.AnonymousUserId))
            {
                activeYesterday.Add(r.UserId);
            }

            activities.Add(new DailyMetricDto
            {
                Key = f.Key,
                Label = f.Label,
                Area = f.Area,
                Today = today.Count,
                UsersToday = knownToday.Count,
                Yesterday = yesterday,
                Avg7 = Math.Round(baseline / 7.0, 2),
                Total = total,
            });
        }

        activities = activities
            .OrderBy(a => Array.IndexOf(EngagementCatalog.AreaOrder, a.Area))
            .ThenByDescending(a => a.Today)
            .ThenBy(a => a.Label)
            .ToList();

        // Civic has no account table of its own — accounts live in the Arena DB — so a first
        // profile is the closest equivalent to a signup on this side.
        var knownProfiles = _db.UserProfiles
            .Where(p => p.UserId != "" && p.UserId != EngagementCatalog.AnonymousUserId);

        var signups = await knownProfiles.CountAsync(p => p.CreatedAt >= dayStart && p.CreatedAt < dayEnd, ct);
        var signupsLast7 = await knownProfiles.CountAsync(p => p.CreatedAt >= dayEnd.AddDays(-7) && p.CreatedAt < dayEnd, ct);
        var totalKnown = await knownProfiles.Where(p => p.CreatedAt < dayEnd).Select(p => p.UserId).Distinct().CountAsync(ct);

        return new DailyStatsDto
        {
            App = "civic",
            Date = date,
            GeneratedAt = DateTime.UtcNow,
            Audience = new DailyAudienceDto
            {
                Signups = signups,
                SignupsVerified = 0,      // email verification is an Arena-side concept
                AnonymousArrivals = 0,    // Civic has no per-visitor row, only the "anonymous" sentinel
                ActiveUsers = activeToday.Count,
                ActiveUsersYesterday = activeYesterday.Count,
                AnonymousEvents = anonEventsToday,
                TotalKnownUsers = totalKnown,
                SignupsLast7 = signupsLast7,
            },
            Activities = activities,
        };
    }
}
