namespace Civic.API.Models.DTOs;

/// <summary>The caller's weekly cohort plus a leaderboard of how it's doing this week.</summary>
public class CohortDto
{
    public Guid CohortId { get; set; }
    public string WeekKey { get; set; } = "";
    public DateTime WeekStart { get; set; }
    public int MemberCount { get; set; }
    public int TargetSize { get; set; }
    /// <summary>Name of the league this cohort grew from, if any.</summary>
    public string? LeagueName { get; set; }
    /// <summary>How many of the members came from the caller's league (friends).</summary>
    public int FriendsCount { get; set; }
    public int YourRank { get; set; }
    public int YourWeeklyPoints { get; set; }
    public List<CohortStandingDto> Leaderboard { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class CohortStandingDto
{
    public int Rank { get; set; }
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAgent { get; set; }
    public bool IsMe { get; set; }
    public bool IsFriend { get; set; }
    /// <summary>Coalition points earned this week (reasoning + scarce).</summary>
    public int WeeklyPoints { get; set; }
    /// <summary>Distinct active days this week.</summary>
    public int ActiveDays { get; set; }
}
