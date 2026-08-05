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

/// <summary>One row of the "Latest" section (design 1g).</summary>
public class DevelopmentDto
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Category { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    /// <summary>Required on every development.</summary>
    public string WhyItMatters { get; set; } = "";
    /// <summary>Which clause of the inclusion rule let this in.</summary>
    public string InclusionReason { get; set; } = "";
    public string EvidenceStatus { get; set; } = "";
    public Guid? StoryRoomId { get; set; }
    public string? StorySlug { get; set; }
}

/// <summary>
/// The Latest section, with its honest denominator. Design 1g states the bound plainly:
/// "Eight developments in 34 days. We logged 260 articles and judged eight of them to have
/// changed something." Both numbers ship with the list so the disclosure cannot drift.
/// </summary>
public class RoomLatestDto
{
    public List<DevelopmentDto> Developments { get; set; } = new();
    public int ArticlesConsidered { get; set; }
    public int WindowDays { get; set; }
    public string[] InclusionRules { get; set; } = Array.Empty<string>();
    public string[] ExclusionRules { get; set; } = Array.Empty<string>();
    /// <summary>Considered minus logged — "what we left out".</summary>
    public int ExcludedCount { get; set; }
}

/// <summary>One point on the design 1h timeline.</summary>
public class TimelineEventDto
{
    public DateOnly OccurredOn { get; set; }
    public string OccurredPrecision { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Agreed | Contested | Trigger | Now — the marker vocabulary.</summary>
    public string Marker { get; set; } = "";
    /// <summary>What was known ON this date, not what is known now.</summary>
    public string? WhatWasKnownThen { get; set; }
    /// <summary>Required accessibility alternative to the visual track.</summary>
    public string? TextAlternative { get; set; }
}

/// <summary>An actor card (design 1i), answering the five questions in fixed order.</summary>
public class RoomActorDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string ActorType { get; set; } = "";
    /// <summary>Decides | Shapes | Constrained, relative to the requested decision.</summary>
    public string Tier { get; set; } = "";
    public string RoleHere { get; set; } = "";
    public string ActualPower { get; set; } = "";
    /// <summary>Always a quote or filing with a date — never inferred motive.</summary>
    public string? StatedWants { get; set; }
    public DateTime? StatedWantsAsOf { get; set; }
    public Guid? StatedWantsSourceRefId { get; set; }
    public string ConstrainedBy { get; set; } = "";
    public string LeverageStatement { get; set; } = "";
    /// <summary>How many rooms and stories this actor appears in.</summary>
    public int AppearanceCount { get; set; }
}

/// <summary>The actor map, plus the decision its ordering is relative to.</summary>
public class RoomActorsDto
{
    /// <summary>Null when this is the room's default tiering.</summary>
    public string? DecisionKey { get; set; }
    /// <summary>Other decisions this room can re-sort by.</summary>
    public List<string> AvailableDecisions { get; set; } = new();
    public List<RoomActorDto> Decides { get; set; } = new();
    public List<RoomActorDto> Shapes { get; set; } = new();
    public List<RoomActorDto> Constrained { get; set; } = new();
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
