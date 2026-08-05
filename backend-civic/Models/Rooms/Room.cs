using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// Room lifecycle. Merges the PRD 01 §TR-2 theme states with the PRD 04 §10.4 editorial
/// review workflow — they are the same axis and keeping two enums would let a room be
/// "Published" and "Editorial review" at once.
/// </summary>
public enum RoomStatus
{
    /// <summary>Detected by the candidate pass, nobody has looked at it.</summary>
    Candidate,
    Drafting,
    Draft,
    InReview,
    Published,
    /// <summary>Published but no longer changing often; still checked on cadence.</summary>
    Monitoring,
    Dormant,
    Archived,
    /// <summary>A dependent object has been flagged unreviewed for over 24h. Read paths
    /// still serve it; the admin queue shouts about it.</summary>
    CorrectionRequired,
}

/// <summary>Editorial sensitivity, driving the PRD 07 §8.2 escalation path.</summary>
public enum SensitivityLevel
{
    Standard,
    /// <summary>Casualty figures, criminal accusations, attribution — senior review.</summary>
    Elevated,
    /// <summary>Not served publicly without a named trust-and-safety sign-off.</summary>
    Restricted,
}

/// <summary>How often an active room is checked (PRD 01 §TR-2 monitoring cadence).</summary>
public enum MonitoringCadence
{
    Daily,
    Weekly,
    Monthly,
    Paused,
}

/// <summary>
/// The development / story-type vocabulary from PRD 01 §6.3. One enum serves both the
/// "Latest" category filter and the Story Room type, because the PRD lists exactly the
/// same set for both.
/// </summary>
public enum RoomTopicCategory
{
    Military,
    Legislative,
    Court,
    ExecutiveAction,
    Diplomatic,
    Economic,
    Election,
    Investigation,
    Humanitarian,
    Regulatory,
    PublicHealth,
    Bill,
}

/// <summary>
/// A room — either a long-lived Theme Room or an atomic Story Room.
///
/// EF table-per-hierarchy: one physical Rooms table with a "Kind" discriminator. This is
/// the only inheritance in the codebase, and it earns the exception: revisions, changelog,
/// per-user state, following, section progress and publish gates apply IDENTICALLY to both
/// kinds. One table means RoomRevision.RoomId, UserRoomState.RoomId and ChangeLogEntry.RoomId
/// are real foreign keys to one target. The alternatives — a polymorphic RoomType+RoomId on
/// the most-written code path, or two parallel copies of the revision machinery — are worse.
/// </summary>
public abstract class Room
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(300)]
    public string Title { get; set; } = "";

    /// <summary>Neutral subtitle. "Neutral" is a publish gate, not a suggestion.</summary>
    [MaxLength(1000)]
    public string Dek { get; set; } = "";

    public RoomStatus Status { get; set; } = RoomStatus.Draft;

    public SensitivityLevel Sensitivity { get; set; } = SensitivityLevel.Standard;

    /// <summary>Rendered in --state above the body when the material is distressing.</summary>
    [MaxLength(500)]
    public string? ContentNote { get; set; }

    /// <summary>2-letter state code; null = national. Same read-wall as briefings.</summary>
    [MaxLength(2)]
    public string? Locality { get; set; }

    /// <summary>
    /// Monotonic from 1. ONLY RoomRevisionService.CommitAsync may increment this — every
    /// other writer goes through it so no edit can slip past the changelog.
    /// </summary>
    public int Revision { get; set; } = 1;

    /// <summary>Last change that met the meaningful-change bar. Drives "reviewed daily"
    /// copy and the follower notification, never a plain edit timestamp.</summary>
    public DateTime? LastMeaningfulUpdateAt { get; set; }

    public DateTime? LastReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    /// <summary>Per-field provenance, jsonb. Design 1y renders a 3px accent rule on any
    /// field still lacking a VerifiedAt.</summary>
    public List<FieldProvenance> Provenance { get; set; } = new();

    // --- LLM drafting bookkeeping ------------------------------------------------------
    // These land in this migration even though the drafting service is phase R7, so that
    // R7 is pure service code with no schema change. They mirror Bill.AttemptCount /
    // Bill.LastError, whose failure semantics RoomDraftService copies exactly.

    [MaxLength(60)]
    public string? DraftModelId { get; set; }

    public int DraftPromptVersion { get; set; }

    public int DraftAttemptCount { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    public DateTime? DraftedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A long-lived hub for an evolving public issue (PRD 01). Contains Story Rooms plus
/// persistent knowledge, actors, bills, money, claims and predictions — all by reference.
/// </summary>
public class ThemeRoom : Room
{
    /// <summary>Alternate and disputed names. PRD 01 §13: naming an evolving conflict can
    /// itself imply a contested classification, so alternates are first-class.</summary>
    public string[] AlternateTitles { get; set; } = Array.Empty<string>();

    /// <summary>Keywords the deterministic candidate pass matches incoming news, bills and
    /// briefings against. No LLM involved — this is a plain, testable, free filter.</summary>
    public string[] MatchTerms { get; set; } = Array.Empty<string>();

    [MaxLength(2000)]
    public string ScopeStatement { get; set; } = "";

    /// <summary>The seven-part inclusion rule design 1g prints verbatim in the sidebar.</summary>
    public string[] InclusionRules { get; set; } = Array.Empty<string>();
    public string[] ExclusionRules { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The room's most important element (design 1a). Describes a STATE, not an event, and
    /// is edited in place — never appended to.
    /// </summary>
    [MaxLength(1000)]
    public string CurrentStatusSentence { get; set; } = "";

    [MaxLength(500)]
    public string TopUnresolvedQuestion { get; set; } = "";

    [MaxLength(500)]
    public string WatchNext { get; set; } = "";

    /// <summary>
    /// The three front-door facts. Each points at a Claim, so its status line renders from
    /// data and a correction reaches the front door without anyone editing this row.
    /// </summary>
    public List<EssentialFact> EssentialFacts { get; set; } = new();

    /// <summary>Contested terminology and why we chose the word we chose (PRD 07 §3.5).</summary>
    public List<TerminologyNote> TerminologyNotes { get; set; } = new();

    public MonitoringCadence MonitoringCadence { get; set; } = MonitoringCadence.Weekly;

    [MaxLength(120)]
    public string? FreshnessOwner { get; set; }

    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveTo { get; set; }

    /// <summary>
    /// How many candidate articles this room has considered, and over what window.
    ///
    /// Design 1g prints "We logged 260 articles and judged eight of them to have changed
    /// something." That number is incremented by the candidate pass and stored, because a
    /// number invented at render time is not a disclosure, it is decoration.
    /// </summary>
    public int ArticlesConsideredCount { get; set; }

    public int DevelopmentWindowDays { get; set; } = 34;
}

/// <summary>One of the three essential facts on the front door.</summary>
public class EssentialFact
{
    /// <summary>The statement as displayed. Kept alongside ClaimId so the front door can
    /// phrase a fact for its context; the STATUS still comes from the claim.</summary>
    [Required, MaxLength(500)]
    public string Text { get; set; } = "";

    public Guid? ClaimId { get; set; }

    public int Ordinal { get; set; }
}

/// <summary>A contested term and the note explaining our usage (PRD 07 §3.5).</summary>
public class TerminologyNote
{
    [Required, MaxLength(80)]
    public string Term { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Note { get; set; } = "";
}

/// <summary>
/// One development, rendered as the nine-part sequence from PRD 02 §4.
///
/// The nine parts are real columns rather than a payload blob: the shapes are identical
/// across story types, publish gates validate field presence server-side, and — decisively —
/// field-level provenance needs a field to be an addressable named thing. Only the
/// genuinely per-type tail lives in <see cref="TypePayloadJson"/>.
/// </summary>
public class StoryRoom : Room
{
    public RoomTopicCategory StoryType { get; set; } = RoomTopicCategory.Legislative;

    public DateTime EventTime { get; set; }

    public int EstimatedMinutes { get; set; } = 3;

    /// <summary>"How it works" — the institutional mechanics section.</summary>
    [MaxLength(4000)]
    public string HowItWorksIntro { get; set; } = "";

    /// <summary>The six named dimensions of PRD 02 §5.4 / design 1o. Filling all six is a
    /// content requirement; a genuinely empty dimension says so rather than being padded.</summary>
    public List<StoryDimension> WhyItMatters { get; set; } = new();

    /// <summary>Who is affected, WITH an explicit confidence column — including the
    /// low-confidence rows noting that members of a group hold different views.</summary>
    public List<StakeholderImpact> Stakeholders { get; set; } = new();

    /// <summary>"What happens next": each outcome carries a "Confirmed if:" criterion.</summary>
    public List<NextStep> NextSteps { get; set; } = new();

    /// <summary>
    /// Story-type-specific fields — Bill (committee, cosponsors, recorded votes), Court
    /// (question presented, holding, dissent), Economic (metric, current, prior, revisions).
    /// Genuinely disjoint per type and never queried, so a new story type costs no migration.
    /// Shapes are modelled in Models/Rooms/StoryTypePayloads.cs.
    /// </summary>
    public string TypePayloadJson { get; set; } = "{}";

    public int TypePayloadVersion { get; set; } = 1;

    // Provenance — the real content this story was cut from.
    public Guid? SourceBillId { get; set; }
    public Guid? SourceNewsItemId { get; set; }
    public Guid? SourceBriefingId { get; set; }
}

/// <summary>One cell of the 2x3 "Why it matters" grid.</summary>
public class StoryDimension
{
    /// <summary>Legal · Institutional · Financial · Human · Immediate · Longer term.</summary>
    [Required, MaxLength(40)]
    public string Dimension { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Text { get; set; } = "";

    public Guid? ClaimId { get; set; }
}

/// <summary>An affected group, and how confident we are about the effect.</summary>
public class StakeholderImpact
{
    [Required, MaxLength(200)]
    public string Group { get; set; } = "";

    [Required, MaxLength(1000)]
    public string ImpactSummary { get; set; } = "";

    /// <summary>0..1. Low values are shown, not hidden — design 1p keeps a low-confidence
    /// row explicitly noting that members of the group disagree among themselves.</summary>
    public double Confidence { get; set; } = 0.5;
}

/// <summary>A possible next outcome with an objective confirmation criterion.</summary>
public class NextStep
{
    [Required, MaxLength(500)]
    public string Description { get; set; } = "";

    /// <summary>"Confirmed if:" — required, because an outcome nobody can check is not a
    /// prediction, it is a vibe.</summary>
    [Required, MaxLength(500)]
    public string VerificationCondition { get; set; } = "";

    public Guid? ActorId { get; set; }

    [MaxLength(120)]
    public string? ExpectedTiming { get; set; }

    /// <summary>Set when this outcome has been promoted to a forecastable Prediction.</summary>
    public Guid? PredictionId { get; set; }
}
