using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A weekly working group of up to 50 people who collaborate on bills together. A cohort
/// grows out of a user's league (their friends group) and is then topped up with other
/// people so the week's coalition work happens among a fixed group rather than the whole world.
/// The matching is intentionally simple for now (league + random fill) and will improve later.
/// </summary>
public class Cohort
{
    public Guid Id { get; set; }

    /// <summary>Monday (UTC) of the cohort's week, formatted yyyy-MM-dd. One cohort per week.</summary>
    [Required, MaxLength(20)]
    public string WeekKey { get; set; } = "";

    /// <summary>Start of the week (Monday 00:00 UTC); the leaderboard windows on this.</summary>
    public DateTime WeekStart { get; set; }

    /// <summary>The friends-league this cohort grew from, if any. Null = solo-seeded cohort.</summary>
    public Guid? AnchorLeagueId { get; set; }

    /// <summary>Target headcount (50). Cohorts may be smaller when there aren't enough people yet.</summary>
    public int TargetSize { get; set; } = 50;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CohortMember> Members { get; set; } = new();
}

public class CohortMember
{
    public Guid Id { get; set; }

    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    /// <summary>Denormalized from the cohort so we can enforce one cohort per user per week.</summary>
    [Required, MaxLength(20)]
    public string WeekKey { get; set; } = "";

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>Snapshot display name (from the league membership if available, else derived).</summary>
    [MaxLength(160)]
    public string DisplayName { get; set; } = "";

    public bool IsAgent { get; set; }

    /// <summary>How this member got into the cohort: "self" | "league" | "random".</summary>
    [MaxLength(20)]
    public string Source { get; set; } = "random";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
