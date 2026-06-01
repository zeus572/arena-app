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

    // Campaign duration is no longer chosen — it snaps to the live election date.
}

public class TakeActionRequest
{
    [Required]
    public CivicCampaignActionType ActionType { get; set; }

    /// <summary>For RespondToNews: the briefing slug to respond to.</summary>
    public string? BriefingSlug { get; set; }

    /// <summary>For RespondToNews: the chosen response option id.</summary>
    public string? OptionId { get; set; }

    /// <summary>Issue tag / plank title / axis key the action targets (for secondary actions).</summary>
    public string? Target { get; set; }

    /// <summary>Tone to use for the post; defaults to the candidate's default tone.</summary>
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
    public int CurrentDay { get; set; }
    public int TotalDays { get; set; }
    public int DaysRemaining { get; set; }
    public string ElectionName { get; set; } = "";
    public DateTime ElectionDate { get; set; }
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
    public int DayNumber { get; set; }
    public double PlayerSupportAfter { get; set; }
    public List<string> SalientIssues { get; set; } = new();
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class CivicCampaignActionDto
{
    public int DayNumber { get; set; }
    public string ActionType { get; set; } = "";
    public string? Target { get; set; }
    public string? RespondedBriefingSlug { get; set; }
    public string? Tone { get; set; }
    public double SupportDelta { get; set; }
    public Guid? GeneratedPostId { get; set; }
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>An action option offered to the player for the current day.</summary>
public class CivicActionOptionDto
{
    public string ActionType { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Suggested target (issue/axis) for this option, when applicable.</summary>
    public string? SuggestedTarget { get; set; }
}

/// <summary>A single way the candidate could respond to a news item.</summary>
public class NewsResponseOptionDto
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Angle { get; set; } = "";
    public string Tone { get; set; } = "";
}

/// <summary>A news item (briefing) the manager can choose to respond to, with ready options.</summary>
public class CampaignNewsItemDto
{
    public string BriefingSlug { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> ValuesInConflict { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<NewsResponseOptionDto> Options { get; set; } = new();
}

/// <summary>A response option including the full ready-to-publish post body (for the response page).</summary>
public class NewsResponseOptionDetailDto
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Angle { get; set; } = "";
    public string Tone { get; set; } = "";
    /// <summary>The actual post the candidate would publish if this option is chosen.</summary>
    public string Body { get; set; } = "";
}

/// <summary>
/// Everything the response page needs: a summary of the candidate's profile + values, the news
/// item being responded to, and each response option's full post text.
/// </summary>
public class NewsResponsePageDto
{
    public Guid CampaignId { get; set; }
    public string CandidateSlug { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Party { get; set; } = "";
    public string CandidateBio { get; set; } = "";
    public string AvatarBaseUrl { get; set; } = "";
    public List<CandidateValueDto> Values { get; set; } = new();
    public List<PlatformPlankDto> Platform { get; set; } = new();

    public string BriefingSlug { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> ValuesInConflict { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public bool AlreadyResponded { get; set; }
    public int ActionsRemaining { get; set; }
    public List<NewsResponseOptionDetailDto> Options { get; set; } = new();
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

    public string ElectionName { get; set; } = "";
    public DateTime ElectionDate { get; set; }
    public int DaysRemaining { get; set; }
    public int CurrentDay { get; set; }
    public int TotalDays { get; set; }

    public int ActionsRemaining { get; set; }
    public bool? Won { get; set; }
    public double? FinalSupport { get; set; }
    public string? Outcome { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CivicCampaignStandingDto> Standings { get; set; } = new();
    public List<string> SalientIssues { get; set; } = new();

    /// <summary>The primary mechanic: news items the manager can respond to, with ready options.</summary>
    public List<CampaignNewsItemDto> NewsItems { get; set; } = new();

    /// <summary>Secondary "budgeting tools" (Target Issue / Shore Up a Weakness).</summary>
    public List<CivicActionOptionDto> AvailableActions { get; set; } = new();
    public List<CivicCampaignActionDto> TodayActions { get; set; } = new();
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

public class AdvanceDayResult
{
    public int CompletedDay { get; set; }
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
