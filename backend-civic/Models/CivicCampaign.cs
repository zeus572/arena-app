using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum CivicCampaignStatus
{
    Active,
    Completed,
    Abandoned,
}

public enum CivicCampaignDifficulty
{
    Easy,
    Normal,
    Hard,
}

/// <summary>
/// The kinds of weekly action a campaign manager can take. Each maps onto a real
/// Civic Arena mechanic so the game teaches genuine campaign craft.
/// </summary>
public enum CivicCampaignActionType
{
    /// <summary>Publish a campaign post (pick a plank/issue + tone). Generates a real CampaignPost.</summary>
    PublishPost,

    /// <summary>Respond to a hot news briefing this week (offense / disciplined / pivot).</summary>
    RapidResponse,

    /// <summary>Invest a turn shoring up a weak value-axis to blunt opponent gains there.</summary>
    ShoreUpAxis,

    /// <summary>Concentrate the week on a high-salience issue the candidate is strong on.</summary>
    TargetIssue,
}

/// <summary>
/// A single player's run as campaign manager for one EXISTING <see cref="VirtualCandidate"/>,
/// trying to get that candidate to finish first in their race by election day. All support
/// simulation is local to the campaign — it never mutates the global candidate catalog.
/// </summary>
public class CivicCampaign
{
    public Guid Id { get; set; }

    /// <summary>Civic identifies users by a string id (JWT sub / X-User-Id / "anonymous").</summary>
    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>The candidate the player is managing.</summary>
    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    public Guid ElectionCycleId { get; set; }
    public ElectionCycle? ElectionCycle { get; set; }

    /// <summary>Stable key for the race = office[/state[/district]]. Opponents share this key.</summary>
    [Required, MaxLength(60)]
    public string RaceKey { get; set; } = "";

    [MaxLength(160)]
    public string RaceLabel { get; set; } = "";

    public CivicCampaignDifficulty Difficulty { get; set; } = CivicCampaignDifficulty.Normal;

    public int TotalWeeks { get; set; }
    public int CurrentWeek { get; set; } = 1;

    public CivicCampaignStatus Status { get; set; } = CivicCampaignStatus.Active;

    /// <summary>Set on the final week: did the managed candidate finish first in the race?</summary>
    public bool? Won { get; set; }

    /// <summary>Final support share (0..100) of the managed candidate.</summary>
    public double? FinalSupport { get; set; }

    [MaxLength(400)]
    public string? Outcome { get; set; }

    /// <summary>Remaining action points for the current week (refilled on advance).</summary>
    public int ActionsRemaining { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public List<CivicCampaignStanding> Standings { get; set; } = new();
    public List<CivicCampaignWeek> Weeks { get; set; } = new();
    public List<CivicCampaignAction> Actions { get; set; } = new();
}

/// <summary>
/// One candidate's current simulated support share within a campaign's race. There is one row
/// per candidate in the race (the managed candidate plus every real opponent). Shares across a
/// campaign sum to ~100.
/// </summary>
public class CivicCampaignStanding
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }
    public CivicCampaign? Campaign { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    /// <summary>True for the candidate the player manages.</summary>
    public bool IsPlayer { get; set; }

    /// <summary>Current support share, 0..100.</summary>
    public double SupportShare { get; set; }

    /// <summary>Momentum, 0..100 (centered at 50). Amplifies the candidate's weekly gains.</summary>
    public double Momentum { get; set; } = 50;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A persisted snapshot of one completed campaign week (for the trend chart + recap).</summary>
public class CivicCampaignWeek
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }
    public CivicCampaign? Campaign { get; set; }

    public int WeekNumber { get; set; }

    /// <summary>Managed candidate's support share after this week resolved.</summary>
    public double PlayerSupportAfter { get; set; }

    /// <summary>JSON: the salient issues that were in play this week.</summary>
    public string SalientIssuesJson { get; set; } = "[]";

    /// <summary>JSON: full per-candidate standings snapshot after the week.</summary>
    public string StandingsJson { get; set; } = "[]";

    /// <summary>JSON: breakdown of how the player's support delta was computed.</summary>
    public string DeltaBreakdownJson { get; set; } = "{}";

    [MaxLength(600)]
    public string Summary { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A single action the player took during a week.</summary>
public class CivicCampaignAction
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }
    public CivicCampaign? Campaign { get; set; }

    public int WeekNumber { get; set; }

    public CivicCampaignActionType ActionType { get; set; }

    /// <summary>The issue/axis/plank the action targeted (free-form key).</summary>
    [MaxLength(120)]
    public string? Target { get; set; }

    /// <summary>Tone chosen for a PublishPost action.</summary>
    public CampaignTone? Tone { get; set; }

    /// <summary>Support delta this action contributed to the managed candidate.</summary>
    public double SupportDelta { get; set; }

    /// <summary>The generated CampaignPost, when this action published one.</summary>
    public Guid? GeneratedPostId { get; set; }

    [MaxLength(400)]
    public string Summary { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
