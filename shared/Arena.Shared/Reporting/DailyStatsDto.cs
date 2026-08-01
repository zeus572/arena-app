namespace Arena.Shared.Reporting;

/// <summary>
/// One app's slice of a single UTC day, plus the comparison numbers needed to say
/// whether that day was normal. This is the wire contract between the two backends:
/// Civic serves it from <c>GET /api/admin/daily-stats</c> and the Arena daily report
/// composes its own slice with Civic's into one email, so both sides must agree on
/// the shape. It lives in Arena.Shared for exactly that reason.
///
/// COUNTS ONLY — no user ids, emails, or other PII crosses this boundary, matching
/// the rule the Civic admin engagement endpoint already documents.
/// </summary>
public class DailyStatsDto
{
    /// <summary>"arena" or "civic".</summary>
    public string App { get; set; } = "";

    /// <summary>The UTC day this slice covers (00:00:00Z inclusive to the next 00:00:00Z exclusive).</summary>
    public DateOnly Date { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DailyAudienceDto Audience { get; set; } = new();

    /// <summary>Per-activity counts for the day, ordered by area then descending volume.</summary>
    public List<DailyMetricDto> Activities { get; set; } = new();
}

/// <summary>Who showed up — the people numbers that sit above the activity table.</summary>
public class DailyAudienceDto
{
    /// <summary>New real accounts on the day. Arena: non-anonymous Users created.
    /// Civic: UserProfiles created (Civic has no user table of its own — accounts are
    /// Arena-side — so a first profile is its closest equivalent to a signup).</summary>
    public int Signups { get; set; }

    /// <summary>Subset of <see cref="Signups"/> that had verified their email by the
    /// time the report ran. Arena only; 0 for Civic.</summary>
    public int SignupsVerified { get; set; }

    /// <summary>New anonymous user rows created on the day — the closest thing to a
    /// "someone browsed without signing up" counter. Arena only; 0 for Civic.</summary>
    public int AnonymousArrivals { get; set; }

    /// <summary>Distinct known (non-anonymous) users who did at least one tracked
    /// thing on the day.</summary>
    public int ActiveUsers { get; set; }

    /// <summary>Same measure for the previous day, so the email can show a delta.</summary>
    public int ActiveUsersYesterday { get; set; }

    /// <summary>Events on the day attributable to anonymous users — volume that is real
    /// engagement but can't be tied to an account.</summary>
    public int AnonymousEvents { get; set; }

    /// <summary>Cumulative known users as of the end of the day.</summary>
    public int TotalKnownUsers { get; set; }

    /// <summary>Signups over the 7 days ending with this one (inclusive).</summary>
    public int SignupsLast7 { get; set; }
}

/// <summary>One tracked activity: what happened today, and what "normal" looks like.</summary>
public class DailyMetricDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Area { get; set; } = "";

    /// <summary>Events on the reported day.</summary>
    public int Today { get; set; }

    /// <summary>Distinct known (non-anonymous) users behind <see cref="Today"/>.</summary>
    public int UsersToday { get; set; }

    /// <summary>Events on the day before the reported day.</summary>
    public int Yesterday { get; set; }

    /// <summary>Mean events/day over the 7 days BEFORE the reported day — the baseline
    /// "today" is judged against. Excludes the reported day so a spike can't flatten
    /// its own baseline.</summary>
    public double Avg7 { get; set; }

    /// <summary>Cumulative events as of the end of the reported day.</summary>
    public int Total { get; set; }
}
