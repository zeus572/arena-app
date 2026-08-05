namespace Civic.API.Models.DTOs;

/// <summary>One typed changelog line, as rendered by the delta ledger (design 1d).</summary>
public class ChangeLogEntryDto
{
    public string Type { get; set; } = "";
    /// <summary>The short uppercase word in the ledger's type column.</summary>
    public string Label { get; set; } = "";
    public bool IsMeaningful { get; set; }
    public string Headline { get; set; } = "";
    public string? WhyItMatters { get; set; }
    public string? ObjectType { get; set; }
    public Guid? ObjectId { get; set; }
    /// <summary>For a status move the ledger renders the transition literally:
    /// old mark, word, arrow, new mark, word.</summary>
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? CorrectionKind { get; set; }
    public int Revision { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>An honest count of the edits we chose not to interrupt anyone for.</summary>
public class WithheldChangeDto
{
    public string Type { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>
/// "Since your last visit."
///
/// Corrections are a separate array from meaningful changes on purpose — the handoff
/// requires corrections never be folded into "updated", and splitting them at the API
/// boundary means the frontend cannot merge them by accident.
/// </summary>
public class RoomDeltaDto
{
    public int FromRevision { get; set; }
    public int ToRevision { get; set; }
    public List<ChangeLogEntryDto> MeaningfulChanges { get; set; } = new();
    public List<ChangeLogEntryDto> Corrections { get; set; } = new();
    /// <summary>"11 edits we did not bother you with."</summary>
    public int WithheldCount { get; set; }
    public List<WithheldChangeDto> WithheldByType { get; set; } = new();

    public bool HasChanges => MeaningfulChanges.Count > 0 || Corrections.Count > 0;
}

/// <summary>A room in a list. Deliberately thin — the front door is a separate fetch.</summary>
public class RoomSummaryDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Dek { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Locality { get; set; }
    public int Revision { get; set; }
    public DateTime? LastMeaningfulUpdateAt { get; set; }
    public string? ContentNote { get; set; }
    /// <summary>Story rooms only.</summary>
    public string? StoryType { get; set; }
    public DateTime? EventTime { get; set; }
    public int? EstimatedMinutes { get; set; }
}

/// <summary>An essential fact on the front door. Status comes from the claim, not from here.</summary>
public class EssentialFactDto
{
    public string Text { get; set; } = "";
    public Guid? ClaimId { get; set; }
    public string? ClaimSlug { get; set; }
    /// <summary>Rendered as the evidence mark. Null when the fact has no claim attached yet.</summary>
    public string? ClaimStatus { get; set; }
    public int Ordinal { get; set; }
}

public class TerminologyNoteDto
{
    public string Term { get; set; } = "";
    public string Note { get; set; } = "";
}

public class SectionProgressDto
{
    public string SectionKey { get; set; } = "";
    public bool Opened { get; set; }
    public int ItemsSeen { get; set; }
    public int ItemsTotal { get; set; }
}

/// <summary>Everything personal about this reader's relationship to the room.</summary>
public class RoomViewerStateDto
{
    public int LastSeenRevision { get; set; }
    public bool Following { get; set; }
    public string Density { get; set; } = "Read";
    public List<SectionProgressDto> SectionProgress { get; set; } = new();
    /// <summary>Populated on a return visit so the ribbon can render without a second call.</summary>
    public RoomDeltaDto? Delta { get; set; }
}

/// <summary>The Theme Room front door (design 1a).</summary>
public class ThemeRoomDetailDto : RoomSummaryDto
{
    public string[] AlternateTitles { get; set; } = Array.Empty<string>();
    public string ScopeStatement { get; set; } = "";
    public string[] InclusionRules { get; set; } = Array.Empty<string>();
    public string[] ExclusionRules { get; set; } = Array.Empty<string>();
    /// <summary>The room's most important element. Describes a state, not an event.</summary>
    public string CurrentStatusSentence { get; set; } = "";
    public string TopUnresolvedQuestion { get; set; } = "";
    public string WatchNext { get; set; } = "";
    public List<EssentialFactDto> EssentialFacts { get; set; } = new();
    public List<TerminologyNoteDto> TerminologyNotes { get; set; } = new();
    public string MonitoringCadence { get; set; } = "";
    /// <summary>The honest denominator: how many candidates we considered over the window.</summary>
    public int ArticlesConsideredCount { get; set; }
    public int DevelopmentWindowDays { get; set; }
    public RoomViewerStateDto Viewer { get; set; } = new();
}

public class StoryDimensionDto
{
    public string Dimension { get; set; } = "";
    public string Text { get; set; } = "";
    public Guid? ClaimId { get; set; }
}

public class StakeholderImpactDto
{
    public string Group { get; set; } = "";
    public string ImpactSummary { get; set; } = "";
    public double Confidence { get; set; }
}

public class NextStepDto
{
    public string Description { get; set; } = "";
    /// <summary>"Confirmed if:" — the objective criterion.</summary>
    public string VerificationCondition { get; set; } = "";
    public Guid? ActorId { get; set; }
    public string? ExpectedTiming { get; set; }
    public Guid? PredictionId { get; set; }
}

/// <summary>A Story Room (designs 1o / 1p).</summary>
public class StoryRoomDetailDto : RoomSummaryDto
{
    public string HowItWorksIntro { get; set; } = "";
    public List<StoryDimensionDto> WhyItMatters { get; set; } = new();
    public List<StakeholderImpactDto> Stakeholders { get; set; } = new();
    public List<NextStepDto> NextSteps { get; set; } = new();
    /// <summary>Story-type-specific fields, shape per StoryType.</summary>
    public System.Text.Json.JsonElement? TypePayload { get; set; }
    public Guid? SourceBillId { get; set; }
    public RoomViewerStateDto Viewer { get; set; } = new();
}
