namespace Civic.API.Models.DTOs;

public class CandidateSummaryDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Office { get; set; } = "";
    public string? State { get; set; }
    public int? District { get; set; }
    public string Party { get; set; } = "";
    public bool IsIncumbent { get; set; }
    public string Bio { get; set; } = "";
    public string ArchetypeKey { get; set; } = "";
    public string DefaultTone { get; set; } = "";
    public int DefaultIntensity { get; set; }
    public string AvatarBaseUrl { get; set; } = "";
    /// <summary>Always true — these candidates are fictional simulations.</summary>
    public bool IsFictional { get; set; } = true;
}

public class CandidateDetailDto : CandidateSummaryDto
{
    public string Background { get; set; } = "";
    public List<PlatformPlankDto> PlatformPlanks { get; set; } = new();
    public List<CandidateValueDto> Values { get; set; } = new();
    public List<IssueToneDto> IssueTones { get; set; } = new();
    public int PostCount { get; set; }
}

public class PlatformPlankDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string[] IssueTags { get; set; } = Array.Empty<string>();
}

public class CandidateSourceDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string[] IssueTags { get; set; } = Array.Empty<string>();
    public int Priority { get; set; }
}

public class CandidateValueDto
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public string LowLabel { get; set; } = "";
    public string HighLabel { get; set; } = "";
    public int Order { get; set; }
    public double Score { get; set; }
}

public class IssueToneDto
{
    public string Issue { get; set; } = "";
    public string Tone { get; set; } = "";
    public string ToneLabel { get; set; } = "";
    public int Intensity { get; set; }
    public string IntensityLabel { get; set; } = "";
}

public class PostFragmentDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = "";
    public int Start { get; set; }
    public int End { get; set; }
    public int Order { get; set; }
    public int Up { get; set; }
    public int Down { get; set; }
}

public class CampaignPostDto
{
    public Guid Id { get; set; }
    public string Body { get; set; } = "";
    public string Tone { get; set; } = "";
    public string ToneLabel { get; set; } = "";
    public int Intensity { get; set; }
    public string IntensityLabel { get; set; } = "";
    public string[] IssueTags { get; set; } = Array.Empty<string>();
    public string Trigger { get; set; } = "";
    public string? TriggerBriefingSlug { get; set; }
    public string? TriggerBriefingHeadline { get; set; }
    /// <summary>Short snippet of the briefing being responded to, for a quoted-source preview.</summary>
    public string? TriggerBriefingSummary { get; set; }
    public Guid? TriggerPostId { get; set; }
    public string? CitedReference { get; set; }
    public int Up { get; set; }
    public int Down { get; set; }
    public DateTime CreatedAt { get; set; }
    public CandidateSummaryDto? Candidate { get; set; }
    public List<PostFragmentDto> Fragments { get; set; } = new();
}

public class CampaignFeedDto
{
    public List<CampaignPostDto> Items { get; set; } = new();
    public string? NextCursor { get; set; }
}

public class HeatmapFragmentDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = "";
    public int Start { get; set; }
    public int End { get; set; }
    public int Order { get; set; }
    public int Up { get; set; }
    public int Down { get; set; }
    /// <summary>Net sentiment in [-1, 1]: (up - down) / (up + down).</summary>
    public double Net { get; set; }
}

public class PostHeatmapDto
{
    public Guid PostId { get; set; }
    public string Body { get; set; } = "";
    public List<HeatmapFragmentDto> Fragments { get; set; } = new();
}

public class ReactionRequestDto
{
    public string Type { get; set; } = "";
}

public class ReactionResultDto
{
    public Guid PostId { get; set; }
    public Guid? FragmentId { get; set; }
    public int PostUp { get; set; }
    public int PostDown { get; set; }
    public int? FragmentUp { get; set; }
    public int? FragmentDown { get; set; }
}

public class ElectionCycleDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime ElectionDate { get; set; }
    public DateTime PrimarySeasonStart { get; set; }
    public DateTime GeneralSeasonStart { get; set; }
    public bool IsCurrent { get; set; }
    public int DaysUntilElection { get; set; }
}

public class RaceDto
{
    public string Office { get; set; } = "";
    public string? State { get; set; }
    public int? District { get; set; }
    public string Label { get; set; } = "";
    public List<CandidateSummaryDto> Candidates { get; set; } = new();
}

public class CandidateMatchItemDto
{
    public CandidateSummaryDto Candidate { get; set; } = new();
    public double Score { get; set; }
    public string Reason { get; set; } = "";
}

public class CandidateMatchesDto
{
    public bool HasProfile { get; set; }
    public List<CandidateMatchItemDto> TopMatches { get; set; } = new();
    public List<CandidateMatchItemDto> ProductiveChallenges { get; set; } = new();
    public List<CandidateMatchItemDto> SurprisingAgreements { get; set; } = new();
}

public class CandidateBudgetDto
{
    public Guid CandidateId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public int PostsLast24h { get; set; }
    public int Intensity5Last24h { get; set; }
    public int PostsTotal { get; set; }
    public DateTime? LastPostAt { get; set; }
}

public class AdminBudgetDto
{
    public int TotalPosts { get; set; }
    public int PostsLast24h { get; set; }
    public List<CandidateBudgetDto> Candidates { get; set; } = new();
}

public class GeneratePostRequestDto
{
    public Guid? TriggerBriefingId { get; set; }
    public bool Force { get; set; }
}
