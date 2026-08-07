using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// Every kind of thing that can sit at either end of an <see cref="ObjectLink"/>.
///
/// Persisted as a string, but members are still APPEND-only by convention so that
/// <see cref="Civic.API.Services.Rooms.LinkSchema"/>'s allow-set stays reviewable as a diff.
/// </summary>
public enum ObjectType
{
    Room,
    Claim,
    SourceRef,
    Actor,
    /// <summary>The existing <see cref="Concept"/> table, which doubles as the PRD 04 §4.4
    /// knowledge item. There is deliberately no second KnowledgeItem table.</summary>
    Concept,
    TimelineEvent,
    Development,
    Prediction,
    MoneyItem,
    Interaction,
    // Pre-existing Civersify objects the graph reaches into.
    Bill,
    NewsItem,
    Briefing,
    Provision,
    /// <summary>Reserved. The Conversation Map is out of scope (PRD 08 Gate 3) — this member
    /// exists so LinkSchema can name the relation without a later enum insert.</summary>
    ConversationCluster,
}

/// <summary>
/// The relationship vocabulary from PRD 04 §5. Direction is always From -> To.
/// </summary>
public enum LinkRelation
{
    /// <summary>Theme Room CONTAINS Story Room.</summary>
    Contains,
    /// <summary>Room -> Claim, for the three headline facts. Ordered by <see cref="ObjectLink.Ordinal"/>.</summary>
    EssentialFact,
    /// <summary>Room/Story -> Concept, Actor, anything cited in passing.</summary>
    References,
    DescribesEvent,
    ParticipatesIn,
    Sponsors,
    RelatesTo,
    Funds,
    AssertedBy,
    SupportedBy,
    ContradictedBy,
    RespondsTo,
    About,
    Teaches,
    /// <summary>Interaction USES Claim. Combined with Interaction.AnswerDependsOnClaimStatus,
    /// this is what makes a status change flag the interaction for revalidation.</summary>
    Uses,
    Precedent,
    DependsOn,
    SupersededBy,
    /// <summary>Two records are the same real-world object. Stands in for full entity
    /// resolution (PRD 04 §6.3), which is deferred.</summary>
    SameAs,
}

/// <summary>
/// The single edge table for the civic knowledge graph.
///
/// Why one polymorphic table instead of ~25 typed join tables: the product requirement is
/// correction fan-out — "which rooms, interactions and developments reference claim X?" —
/// and here that is one indexed scan on (ToType, ToId). With typed tables it is a UNION
/// over a dozen tables that has to be edited every time an object type is added, and a
/// missing arm produces no compiler error, only a correction that silently fails to
/// propagate. That is the one failure this feature cannot tolerate.
///
/// What the type system would have given us is restored by three cheap things:
///   - <see cref="Civic.API.Services.Rooms.LinkSchema"/> rejects illegal (From, Relation, To) triples.
///   - <see cref="Civic.API.Services.Rooms.ObjectResolver"/> hydrates by type, one query per type.
///   - GET /api/admin/rooms/integrity reports dangling edges.
///
/// Containment and cardinality-1 relations are NOT edges — they stay real foreign keys
/// (RoomRevision.RoomId, Development.RoomId, ClaimStatusHistory.ClaimId, and so on).
/// </summary>
public class ObjectLink
{
    public Guid Id { get; set; }

    public ObjectType FromType { get; set; }
    public Guid FromId { get; set; }

    public LinkRelation Relation { get; set; }

    public ObjectType ToType { get; set; }
    public Guid ToId { get; set; }

    /// <summary>Display order within a (From, Relation) group — essential-fact position,
    /// section position. Zero when order is not meaningful.</summary>
    public int Ordinal { get; set; }

    /// <summary>0..1. Always 1.0 for a human- or seed-attached edge; the model's own
    /// number when an extraction pass proposed it.</summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>The source that justifies the relationship itself (PRD 04 §5).</summary>
    public Guid? SourceRefId { get; set; }

    // Temporal validity (PRD 04 §6.4). Edges are retired by setting ValidTo, never deleted —
    // "person held office during a period", "bill was in committee before advancing".
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    public DateTime? ValidTo { get; set; }

    public ProvenanceOrigin ProposedBy { get; set; } = ProvenanceOrigin.Human;

    [MaxLength(120)]
    public string? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A (type, id) pair naming one object in the graph.</summary>
public readonly record struct ObjectRef(ObjectType Type, Guid Id);
