using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Structural validation of the hand-authored pilot room.
///
/// The seed file is the one place where the required-field rules are asserted by a human
/// typing rather than by a form, so the invariants the designs call "required fields" get
/// checked here — cheaply, with no database, on every build.
/// </summary>
public class RoomSeedJsonTests
{
    private static RoomSeedFile Pilot()
    {
        var file = SeedService.LoadJson<RoomSeedFile>("Seed.rooms.federal-appropriations.json");
        file.Should().NotBeNull("the pilot room must be an embedded resource");
        return file!;
    }

    [Fact]
    public void PilotRoom_Deserializes()
    {
        var file = Pilot();

        file.Theme.Should().NotBeNull();
        file.Theme!.Slug.Should().Be("federal-appropriations");
        file.Claims.Should().NotBeEmpty();
        file.Actors.Should().NotBeEmpty();
        file.Concepts.Should().NotBeEmpty();
        file.Sources.Should().NotBeEmpty();
    }

    [Fact]
    public void EverySlugReference_ResolvesInsideTheFile()
    {
        // A slug typo in a 400-line hand-authored file is otherwise invisible: the seeder
        // skips what it cannot resolve, and the room renders with a section quietly missing.
        var file = Pilot();

        var sourceKeys = file.Sources.Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claimSlugs = file.Claims.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actorSlugs = file.Actors.Select(a => a.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conceptSlugs = file.Concepts.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var storySlugs = file.Stories.Select(s => s.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // BeSubsetOf rather than OnlyContain throughout: most of these lists are legitimately
        // empty (a claim with no contradicting evidence is the common case), and
        // OnlyContain treats an empty collection as a failure.
        foreach (var claim in file.Claims)
        {
            claim.SupportedBy.Should().BeSubsetOf(sourceKeys);
            claim.ContradictedBy.Should().BeSubsetOf(sourceKeys);
            claim.AssertedBy.Should().BeSubsetOf(actorSlugs);
        }

        foreach (var actor in file.Actors.Where(a => a.StatedWantsSourceKey is not null))
        {
            sourceKeys.Should().Contain(actor.StatedWantsSourceKey!);
        }

        foreach (var concept in file.Concepts.Where(c => c.ConfusionPairSlug is not null))
        {
            conceptSlugs.Should().Contain(concept.ConfusionPairSlug!);
        }

        var theme = file.Theme!;
        theme.Concepts.Should().BeSubsetOf(conceptSlugs);
        theme.Claims.Should().BeSubsetOf(claimSlugs);
        theme.Actors.Should().BeSubsetOf(actorSlugs);
        theme.EssentialFacts.Where(f => f.ClaimSlug is not null).Select(f => f.ClaimSlug!)
            .Should().BeSubsetOf(claimSlugs);
        theme.Developments.Where(d => d.StorySlug is not null).Select(d => d.StorySlug!)
            .Should().BeSubsetOf(storySlugs);

        foreach (var story in file.Stories)
        {
            story.Concepts.Should().BeSubsetOf(conceptSlugs);
            story.EssentialFactClaims.Should().BeSubsetOf(claimSlugs);
            story.WhyItMatters.Where(d => d.ClaimSlug is not null).Select(d => d.ClaimSlug!)
                .Should().BeSubsetOf(claimSlugs);
        }
    }

    [Fact]
    public void EveryClaim_SaysWhatWouldSettleIt()
    {
        // Design 1n makes this a required field. A claim nobody can say how to settle is
        // not a claim, it is a mood.
        Pilot().Claims.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.WhatWouldSettleIt));
    }

    [Fact]
    public void EveryClaim_UsesADefinedStatusAndKind()
    {
        foreach (var claim in Pilot().Claims)
        {
            Enum.TryParse<ClaimStatus>(claim.Status, out _).Should().BeTrue(
                "'{0}' is not one of the eight statuses", claim.Status);
            Enum.TryParse<ClaimKind>(claim.Kind, out _).Should().BeTrue(
                "'{0}' is not a valid claim kind", claim.Kind);
        }
    }

    [Fact]
    public void AnInterpretationIsNotLabelledConfirmed()
    {
        // The one that actually catches authoring drift: it is tempting to mark a
        // well-argued interpretation "Confirmed". Only factual claims can be.
        foreach (var claim in Pilot().Claims.Where(c => c.Kind != "Factual"))
        {
            claim.Status.Should().NotBe("Confirmed",
                "'{0}' is a {1}, and only a factual claim can be Confirmed", claim.Slug, claim.Kind);
        }
    }

    [Fact]
    public void EveryActorWithStatedWants_CitesASource()
    {
        // Design 1i: "always a quote or filing, with date — never inferred motive."
        // Without this, "what they want" quietly becomes us guessing.
        foreach (var actor in Pilot().Actors.Where(a => !string.IsNullOrWhiteSpace(a.StatedWants)))
        {
            actor.StatedWantsSourceKey.Should().NotBeNullOrWhiteSpace(
                "actor '{0}' states what it wants but cites nothing", actor.Slug);
        }
    }

    [Fact]
    public void EveryDevelopment_HasWhyItMattersAndAnInclusionReason()
    {
        // Both are [Required] on the entity; asserting them here means a bad seed fails
        // the build rather than a database constraint at startup.
        foreach (var dev in Pilot().Theme!.Developments)
        {
            dev.WhyItMatters.Should().NotBeNullOrWhiteSpace();
            dev.InclusionReason.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void AFreshlySeededRoom_HonestlyReportsZeroConsideredAndZeroLogged()
    {
        // The pilot ships with no developments ON PURPOSE. A Development means an editor
        // judged that something changed, and nothing has yet — the candidate pass has not
        // run, so ArticlesConsideredCount is 0.
        //
        // This matters because design 1g prints "we logged N articles and judged M of them
        // to have changed something". Pre-filling that with invented developments would
        // make the room's own disclosure a lie on its first render. 0 of 0 is the truthful
        // initial state, and this test stops anyone padding it.
        var theme = Pilot().Theme!;

        theme.Developments.Should().BeEmpty();
        theme.ArticlesConsideredCount.Should().Be(0,
            "the denominator is incremented by the candidate pass at runtime, never authored");
    }

    [Fact]
    public void TheInclusionRule_MatchesTheSevenMeaningfulChangeTypes()
    {
        // Design 1g prints the inclusion rule beside the Latest list. It is only honest if
        // it actually corresponds to what the code treats as meaningful.
        Pilot().Theme!.InclusionRules.Should().HaveCount(
            MeaningfulChange.MeaningfulTypes.Count,
            "the printed rule and the code's meaningful-change taxonomy must not drift apart");
    }

    [Fact]
    public void TimelineEvents_CarryATextAlternative()
    {
        // Accessibility publish gate: a horizontal track of squares means nothing to a
        // screen reader.
        Pilot().Theme!.Timeline.Should()
            .OnlyContain(t => !string.IsNullOrWhiteSpace(t.TextAlternative));
    }

    [Fact]
    public void TimelineEvents_SayWhatWasKnownAtTheTime()
    {
        // The payoff of the Timeline Builder's second pass depends on this field being
        // populated, so the seed has to model it from the start.
        Pilot().Theme!.Timeline.Should()
            .OnlyContain(t => !string.IsNullOrWhiteSpace(t.WhatWasKnownThen));
    }

    [Fact]
    public void TheStoryRoom_FillsAllSixWhyItMattersDimensions()
    {
        // PRD 02 §5.4 / design 1o: filling all six is a content requirement. If a dimension
        // is genuinely empty it should say so, not be dropped.
        foreach (var story in Pilot().Stories)
        {
            story.WhyItMatters.Select(d => d.Dimension).Should().BeEquivalentTo(new[]
            {
                "Legal", "Institutional", "Financial", "Human", "Immediate", "Longer term",
            });
        }
    }

    [Fact]
    public void EveryNextStep_StatesHowItWouldBeConfirmed()
    {
        foreach (var story in Pilot().Stories)
        {
            story.NextSteps.Should()
                .OnlyContain(n => !string.IsNullOrWhiteSpace(n.VerificationCondition));
        }
    }

    [Fact]
    public void SlugsAreUnique()
    {
        var file = Pilot();
        file.Claims.Select(c => c.Slug).Should().OnlyHaveUniqueItems();
        file.Actors.Select(a => a.Slug).Should().OnlyHaveUniqueItems();
        file.Concepts.Select(c => c.Slug).Should().OnlyHaveUniqueItems();
        file.Sources.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        file.Stories.Select(s => s.Slug).Should().OnlyHaveUniqueItems();
    }

    // ---------------------------------------------------------------- contested terms

    [Fact]
    public void ContestedTerms_LoadAndCoverThePrd07Examples()
    {
        var catalog = new CivicCatalog();

        catalog.ContestedTerms.Should().NotBeEmpty();
        var terms = catalog.ContestedTerms.Select(t => t.Term).ToList();
        terms.Should().Contain(new[]
        {
            "war", "genocide", "terrorist", "riot", "recession", "constitutional crisis",
        });
    }

    [Fact]
    public void ContestedTermsIn_MatchesWholeWordsOnly()
    {
        // A gate that fires on "toward" because it contains "war" gets clicked through,
        // and then it is not a gate.
        var catalog = new CivicCatalog();

        catalog.ContestedTermsIn("The delegation moved toward an agreement.")
            .Should().BeEmpty();
        catalog.ContestedTermsIn("Executed contracts were reviewed.")
            .Should().NotContain(t => t.Term == "cut");

        catalog.ContestedTermsIn("The committee cut the request by half.")
            .Should().Contain(t => t.Term == "cut");
        catalog.ContestedTermsIn("The government is spending the money now.")
            .Should().Contain(t => t.Term == "spending");
    }

    [Fact]
    public void ContestedTermsIn_HandlesNullAndEmpty()
    {
        var catalog = new CivicCatalog();
        catalog.ContestedTermsIn(null).Should().BeEmpty();
        catalog.ContestedTermsIn("   ").Should().BeEmpty();
    }

    [Fact]
    public void ThePilotRoom_DeclaresANoteForEveryContestedTermItUses()
    {
        // The room's own copy has to pass the gate it will be held to. "spending" and "cut"
        // are exactly the words a budget room cannot avoid, so it declares notes for them.
        var catalog = new CivicCatalog();
        var theme = Pilot().Theme!;
        var declared = theme.TerminologyNotes
            .Select(n => n.Term).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var used = catalog.ContestedTermsIn(theme.CurrentStatusSentence)
            .Concat(catalog.ContestedTermsIn(theme.Dek))
            .Where(t => t.RequiresNote)
            .Select(t => t.Term)
            .Distinct();

        foreach (var term in used)
        {
            declared.Should().Contain(term,
                "the room's status sentence or dek uses '{0}', which requires a terminology note",
                term);
        }
    }
}
