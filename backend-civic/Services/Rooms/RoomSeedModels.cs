namespace Civic.API.Services.Rooms;

/// <summary>
/// The hand-authored pilot-room file format (Seed/rooms/*.json).
///
/// Everything cross-references by SLUG rather than by Guid, so the file is editable by a
/// person, diffable in review, and re-runnable — the seeder resolves slugs to ids in two
/// passes. That is the whole reason this shape exists instead of dumping entities.
/// </summary>
public class RoomSeedFile
{
    public List<SeedSource> Sources { get; set; } = new();
    public List<SeedConcept> Concepts { get; set; } = new();
    public List<SeedActor> Actors { get; set; } = new();
    public List<SeedClaim> Claims { get; set; } = new();
    public SeedThemeRoom? Theme { get; set; }
    public List<SeedStoryRoom> Stories { get; set; } = new();
}

public class SeedSource
{
    /// <summary>File-local key other entries cite. Not persisted.</summary>
    public string Key { get; set; } = "";
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Organization { get; set; }
    /// <summary>PrimaryDocument | GovernmentData | DirectStatement | Reporting | Analysis | PublicReaction.</summary>
    public string SourceType { get; set; } = "Reporting";
    public bool IsPrimary { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool FullTextAvailable { get; set; }
}

public class SeedConcept
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string KnowledgeKind { get; set; } = "Concept";
    public string ShortGloss { get; set; } = "";
    public string PlainDefinition { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public string CurrentExample { get; set; } = "";
    public string CommonMisunderstanding { get; set; } = "";
    public string TryItQuestion { get; set; } = "";
    /// <summary>The concept this one is easy to confuse with (design 1h).</summary>
    public string? ConfusionPairSlug { get; set; }
    public string? ConfusionDiscriminator { get; set; }
}

public class SeedActor
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string ActorType { get; set; } = "GovernmentBody";
    public string[] AlternateNames { get; set; } = Array.Empty<string>();
    public string ActualPower { get; set; } = "";
    public string ConstrainedBy { get; set; } = "";
    /// <summary>Must be accompanied by <see cref="StatedWantsSourceKey"/> — design 1i
    /// requires a quote or filing, never inferred motive.</summary>
    public string? StatedWants { get; set; }
    public string? StatedWantsSourceKey { get; set; }
    public DateTime? StatedWantsAsOf { get; set; }
    /// <summary>Decides | Shapes | Constrained.</summary>
    public string Tier { get; set; } = "Shapes";
    public string LeverageStatement { get; set; } = "";
    public string RoleHere { get; set; } = "";

    /// <summary>
    /// Named decisions this actor is ALSO tiered for, on top of the room's default map.
    ///
    /// Additive, never instead-of. An actor listed only under a decision key would vanish
    /// from the unfiltered People &amp; Power view, which is where most readers will only
    /// ever look — so the seeder always writes the default tiering and treats these as
    /// extra rows.
    /// </summary>
    public List<SeedActorDecision> Decisions { get; set; } = new();

    public int Ordinal { get; set; }
}

/// <summary>
/// An actor's tiering relative to ONE named decision (design 1i).
///
/// The whole point of the decision selector is that leverage is not a property of an actor
/// but of an actor-and-a-decision: the appropriations committees decide an appropriation and
/// merely shape whether a agency releases it. Every field except <see cref="Key"/> falls
/// back to the actor's room-wide value, so a decision entry only states what differs.
/// </summary>
public class SeedActorDecision
{
    public string Key { get; set; } = "";
    /// <summary>Decides | Shapes | Constrained. Defaults to the actor's room-wide tier.</summary>
    public string? Tier { get; set; }
    public string? LeverageStatement { get; set; }
    public string? RoleHere { get; set; }
}

public class SeedClaim
{
    public string Slug { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>One of the eight ClaimStatus members.</summary>
    public string Status { get; set; } = "PlausibleButUnresolved";
    /// <summary>Factual | Interpretation | Opinion | Prediction.</summary>
    public string Kind { get; set; } = "Factual";
    public string? EvidenceSummary { get; set; }
    /// <summary>Required. A claim nobody can say how to settle is not a claim.</summary>
    public string WhatWouldSettleIt { get; set; } = "";
    public string[] SupportedBy { get; set; } = Array.Empty<string>();
    public string[] ContradictedBy { get; set; } = Array.Empty<string>();
    public string[] AssertedBy { get; set; } = Array.Empty<string>();
}

public class SeedEssentialFact
{
    public string Text { get; set; } = "";
    public string? ClaimSlug { get; set; }
}

public class SeedTerminologyNote
{
    public string Term { get; set; } = "";
    public string Note { get; set; } = "";
}

public class SeedTimelineEvent
{
    public DateOnly OccurredOn { get; set; }
    public string OccurredPrecision { get; set; } = "Day";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Agreed | Contested | Trigger | Now.</summary>
    public string Marker { get; set; } = "Agreed";
    public string? WhatWasKnownThen { get; set; }
    public string? TextAlternative { get; set; }
}

public class SeedDevelopment
{
    public DateTime OccurredAt { get; set; }
    public string Category { get; set; } = "Legislative";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    /// <summary>Required on every development.</summary>
    public string WhyItMatters { get; set; } = "";
    /// <summary>Required — which clause of the inclusion rule let this in.</summary>
    public string InclusionReason { get; set; } = "";
    public string EvidenceStatus { get; set; } = "Confirmed";
    /// <summary>Slug of the story room this opens into, if any.</summary>
    public string? StorySlug { get; set; }
}

/// <summary>
/// One funding item and where it actually sits on the five-rung ladder (PRD 05, design 1s).
///
/// <see cref="Stages"/> carries only the rungs that have an amount. The seeder builds all
/// five rows regardless, because "empty stages render as visible empty" has to be a
/// property of the data rather than something the UI remembers to do.
/// </summary>
public class SeedMoneyItem
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>GovernmentOutlay | ModeledEconomicEffect | Estimate.</summary>
    public string Kind { get; set; } = "GovernmentOutlay";
    public string Jurisdiction { get; set; } = "Federal";
    public string? SourceProgramName { get; set; }
    public string? CategoryKey { get; set; }

    /// <summary>Null when the figure has not been published — a real and common case.</summary>
    public decimal? AmountUsd { get; set; }
    public decimal? AmountMinUsd { get; set; }
    public decimal? AmountMaxUsd { get; set; }

    public int FiscalYearStart { get; set; }
    /// <summary>Equal to <see cref="FiscalYearStart"/> for a single-year item.</summary>
    public int FiscalYearEnd { get; set; }

    public bool IsRecurring { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>Required. Design 1s puts it in an inverse panel, not a tooltip.</summary>
    public string WhatThisDoesNotMean { get; set; } = "";
    public string? DecidesNext { get; set; }
    public string? EstimateMethod { get; set; }
    public string[] Exclusions { get; set; } = Array.Empty<string>();

    /// <summary>Stage name to amount, for the rungs actually reached.</summary>
    public Dictionary<string, decimal> Stages { get; set; } = new();

    /// <summary>Stage name to the reason it does not apply here. Design 1s: a stage that
    /// does not apply says so, rather than looking merely empty.</summary>
    public Dictionary<string, string> NotApplicable { get; set; } = new();

    public List<SeedMoneyBreakdown> Breakdown { get; set; } = new();
    public List<SeedMoneyComparison> Comparisons { get; set; } = new();

    /// <summary>Source key for the figure.</summary>
    public string? SourceKey { get; set; }
}

public class SeedMoneyBreakdown
{
    public string Label { get; set; } = "";
    public decimal AmountUsd { get; set; }
    public string? Note { get; set; }
}

/// <summary>A comparison offered, or explicitly refused with its reason (design 1s).</summary>
public class SeedMoneyComparison
{
    public string Text { get; set; } = "";
    public bool Accepted { get; set; } = true;
    public string? RejectionReason { get; set; }
}

public class SeedRoomBase
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Dek { get; set; } = "";
    public string? ContentNote { get; set; }
    public string? Locality { get; set; }
    public string Sensitivity { get; set; } = "Standard";
    /// <summary>Concept slugs this room references.</summary>
    public string[] Concepts { get; set; } = Array.Empty<string>();
    /// <summary>Claim slugs this room cites beyond its essential facts.</summary>
    public string[] Claims { get; set; } = Array.Empty<string>();
}

public class SeedThemeRoom : SeedRoomBase
{
    public string[] AlternateTitles { get; set; } = Array.Empty<string>();
    public string[] MatchTerms { get; set; } = Array.Empty<string>();
    public string ScopeStatement { get; set; } = "";
    public string[] InclusionRules { get; set; } = Array.Empty<string>();
    public string[] ExclusionRules { get; set; } = Array.Empty<string>();
    public string CurrentStatusSentence { get; set; } = "";
    public string TopUnresolvedQuestion { get; set; } = "";
    public string WatchNext { get; set; } = "";
    public string MonitoringCadence { get; set; } = "Weekly";
    public string? FreshnessOwner { get; set; }
    /// <summary>The honest denominator behind "we logged N and judged M".</summary>
    public int ArticlesConsideredCount { get; set; }
    public int DevelopmentWindowDays { get; set; } = 34;
    public List<SeedMoneyItem> MoneyItems { get; set; } = new();
    public List<SeedEssentialFact> EssentialFacts { get; set; } = new();
    public List<SeedTerminologyNote> TerminologyNotes { get; set; } = new();
    public List<SeedTimelineEvent> Timeline { get; set; } = new();
    public List<SeedDevelopment> Developments { get; set; } = new();
    /// <summary>Actor slugs, tiered for this room.</summary>
    public string[] Actors { get; set; } = Array.Empty<string>();
}

public class SeedStoryRoom : SeedRoomBase
{
    public string StoryType { get; set; } = "Legislative";
    public DateTime EventTime { get; set; }
    public int EstimatedMinutes { get; set; } = 3;
    public string HowItWorksIntro { get; set; } = "";
    public List<SeedStoryDimension> WhyItMatters { get; set; } = new();
    public List<SeedStakeholder> Stakeholders { get; set; } = new();
    public List<SeedNextStep> NextSteps { get; set; } = new();
    /// <summary>Claim slugs forming the "what happened" spine, in order.</summary>
    public string[] EssentialFactClaims { get; set; } = Array.Empty<string>();
}

public class SeedStoryDimension
{
    /// <summary>Legal · Institutional · Financial · Human · Immediate · Longer term.</summary>
    public string Dimension { get; set; } = "";
    public string Text { get; set; } = "";
    public string? ClaimSlug { get; set; }
}

public class SeedStakeholder
{
    public string Group { get; set; } = "";
    public string ImpactSummary { get; set; } = "";
    public double Confidence { get; set; } = 0.5;
}

public class SeedNextStep
{
    public string Description { get; set; } = "";
    /// <summary>"Confirmed if:" — required.</summary>
    public string VerificationCondition { get; set; } = "";
    public string? ActorSlug { get; set; }
    public string? ExpectedTiming { get; set; }
}
