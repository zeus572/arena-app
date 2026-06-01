using System.ComponentModel.DataAnnotations;
using Civic.API.Models;

namespace Civic.API.Models.DTOs;

// ---- Requests ----

public class CreateCivicCampaignRequest
{
    /// <summary>Slug of the existing VirtualCandidate the player will manage.</summary>
    [Required]
    public string CandidateSlug { get; set; } = "";

    public CivicCampaignDifficulty Difficulty { get; set; } = CivicCampaignDifficulty.Normal;

    /// <summary>Campaign length in weeks. Falls back to the configured default when null.</summary>
    public int? TotalWeeks { get; set; }
}

public class TakeActionRequest
{
    [Required]
    public CivicCampaignActionType ActionType { get; set; }

    /// <summary>Issue tag / plank title / axis key the action targets (depends on ActionType).</summary>
    public string? Target { get; set; }

    /// <summary>Tone to use for a PublishPost action; defaults to the candidate's default tone.</summary>
    public CampaignTone? Tone { get; set; }
}

// ---- Responses ----

public class CivicCampaignSummaryDto
{
    public Guid Id { get; set; }
    public string CandidateSlug { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Party { get; set; } = "";
    public string RaceLabel { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Status { get; set; } = "";
    public int CurrentWeek { get; set; }
    public int TotalWeeks { get; set; }
    public double PlayerSupport { get; set; }
    public bool IsLeading { get; set; }
    public bool? Won { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CivicCampaignStandingDto
{
    public Guid CandidateId { get; set; }
    public string CandidateSlug { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Party { get; set; } = "";
    public bool IsPlayer { get; set; }
    public double SupportShare { get; set; }
    public double Momentum { get; set; }
}

public class CivicCampaignWeekDto
{
    public int WeekNumber { get; set; }
    public double PlayerSupportAfter { get; set; }
    public List<string> SalientIssues { get; set; } = new();
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class CivicCampaignActionDto
{
    public int WeekNumber { get; set; }
    public string ActionType { get; set; } = "";
    public string? Target { get; set; }
    public string? Tone { get; set; }
    public double SupportDelta { get; set; }
    public Guid? GeneratedPostId { get; set; }
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>An action option offered to the player for the current week.</summary>
public class CivicActionOptionDto
{
    public string ActionType { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Suggested target (issue/axis) for this option, when applicable.</summary>
    public string? SuggestedTarget { get; set; }
}

public class CivicCampaignDetailDto
{
    public Guid Id { get; set; }
    public string CandidateSlug { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Party { get; set; } = "";
    public string CandidateBio { get; set; } = "";
    public string AvatarBaseUrl { get; set; } = "";
    public string RaceKey { get; set; } = "";
    public string RaceLabel { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Status { get; set; } = "";
    public int CurrentWeek { get; set; }
    public int TotalWeeks { get; set; }
    public int ActionsRemaining { get; set; }
    public bool? Won { get; set; }
    public double? FinalSupport { get; set; }
    public string? Outcome { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CivicCampaignStandingDto> Standings { get; set; } = new();
    public List<string> SalientIssues { get; set; } = new();
    public List<CivicActionOptionDto> AvailableActions { get; set; } = new();
    public List<CivicCampaignActionDto> ThisWeekActions { get; set; } = new();
    public List<CivicCampaignWeekDto> History { get; set; } = new();
}

public class TakeActionResult
{
    public CivicCampaignActionDto Action { get; set; } = new();
    public double PlayerSupportAfter { get; set; }
    public int ActionsRemaining { get; set; }
    public string? GeneratedPostBody { get; set; }
    public CivicCampaignDetailDto Campaign { get; set; } = new();
}

public class AdvanceWeekResult
{
    public int CompletedWeek { get; set; }
    public double PlayerSupportAfter { get; set; }
    public bool IsLeading { get; set; }
    public List<CivicCampaignStandingDto> Standings { get; set; } = new();
    public string Summary { get; set; } = "";
    public bool CampaignCompleted { get; set; }
    public CivicCampaignDetailDto Campaign { get; set; } = new();
}

public class CivicCampaignResultsDto
{
    public Guid Id { get; set; }
    public string CandidateName { get; set; } = "";
    public string RaceLabel { get; set; } = "";
    public bool Won { get; set; }
    public double FinalSupport { get; set; }
    public int FinalRank { get; set; }
    public int FieldSize { get; set; }
    public int TotalWeeks { get; set; }
    public string Outcome { get; set; } = "";
    public List<CivicCampaignStandingDto> FinalStandings { get; set; } = new();
    public List<CivicCampaignWeekDto> SupportTrend { get; set; } = new();
}

// ---- Race picker ----

public class CivicRaceDto
{
    public string RaceKey { get; set; } = "";
    public string Office { get; set; } = "";
    public string? State { get; set; }
    public int? District { get; set; }
    public string Label { get; set; } = "";
    public List<CandidateSummaryDto> Candidates { get; set; } = new();
}
