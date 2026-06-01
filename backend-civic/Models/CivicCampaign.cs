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
    /// <summary>Publish a campaign post (pick a plank/issue + tone). Generates a real CampaignPost.
    /// Retained for compatibility; no longer offered as a primary option.</summary>
    PublishPost,

    /// <summary>Respond to a hot news briefing this week (offense / disciplined / pivot).
    /// Retained for compatibility; superseded by <see cref="RespondToNews"/>.</summary>
    RapidResponse,

    /// <summary>Invest a turn shoring up a weak value-axis to blunt opponent gains there.</summary>
    ShoreUpAxis,

    /// <summary>Concentrate the day on a high-salience issue the candidate is strong on.</summary>
    TargetIssue,

    /// <summary>Respond to a specific incoming news briefing with a chosen response option.
    /// The primary mechanic: pick a news item and how the candidate responds.</summary>
    RespondToNews,
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

    /// <summary>The live election this campaign is tied to (the one the home countdown shows).</summary>
    public Guid ElectionId { get; set; }
    public Election? Election { get; set; }

    [MaxLength(200)]
    public string ElectionName { get; set; } = "";

    /// <summary>Polls-close date (UTC) the campaign counts down to.</summary>
    public DateTime ElectionDate { get; set; }

    /// <summary>Stable key for the race = office[/state[/district]]. Opponents share this key.</summary>
    [Required, MaxLength(60)]
    public string RaceKey { get; set; } = "";

    [MaxLength(160)]
    public string RaceLabel { get; set; } = "";

    public CivicCampaignDifficulty Difficulty { get; set; } = CivicCampaignDifficulty.Normal;

    /// <summary>Total playable days from campaign start to election day (bounded by options).</summary>
    public int TotalDays { get; set; }

    /// <summary>1-based current campaign day.</summary>
    public int CurrentDay { get; set; } = 1;

    public CivicCampaignStatus Status { get; set; } = CivicCampaignStatus.Active;

    /// <summary>Set on the final week: did the managed candidate finish first in the race?</summary>
    public bool? Won { get; set; }

    /// <summary>Final support share (0..100) of the managed candidate.</summary>
    public double? FinalSupport { get; set; }

    [MaxLength(400)]
    public string? Outcome { get; set; }

    /// <summary>Remaining action points for the current day (refilled on advance).</summary>
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

    /// <summary>Momentum, 0..100 (centered at 50). Amplifies the candidate's daily gains.</summary>
    public double Momentum { get; set; } = 50;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A persisted snapshot of one completed campaign day (for the trend chart + recap).</summary>
public class CivicCampaignWeek
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }
    public CivicCampaign? Campaign { get; set; }

    /// <summary>1-based campaign day this snapshot is for.</summary>
    public int DayNumber { get; set; }

    /// <summary>Managed candidate's support share after this day resolved.</summary>
    public double PlayerSupportAfter { get; set; }

    /// <summary>JSON: the salient issues that were in play this day.</summary>
    public string SalientIssuesJson { get; set; } = "[]";

    /// <summary>JSON: full per-candidate standings snapshot after the day.</summary>
    public string StandingsJson { get; set; } = "[]";

    /// <summary>JSON: breakdown of how the player's support delta was computed.</summary>
    public string DeltaBreakdownJson { get; set; } = "{}";

    [MaxLength(600)]
    public string Summary { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A single action the player took during a day.</summary>
public class CivicCampaignAction
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }
    public CivicCampaign? Campaign { get; set; }

    /// <summary>1-based campaign day the action was taken on.</summary>
    public int DayNumber { get; set; }

    public CivicCampaignActionType ActionType { get; set; }

    /// <summary>The issue/axis/plank the action targeted (free-form key).</summary>
    [MaxLength(120)]
    public string? Target { get; set; }

    /// <summary>For RespondToNews: the briefing slug the candidate responded to.</summary>
    [MaxLength(160)]
    public string? RespondedBriefingSlug { get; set; }

    /// <summary>Tone chosen for the post.</summary>
    public CampaignTone? Tone { get; set; }

    /// <summary>Support delta this action contributed to the managed candidate.</summary>
    public double SupportDelta { get; set; }

    /// <summary>The generated CampaignPost, when this action published one.</summary>
    public Guid? GeneratedPostId { get; set; }

    [MaxLength(400)]
    public string Summary { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cached, pre-generated set of response options for a (candidate, briefing) pair. Generated
/// lazily the first time the briefing is offered to any campaign managing that candidate, then
/// reused across views and players. The options JSON is a list of
/// { id, label, angle, tone, body } objects.
/// </summary>
public class CandidateNewsResponse
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    /// <summary>Slug of the Briefing these options respond to.</summary>
    [Required, MaxLength(160)]
    public string BriefingSlug { get; set; } = "";

    /// <summary>JSON array of response options: [{ id, label, angle, tone, body }].</summary>
    public string OptionsJson { get; set; } = "[]";

    /// <summary>True when the options were produced by the LLM (vs. the templated fallback).</summary>
    public bool LlmGenerated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
