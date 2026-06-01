using Arena.API.Models;

namespace Arena.API.Models.DTOs;

// ---- Requests ----

public class CreateCampaignRequest
{
    public string CandidateName { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public CampaignDifficulty Difficulty { get; set; } = CampaignDifficulty.Normal;
    public int? TotalWeeks { get; set; }
    public string? Theme { get; set; }
    public Dictionary<string, string>? Platform { get; set; }
}

public class ActivityAllocationDto
{
    public CampaignActivityType Type { get; set; }
    public double? Budget { get; set; }
    public int? TimeUnits { get; set; }
    public int? StaffCount { get; set; }
    public int? Count { get; set; }
}

public class AdvanceWeekRequest
{
    public List<ActivityAllocationDto> Activities { get; set; } = new();
}

public class RespondEventRequest
{
    public string OptionId { get; set; } = string.Empty;
}

public class RunDebateRequest
{
    public bool Skip { get; set; }
    public string? Topic { get; set; }
}

// ---- Results / read models ----

public class PersonaDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string OpponentName { get; set; } = string.Empty;
    public string OpponentPersona { get; set; } = string.Empty;
}

public class CampaignSummaryDto
{
    public Guid Id { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public string OpponentName { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public int CurrentWeek { get; set; }
    public int TotalWeeks { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Approval { get; set; }
    public bool? Won { get; set; }
    public double? FinalApproval { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CampaignResourcesDto
{
    public double Budget { get; set; }
    public int TimeUnits { get; set; }
    public int StaffCount { get; set; }
    public double Momentum { get; set; }
}

public class CampaignWeekDto
{
    public int WeekNumber { get; set; }
    public double ApprovalRating { get; set; }
    public string DecisionsJson { get; set; } = "[]";
    public string ResourceChangesJson { get; set; } = "{}";
    public Guid? DebateId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CampaignEventOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class CampaignEventDto
{
    public Guid Id { get; set; }
    public int WeekNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CampaignEventOptionDto> Options { get; set; } = new();
    public bool Resolved { get; set; }
    public string? ResponseChosen { get; set; }
}

public class CampaignDetailDto
{
    public CampaignSummaryDto Campaign { get; set; } = new();
    public CampaignResourcesDto Resources { get; set; } = new();
    public double CurrentApproval { get; set; }
    public List<CampaignWeekDto> Weeks { get; set; } = new();
    public List<CampaignEventDto> PendingEvents { get; set; } = new();
    public bool DebateMilestoneDue { get; set; }
    public Guid? ActiveDebateId { get; set; }
}

public class AdvanceWeekResult
{
    public CampaignDetailDto Detail { get; set; } = new();
    public CampaignWeekDto WeekSummary { get; set; } = new();
    public bool Completed { get; set; }
    public bool DebateMilestoneDue { get; set; }
}

public class DebateMilestoneResult
{
    public Guid? DebateId { get; set; }
    public bool Skipped { get; set; }
    public bool? Won { get; set; }
    public double SignedEffect { get; set; }
    public string Summary { get; set; } = string.Empty;
    public CampaignDetailDto Detail { get; set; } = new();
}

public class AllocationLineItem
{
    public string Type { get; set; } = string.Empty;
    public double BudgetCost { get; set; }
    public int TimeCost { get; set; }
    public string? Note { get; set; }
}

public class AllocationPreviewResult
{
    public bool Affordable { get; set; }
    public double ProjectedBudget { get; set; }
    public int ProjectedTimeUnits { get; set; }
    public int ProjectedStaff { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<object> LineItems { get; set; } = new();
}

public class CampaignResultsDto
{
    public string CandidateName { get; set; } = string.Empty;
    public bool Won { get; set; }
    public double FinalApproval { get; set; }
    public int TotalWeeks { get; set; }
    public int DebatesPlayed { get; set; }
    public int DebatesWon { get; set; }
    public List<double> ApprovalTrend { get; set; } = new();
    public string Outcome { get; set; } = string.Empty;
}
