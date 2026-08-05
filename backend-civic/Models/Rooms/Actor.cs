using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>Actor subtypes from PRD 04 §4.5, plus the two the designs need for `1i`.</summary>
public enum ActorType
{
    ElectedOfficial,
    GovernmentBody,
    Court,
    Agency,
    Country,
    InternationalOrganization,
    Company,
    AdvocacyGroup,
    Community,
    Military,
    Committee,
}

/// <summary>
/// How much leverage an actor has over a named decision (design 1i's three tiers).
/// </summary>
public enum ActorTier
{
    /// <summary>Can make the decision happen or not happen.</summary>
    Decides,
    /// <summary>Can change the terms but not the outcome.</summary>
    Shapes,
    /// <summary>Affected by it, with limited ability to move it.</summary>
    Constrained,
}

/// <summary>
/// A person or organization. First-class so "who is involved" is a graph question rather
/// than a string comparison.
///
/// Note that <c>Bill.Sponsor</c> and <c>Briefing.Institution</c> stay denormalized strings.
/// Actors are attached opportunistically via ObjectLink as they are created, and the string
/// remains the display fallback — backfilling every historical sponsor is a separate project
/// with much less value than it looks.
/// </summary>
public class Actor
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>Abbreviations, former names, translations, disputed terminology.</summary>
    public string[] AlternateNames { get; set; } = Array.Empty<string>();

    public ActorType ActorType { get; set; } = ActorType.GovernmentBody;

    /// <summary>What they can actually do — powers, not intentions.</summary>
    [MaxLength(1000)]
    public string ActualPower { get; set; } = "";

    /// <summary>What limits them. Design 1i gives this its own row on the actor card.</summary>
    [MaxLength(1000)]
    public string ConstrainedBy { get; set; } = "";

    /// <summary>
    /// What they PUBLICLY SAY they want.
    ///
    /// Design 1i is emphatic: "always a quote or filing, with date — never inferred motive."
    /// A non-empty value here without <see cref="StatedWantsSourceRefId"/> is a publish-gate
    /// failure, because an unsourced statement of what someone wants is us guessing at motive.
    /// </summary>
    [MaxLength(1000)]
    public string? StatedWants { get; set; }

    public Guid? StatedWantsSourceRefId { get; set; }

    public DateTime? StatedWantsAsOf { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public List<FieldProvenance> Provenance { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An actor's role and leverage WITHIN one room, relative to one named decision.
///
/// A real table rather than an ObjectLink because it carries three room-scoped prose fields
/// and drives the re-sortable tiering in design 1i.
///
/// <see cref="DecisionKey"/> is nullable on purpose. It answers the handoff's open question
/// about whether leverage-based sorting survives at scale: a room can ship with only the
/// default tiering and add per-decision sorts later with no migration.
/// </summary>
public class ActorRoomRole
{
    public Guid Id { get; set; }

    public Guid ActorId { get; set; }
    public Actor? Actor { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    /// <summary>The decision this tiering is relative to. Null = the room's default sort.</summary>
    [MaxLength(120)]
    public string? DecisionKey { get; set; }

    public ActorTier Tier { get; set; } = ActorTier.Shapes;

    /// <summary>One line on what leverage they have here. Rendered on the tier card.</summary>
    [MaxLength(500)]
    public string LeverageStatement { get; set; } = "";

    /// <summary>Their role in this room specifically, not in general.</summary>
    [MaxLength(500)]
    public string RoleHere { get; set; } = "";

    public int Ordinal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
