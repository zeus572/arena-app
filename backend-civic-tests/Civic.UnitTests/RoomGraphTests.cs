using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Guards on the Topic Rooms knowledge graph (docs/Rooms Expansion, PRD 04 §5 and PRD 07 §6.1).
///
/// These are the invariants a polymorphic edge table cannot get from the type system, plus
/// the enum shapes that are persisted as strings and would silently re-label historical rows
/// if someone renamed a member.
/// </summary>
public class RoomGraphTests
{
    // ---------------------------------------------------------------- ClaimStatus

    [Fact]
    public void ClaimStatus_HasExactlyTheEightPrd07Statuses()
    {
        // PRD 07 §6.1 and design 1m. PRD 03 §6.4 lists a shorter seven-value set; PRD 07 is
        // the cross-cutting standard and wins. Adding a ninth status means adding a ninth
        // mark to the design's vocabulary, so this deliberately fails on any change.
        var names = Enum.GetNames<ClaimStatus>();

        names.Should().BeEquivalentTo(new[]
        {
            "Confirmed",
            "StronglySupported",
            "PlausibleButUnresolved",
            "Disputed",
            "Unsupported",
            "False",
            "Outdated",
            "Prediction",
        });
    }

    [Fact]
    public void ClaimStatus_RoundTripsThroughItsPersistedStringForm()
    {
        // Stored via HasConversion<string>(). A rename would orphan every existing row.
        foreach (var status in Enum.GetValues<ClaimStatus>())
        {
            Enum.Parse<ClaimStatus>(status.ToString()).Should().Be(status);
            status.ToString().Length.Should().BeLessThanOrEqualTo(30,
                "the column is MaxLength(30)");
        }
    }

    [Fact]
    public void ClaimKind_CarriesTheFourEpistemicLabels()
    {
        // PRD 07 §3.2, and the answer key for the Fact/Opinion/Interpretation/Prediction
        // interaction — which is why it lives on the claim rather than per-interaction.
        Enum.GetNames<ClaimKind>().Should().BeEquivalentTo(
            new[] { "Factual", "Interpretation", "Opinion", "Prediction" });
    }

    [Theory]
    [InlineData(ClaimStatus.False)]
    [InlineData(ClaimStatus.Unsupported)]
    public void ClaimStatus_RetainedStatusesExist(ClaimStatus status)
    {
        // Design 1m: False and Unsupported claims are RETAINED, never deleted — the ledger's
        // job is to record that the claim exists and what the evidence does about it.
        // If either member disappeared, the retention rule would have no way to be expressed.
        Enum.IsDefined(status).Should().BeTrue();
    }

    // ---------------------------------------------------------------- LinkSchema

    [Fact]
    public void LinkSchema_ContainsEveryRelationshipNamedInPrd04Section5()
    {
        // The illustrative relationship list from PRD 04 §5, one line per entry.
        var required = new (ObjectType From, LinkRelation Rel, ObjectType To)[]
        {
            (ObjectType.Room, LinkRelation.Contains, ObjectType.Room),                   // Theme CONTAINS Story
            (ObjectType.Room, LinkRelation.DescribesEvent, ObjectType.TimelineEvent),    // Story DESCRIBES Event
            (ObjectType.Room, LinkRelation.References, ObjectType.Concept),              // Story REFERENCES Knowledge Item
            (ObjectType.Actor, LinkRelation.ParticipatesIn, ObjectType.TimelineEvent),   // Actor PARTICIPATES_IN Event
            (ObjectType.Actor, LinkRelation.Sponsors, ObjectType.Bill),                  // Actor SPONSORS Bill
            (ObjectType.Bill, LinkRelation.RelatesTo, ObjectType.Room),                  // Bill RELATES_TO Theme
            (ObjectType.MoneyItem, LinkRelation.Funds, ObjectType.Bill),                 // Budget Item FUNDS Policy Item
            (ObjectType.Claim, LinkRelation.AssertedBy, ObjectType.Actor),               // Claim ASSERTED_BY Actor
            (ObjectType.Claim, LinkRelation.SupportedBy, ObjectType.SourceRef),          // Claim SUPPORTED_BY Source
            (ObjectType.Claim, LinkRelation.ContradictedBy, ObjectType.SourceRef),       // Claim CONTRADICTED_BY Source
            (ObjectType.ConversationCluster, LinkRelation.RespondsTo, ObjectType.TimelineEvent), // Reaction RESPONDS_TO Event
            (ObjectType.Prediction, LinkRelation.About, ObjectType.Room),                // Prediction ABOUT Theme
            (ObjectType.Interaction, LinkRelation.Teaches, ObjectType.Concept),          // Interaction TEACHES Knowledge Item
            (ObjectType.Interaction, LinkRelation.Uses, ObjectType.Claim),               // Interaction USES Claim
        };

        foreach (var (from, rel, to) in required)
        {
            LinkSchema.IsAllowed(from, rel, to).Should().BeTrue(
                "PRD 04 §5 names {0}", LinkSchema.Describe(from, rel, to));
        }
    }

    [Fact]
    public void LinkSchema_AllowsTheEssentialFactEdgeTheFrontDoorRendersFrom()
    {
        // Design 1a's three essential facts are claim REFERENCES, not copied prose — that is
        // what makes a status change on one of them show up on the front door for free.
        LinkSchema.IsAllowed(ObjectType.Room, LinkRelation.EssentialFact, ObjectType.Claim)
            .Should().BeTrue();
    }

    [Fact]
    public void LinkSchema_RejectsAnIllegalTriple()
    {
        // A source cannot assert a claim — only an actor can (PRD 07 §4: a direct statement
        // establishes what someone SAID, and a document is not a someone).
        LinkSchema.IsAllowed(ObjectType.Claim, LinkRelation.AssertedBy, ObjectType.SourceRef)
            .Should().BeFalse();
    }

    [Fact]
    public void LinkSchema_HasNoDuplicateTriples()
    {
        LinkSchema.Allowed.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void LinkSchema_ReferencesOnlyDefinedEnumMembers()
    {
        foreach (var t in LinkSchema.Allowed)
        {
            Enum.IsDefined(t.From).Should().BeTrue();
            Enum.IsDefined(t.Relation).Should().BeTrue();
            Enum.IsDefined(t.To).Should().BeTrue();
        }
    }

    [Fact]
    public void InvalidLinkException_NamesTheOffendingShape()
    {
        var ex = new InvalidLinkException(ObjectType.Claim, LinkRelation.Funds, ObjectType.Bill);
        ex.Message.Should().Contain("Claim -Funds-> Bill");
    }

    // ---------------------------------------------------------------- ObjectResolver

    [Fact]
    public void ObjectResolver_AccountsForEveryObjectType()
    {
        // The one real hazard of a polymorphic graph: adding an object type, forgetting to
        // hydrate it, and shipping blank rows with no compiler error. Every member must be
        // either resolvable today or explicitly parked with the phase that will handle it.
        var covered = ObjectResolver.Resolvable
            .Concat(ObjectResolver.NotYetResolvable)
            .ToHashSet();

        covered.Should().BeEquivalentTo(Enum.GetValues<ObjectType>());
    }

    [Fact]
    public void ObjectResolver_DoesNotClaimATypeIsBothResolvableAndNot()
    {
        ObjectResolver.Resolvable.Intersect(ObjectResolver.NotYetResolvable)
            .Should().BeEmpty();
    }

    // ---------------------------------------------------------------- Required fields

    [Fact]
    public void Claim_RequiresWhatWouldSettleIt()
    {
        // Design 1n calls this a required field, so it is [Required] on the model rather
        // than a UI hint. A claim nobody can say how to settle is not a claim.
        var prop = typeof(Claim).GetProperty(nameof(Claim.WhatWouldSettleIt))!;
        prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void SourceRef_HasNoTrustOrCredibilityField()
    {
        // PRD 07 §4 opens by requiring source TYPE be stored and displayed separately from
        // any trust assessment. The cheapest way to keep them separate is for the trust
        // field not to exist.
        var names = typeof(SourceRef).GetProperties().Select(p => p.Name).ToList();

        names.Should().NotContain(n =>
            n.Contains("Trust", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Credib", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Bias", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Reliab", StringComparison.OrdinalIgnoreCase));
    }
}
