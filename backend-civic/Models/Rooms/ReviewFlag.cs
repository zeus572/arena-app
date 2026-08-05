using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>Why something needs a human to look at it.</summary>
public enum ReviewReason
{
    /// <summary>A claim this object depends on changed status.</summary>
    DependsOnChangedClaim,
    /// <summary>Evidence of comparable quality contradicts a claim rated above Disputed.</summary>
    ContradictionDetected,
    /// <summary>A source was retracted or removed (PRD 04 §14.3).</summary>
    SourceWithdrawn,
    UserReported,
    SensitivityEscalation,
    StaleContent,
    PredictionResolutionDue,
}

/// <summary>What the reviewer is being asked to do (design 1z's right-aligned action).</summary>
public enum ReviewAction
{
    /// <summary>The prose is now wrong and has to be rewritten.</summary>
    Rewrite,
    /// <summary>An interaction's answer key may no longer hold.</summary>
    Revalidate,
    /// <summary>Look and decide.</summary>
    Review,
    /// <summary>Nothing to do — recorded because it happened, not because it is actionable.</summary>
    Logged,
}

public enum ReviewResolution
{
    Pending,
    Revalidated,
    Rewritten,
    Retired,
    /// <summary>A human looked and decided no change was needed. Requires a note.</summary>
    Overridden,
}

/// <summary>
/// One thing a human has to look at.
///
/// A single table rather than a flag column on eight entities: the review queue is a
/// cross-cutting list, and per-entity columns would mean a UNION to build it and a schema
/// change every time a new object type can be flagged.
/// </summary>
public class ReviewFlag
{
    public Guid Id { get; set; }

    public ObjectType ObjectType { get; set; }
    public Guid ObjectId { get; set; }

    public ReviewReason Reason { get; set; }

    public ReviewAction Action { get; set; } = ReviewAction.Review;

    /// <summary>What caused the flag — usually the claim whose status moved.</summary>
    public ObjectType? TriggerObjectType { get; set; }
    public Guid? TriggerObjectId { get; set; }

    /// <summary>Why this object is now wrong, in the reviewer's words.</summary>
    [MaxLength(1000)]
    public string Detail { get; set; } = "";

    public ReviewResolution Resolution { get; set; } = ReviewResolution.Pending;

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(120)]
    public string? ResolvedBy { get; set; }

    [MaxLength(1000)]
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The nine blocking publish gates from design 1y.
///
/// Persisted as strings; renaming a member orphans the cleared-gate records that prove a
/// named person signed off on a specific revision, so treat these as append-only.
/// </summary>
public enum PublishGateKey
{
    /// <summary>Every essential fact traces to at least one source.</summary>
    ProvenanceComplete,
    /// <summary>No claim rated above Disputed while comparable evidence contradicts it.</summary>
    ClaimStatusConsistency,
    /// <summary>Two independent organizations, or one primary document.</summary>
    SourceDiversity,
    /// <summary>Amounts have a period and a stage; dates parse and are not in the future.</summary>
    NumbersAndDates,
    /// <summary>Contested terms carry a terminology note.</summary>
    TerminologyReview,
    /// <summary>The headline passes three neutrality criteria.</summary>
    HeadlineNeutrality,
    /// <summary>Timelines and charts have text alternatives.</summary>
    Accessibility,
    /// <summary>Sensitivity is set; graphic content is off by default.</summary>
    YouthSafety,
    /// <summary>Every interaction option has an explanation and its answer key resolves.</summary>
    InteractionAnswerValidation,
}

/// <summary>
/// The outcome of one gate for one revision.
///
/// Keyed by revision on purpose: editing a room after a gate was cleared re-opens it,
/// because the sign-off attested to text that no longer exists.
/// </summary>
public class PublishGateResult
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    /// <summary>The room revision this result attests to.</summary>
    public int RoomRevision { get; set; }

    public PublishGateKey Gate { get; set; }

    public bool Passed { get; set; }

    /// <summary>False only for advisory gates. All nine shipped gates block.</summary>
    public bool Blocking { get; set; } = true;

    [MaxLength(1000)]
    public string Detail { get; set; } = "";

    /// <summary>
    /// The person who cleared it. Stored with the revision — design 1y is explicit that
    /// gates are blocking and the names are kept.
    ///
    /// With one operator this will often be the same name three times over. That is the
    /// honest implementation: the value is the explicit, recorded, per-revision sign-off,
    /// not a pretence of separated duties.
    /// </summary>
    [MaxLength(120)]
    public string? ClearedBy { get; set; }

    public DateTime? ClearedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
