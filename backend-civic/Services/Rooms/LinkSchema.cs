using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>
/// The allow-set of legal (From, Relation, To) triples for <see cref="ObjectLink"/>.
///
/// A polymorphic edge table buys correction fan-out at the cost of compile-time shape
/// checking. This file buys the checking back: it is pure, it is one screen long, and
/// <c>LinkSchemaTests</c> asserts every relationship named in PRD 04 §5 appears here.
/// If you are adding an edge the graph does not yet allow, add it HERE first — do not
/// weaken <see cref="ObjectLinkService"/>.
/// </summary>
public static class LinkSchema
{
    /// <summary>One legal edge shape.</summary>
    public readonly record struct Triple(ObjectType From, LinkRelation Relation, ObjectType To);

    private static readonly HashSet<Triple> AllowedSet = new(BuildAllowed());

    /// <summary>Every legal edge shape, for tests and for the admin integrity report.</summary>
    public static IReadOnlyCollection<Triple> Allowed => AllowedSet;

    public static bool IsAllowed(ObjectType from, LinkRelation relation, ObjectType to)
        => AllowedSet.Contains(new Triple(from, relation, to));

    public static string Describe(ObjectType from, LinkRelation relation, ObjectType to)
        => $"{from} -{relation}-> {to}";

    private static IEnumerable<Triple> BuildAllowed()
    {
        Triple T(ObjectType f, LinkRelation r, ObjectType t) => new(f, r, t);

        return new[]
        {
            // --- PRD 04 §5, verbatim ---------------------------------------------------
            // "Theme CONTAINS Story" — both are Room rows (TPH), so the shape is Room->Room.
            T(ObjectType.Room, LinkRelation.Contains, ObjectType.Room),
            T(ObjectType.Room, LinkRelation.DescribesEvent, ObjectType.TimelineEvent),
            T(ObjectType.Room, LinkRelation.References, ObjectType.Concept),
            T(ObjectType.Actor, LinkRelation.ParticipatesIn, ObjectType.TimelineEvent),
            T(ObjectType.Actor, LinkRelation.Sponsors, ObjectType.Bill),
            T(ObjectType.Bill, LinkRelation.RelatesTo, ObjectType.Room),
            T(ObjectType.MoneyItem, LinkRelation.Funds, ObjectType.Bill),
            T(ObjectType.Claim, LinkRelation.AssertedBy, ObjectType.Actor),
            T(ObjectType.Claim, LinkRelation.SupportedBy, ObjectType.SourceRef),
            T(ObjectType.Claim, LinkRelation.ContradictedBy, ObjectType.SourceRef),
            T(ObjectType.ConversationCluster, LinkRelation.RespondsTo, ObjectType.TimelineEvent),
            T(ObjectType.Prediction, LinkRelation.About, ObjectType.Room),
            T(ObjectType.Interaction, LinkRelation.Teaches, ObjectType.Concept),
            T(ObjectType.Interaction, LinkRelation.Uses, ObjectType.Claim),

            // --- Room composition ------------------------------------------------------
            // The three headline facts on the front door (design 1a), ordered by Ordinal.
            T(ObjectType.Room, LinkRelation.EssentialFact, ObjectType.Claim),
            T(ObjectType.Room, LinkRelation.Contains, ObjectType.Prediction),
            T(ObjectType.Room, LinkRelation.Contains, ObjectType.Interaction),
            // MoneyItem.RoomId is nullable so one funding item can appear in several rooms.
            T(ObjectType.Room, LinkRelation.Contains, ObjectType.MoneyItem),
            T(ObjectType.Room, LinkRelation.References, ObjectType.Claim),
            T(ObjectType.Room, LinkRelation.References, ObjectType.Actor),
            T(ObjectType.Room, LinkRelation.References, ObjectType.SourceRef),
            T(ObjectType.Room, LinkRelation.RelatesTo, ObjectType.Bill),
            // Provenance back into the content Civersify already had.
            T(ObjectType.Room, LinkRelation.References, ObjectType.Briefing),
            T(ObjectType.Room, LinkRelation.References, ObjectType.NewsItem),
            T(ObjectType.Room, LinkRelation.References, ObjectType.Provision),
            // A prior story that set the pattern for this one (PRD 01 §6.4 "historical precedents").
            T(ObjectType.Room, LinkRelation.Precedent, ObjectType.Room),

            // --- Developments ----------------------------------------------------------
            // Development.RoomId is a real FK; these are its outward citations.
            T(ObjectType.Development, LinkRelation.References, ObjectType.Claim),
            T(ObjectType.Development, LinkRelation.References, ObjectType.SourceRef),
            T(ObjectType.Development, LinkRelation.References, ObjectType.Actor),
            T(ObjectType.Development, LinkRelation.DescribesEvent, ObjectType.TimelineEvent),

            // --- Claims ----------------------------------------------------------------
            T(ObjectType.Claim, LinkRelation.References, ObjectType.Concept),
            T(ObjectType.Claim, LinkRelation.SupersededBy, ObjectType.Claim),
            T(ObjectType.Claim, LinkRelation.SameAs, ObjectType.Claim),

            // --- Actors ----------------------------------------------------------------
            T(ObjectType.Actor, LinkRelation.ParticipatesIn, ObjectType.Room),
            T(ObjectType.Actor, LinkRelation.SameAs, ObjectType.Actor),
            // "Constrained by" as a graph edge, in addition to the prose field on Actor.
            T(ObjectType.Actor, LinkRelation.DependsOn, ObjectType.Actor),

            // --- Money -----------------------------------------------------------------
            T(ObjectType.MoneyItem, LinkRelation.Funds, ObjectType.Actor),
            T(ObjectType.MoneyItem, LinkRelation.SupportedBy, ObjectType.SourceRef),
            T(ObjectType.MoneyItem, LinkRelation.RelatesTo, ObjectType.MoneyItem),

            // --- Timeline & knowledge --------------------------------------------------
            T(ObjectType.TimelineEvent, LinkRelation.SupportedBy, ObjectType.SourceRef),
            T(ObjectType.Concept, LinkRelation.SameAs, ObjectType.Concept),
            T(ObjectType.Concept, LinkRelation.References, ObjectType.Concept),

            // --- Predictions & interactions --------------------------------------------
            T(ObjectType.Prediction, LinkRelation.About, ObjectType.Claim),
            T(ObjectType.Prediction, LinkRelation.About, ObjectType.Bill),
            T(ObjectType.Prediction, LinkRelation.SupportedBy, ObjectType.SourceRef),
            T(ObjectType.Interaction, LinkRelation.About, ObjectType.Prediction),
            T(ObjectType.Interaction, LinkRelation.Uses, ObjectType.TimelineEvent),
            T(ObjectType.Interaction, LinkRelation.Uses, ObjectType.MoneyItem),

            // --- Sources ---------------------------------------------------------------
            // Circular sourcing: this report's only basis is that report (PRD 07 §5).
            T(ObjectType.SourceRef, LinkRelation.DependsOn, ObjectType.SourceRef),
            T(ObjectType.SourceRef, LinkRelation.SupersededBy, ObjectType.SourceRef),

            // --- Conversation Map (reserved; the feature is out of scope, PRD 08 Gate 3) -
            T(ObjectType.ConversationCluster, LinkRelation.Uses, ObjectType.Claim),
            T(ObjectType.Room, LinkRelation.Contains, ObjectType.ConversationCluster),
        };
    }
}

/// <summary>Thrown when a caller tries to attach an edge shape <see cref="LinkSchema"/> forbids.</summary>
public class InvalidLinkException : Exception
{
    public InvalidLinkException(ObjectType from, LinkRelation relation, ObjectType to)
        : base($"Illegal graph edge {LinkSchema.Describe(from, relation, to)}. "
             + "Add the triple to LinkSchema if it is legitimate; do not bypass the check.")
    {
    }
}
