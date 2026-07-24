namespace Civic.API.Models.DTOs;

/// <summary>
/// Read-only aggregate for the admin engagement dashboard. COUNTS ONLY — no PII, no
/// user ids or emails ever leave the server. "Users" = distinct non-anonymous, non-agent
/// user ids that performed the action at least once.
/// </summary>
public class EngagementDto
{
    public DateTime GeneratedAt { get; set; }
    /// <summary>Recency windows (days) used for ActiveShort / ActiveLong.</summary>
    public int ShortWindowDays { get; set; }
    public int LongWindowDays { get; set; }

    public EngagementSummaryDto Summary { get; set; } = new();

    /// <summary>Per-feature funnel rows, ordered by area then descending users.</summary>
    public List<FeatureStatDto> Features { get; set; } = new();

    /// <summary>Rollup of Features up to the area level (Onboarding, Coalitions, …).</summary>
    public List<AreaStatDto> Areas { get; set; } = new();

    /// <summary>Engagement broken down by self-reported locality (state) from UserProfiles.</summary>
    public List<StateStatDto> ByState { get; set; } = new();

    /// <summary>Histogram: how many distinct areas each known user has touched.</summary>
    public List<BreadthBucketDto> Breadth { get; set; } = new();

    /// <summary>Features that exist in the product but emit NO engagement row today — called out so the dashboard doesn't read them as "zero usage".</summary>
    public List<UntrackedDto> Untracked { get; set; } = new();
}

public class EngagementSummaryDto
{
    /// <summary>UserProfiles rows — the Civic onboarding denominator (a profile is the first thing a real user creates).</summary>
    public int Profiles { get; set; }
    /// <summary>Distinct non-anonymous users seen across ALL engagement tables (breadth of the active base).</summary>
    public int EngagedUsers { get; set; }
    /// <summary>Distinct non-anonymous users active within the short window across any feature.</summary>
    public int ActiveUsersShort { get; set; }
    public int ActiveUsersLong { get; set; }
    /// <summary>Anonymous-attributed engagement rows (UserId = "anonymous") excluded from the counts above but surfaced for context.</summary>
    public int AnonymousEvents { get; set; }
}

public class FeatureStatDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Area { get; set; } = "";
    /// <summary>Distinct users who did this ≥ once.</summary>
    public int Users { get; set; }
    /// <summary>Total events (rows).</summary>
    public int Events { get; set; }
    /// <summary>Distinct users whose most-recent event is within the short window.</summary>
    public int ActiveShort { get; set; }
    public int ActiveLong { get; set; }
    public DateTime? LastAt { get; set; }
}

public class AreaStatDto
{
    public string Area { get; set; } = "";
    public int Users { get; set; }
    public int ActiveLong { get; set; }
}

public class StateStatDto
{
    /// <summary>2-letter state, or "national" when a user has no locality set.</summary>
    public string State { get; set; } = "";
    public int Profiles { get; set; }
    public int EngagedUsers { get; set; }
    /// <summary>Distinct engaged users in this state per area (area key -> user count).</summary>
    public Dictionary<string, int> ByArea { get; set; } = new();
}

public class BreadthBucketDto
{
    /// <summary>Number of distinct areas a user has engaged with (0 = profile only / dormant).</summary>
    public int AreasTouched { get; set; }
    public int Users { get; set; }
}

public class UntrackedDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Note { get; set; } = "";
}
