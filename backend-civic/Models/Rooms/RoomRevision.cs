using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// What kind of change happened. The first seven members are the ONLY meaningful ones —
/// they are the handoff's definition of a change worth notifying about, verbatim. Everything
/// after them is real, logged, and deliberately not worth interrupting anyone for.
///
/// <see cref="Civic.API.Services.Rooms.MeaningfulChange"/> classifies these, and a unit test
/// walks every member so an unclassified addition fails the build rather than defaulting to
/// "minor" and quietly suppressing a notification that mattered.
/// </summary>
public enum ChangeType
{
    // --- meaningful: an official body acted, or the evidence itself moved ---------------
    /// <summary>A vote, ruling, order or filing.</summary>
    OfficialAction,
    VerifiedFactChanged,
    ClaimStatusMoved,
    MoneyStageAdvanced,
    NegotiationStatusChanged,
    PredictionResolved,
    /// <summary>Gets its own visual treatment and is NEVER folded into "updated".</summary>
    CorrectionIssued,

    // --- not meaningful: real edits, shown only in the full changelog --------------------
    /// <summary>New commentary about an old event. Explicitly not a development.</summary>
    CommentaryAdded,
    CopyEdit,
    /// <summary>Another source added to a fact that already had one.</summary>
    SourceAdded,
    TypoFix,
    FormattingChange,
    RelationshipAdded,
}

/// <summary>Whether a change interrupts the reader. Deliberately an enum, not a bool —
/// see <see cref="Civic.API.Services.Rooms.MeaningfulChange"/>.</summary>
public enum ChangeSignificance
{
    Meaningful,
    Minor,
}

/// <summary>The taxonomy of corrections from PRD 07 §13.1.</summary>
public enum CorrectionKind
{
    Typographical,
    Clarification,
    /// <summary>A material factual correction. Must be visible; a silent edit is not enough.</summary>
    Factual,
    SourceCorrection,
    Retraction,
    MaterialFraming,
}

/// <summary>
/// One committed edit to a room. Written only by RoomRevisionService.CommitAsync.
/// </summary>
public class RoomRevision
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    /// <summary>Matches <see cref="Room.Revision"/> at the moment of the commit.</summary>
    public int Revision { get; set; }

    /// <summary>True when any of this revision's entries met the meaningful bar.</summary>
    public bool IsMeaningful { get; set; }

    [MaxLength(500)]
    public string Summary { get; set; } = "";

    /// <summary>Publish gates cleared for this revision, with the names that cleared them.
    /// Stored per revision, so editing after a sign-off re-opens the gate.</summary>
    public List<GateApproval> GateApprovals { get; set; } = new();

    /// <summary>
    /// The full serialized room, written for MEANINGFUL revisions only.
    ///
    /// A few KB a handful of times per room per month, and it settles the open question of
    /// whether diff mode (design 1e) is in scope: the data ships now, so the diff renderer
    /// becomes a pure frontend decision later with no schema change.
    /// </summary>
    public string? SnapshotJson { get; set; }

    [Required, MaxLength(120)]
    public string CreatedBy { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A publish gate cleared by a named person, stored with the revision.</summary>
public class GateApproval
{
    [Required, MaxLength(40)]
    public string Gate { get; set; } = "";

    [Required, MaxLength(120)]
    public string ClearedBy { get; set; } = "";

    public DateTime ClearedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One typed line in a room's changelog.
///
/// Meaningful entries are what the delta ribbon counts and what notifies followers; minor
/// entries are counted honestly and shown only in the full changelog ("11 edits we did not
/// bother you with" — design 1d).
/// </summary>
public class ChangeLogEntry
{
    public Guid Id { get; set; }

    public Guid RoomRevisionId { get; set; }
    public RoomRevision? RoomRevision { get; set; }

    /// <summary>Denormalized from the revision so the delta is a single indexed range scan
    /// with no join — this is the hottest read on the whole feature.</summary>
    public Guid RoomId { get; set; }

    /// <summary>Denormalized for the same reason.</summary>
    public int RevisionNumber { get; set; }

    public ChangeType Type { get; set; }

    /// <summary>Persisted for indexing, but always COMPUTED by MeaningfulChange.Classify —
    /// never set by a caller, or the taxonomy stops meaning anything.</summary>
    public bool IsMeaningful { get; set; }

    [Required, MaxLength(300)]
    public string Headline { get; set; } = "";

    /// <summary>Why the reader should care. Required on meaningful entries by the gate.</summary>
    [MaxLength(500)]
    public string? WhyItMatters { get; set; }

    /// <summary>What changed, when it is a graph object.</summary>
    public ObjectType? ObjectType { get; set; }
    public Guid? ObjectId { get; set; }

    /// <summary>For a status move, design 1d renders the transition literally:
    /// old mark, word, arrow, new mark, word. These two carry the words.</summary>
    [MaxLength(500)]
    public string? FromValue { get; set; }

    [MaxLength(500)]
    public string? ToValue { get; set; }

    /// <summary>Set only when <see cref="Type"/> is <see cref="ChangeType.CorrectionIssued"/>.</summary>
    public CorrectionKind? CorrectionKind { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
