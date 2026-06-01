namespace Arena.API.Models;

public enum CampaignStatus
{
    Active,
    Completed,
    Abandoned
}

public enum CampaignDifficulty
{
    Easy,
    Normal,
    Hard
}

public enum CampaignEventType
{
    Opportunity,
    Crisis,
    Neutral
}

public enum CampaignActivityType
{
    Advertising,
    TownHall,
    Fundraising,
    OppResearch,
    DebatePrep,
    Polling
}

/// <summary>
/// A single-player Campaign Manager run. Scoped to a user, resumable, persisted in Postgres.
/// </summary>
public class Campaign
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string OpponentName { get; set; } = string.Empty;
    public string OpponentPersona { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    /// <summary>JSON object of platform planks: { "issue": "stance" }.</summary>
    public string PlatformJson { get; set; } = "{}";
    public int CurrentWeek { get; set; } = 1;
    public int TotalWeeks { get; set; } = 4;
    public CampaignDifficulty Difficulty { get; set; } = CampaignDifficulty.Normal;
    public CampaignStatus Status { get; set; } = CampaignStatus.Active;
    /// <summary>Current approval rating, 0–100.</summary>
    public double Approval { get; set; } = 50;
    public bool? Won { get; set; }
    public double? FinalApproval { get; set; }
    public string? Outcome { get; set; }
    /// <summary>The most recent week whose debate milestone has been resolved (run or skipped).</summary>
    public int LastResolvedDebateWeek { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public CampaignResources Resources { get; set; } = null!;
    public ICollection<CampaignWeek> Weeks { get; set; } = new List<CampaignWeek>();
    public ICollection<CampaignEvent> Events { get; set; } = new List<CampaignEvent>();
}

/// <summary>1:1 resource pool for a campaign.</summary>
public class CampaignResources
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public double Budget { get; set; } = 100000;
    public int TimeUnits { get; set; } = 40;
    public int StaffCount { get; set; } = 5;
    public double Momentum { get; set; } = 50;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A per-week snapshot of a campaign's state and the decisions made that week.</summary>
public class CampaignWeek
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public int WeekNumber { get; set; }
    public double ApprovalRating { get; set; }
    /// <summary>JSON array of the activity allocations chosen this week.</summary>
    public string DecisionsJson { get; set; } = "[]";
    /// <summary>JSON object describing the resource deltas applied this week.</summary>
    public string ResourceChangesJson { get; set; } = "{}";
    public Guid? DebateId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A templated campaign event presented to the player for a response.</summary>
public class CampaignEvent
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public int WeekNumber { get; set; }
    public CampaignEventType Type { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>JSON array of response options: [{ id, label, approval, budget, momentum }].</summary>
    public string OptionsJson { get; set; } = "[]";
    public string? ResponseChosen { get; set; }
    /// <summary>JSON object describing the applied effects once resolved.</summary>
    public string? OutcomeJson { get; set; }
    public bool Resolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
