using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The deterministic half of the drafting pipeline (PRD 08 phase R7).
///
/// This runs over every briefing and bill in the window on every pass, with no LLM call, so
/// it is the filter that decides what the expensive half ever sees. It has to be exactly as
/// boring and predictable as it looks.
/// </summary>
public class RoomCandidateMatchingTests
{
    [Theory]
    [InlineData("The House passed a spending bill", "spending", true)]
    [InlineData("Spending is up", "spending", true)]
    [InlineData("The bill passed.", "bill", true)]
    public void MatchesWholeWords(string text, string term, bool expected)
    {
        RoomCandidateService.ContainsTerm(text, term).Should().Be(expected);
    }

    [Theory]
    // The whole reason this is not String.Contains. "aid" inside "said" and "cut" inside
    // "executive" would put most of the news into a budget room.
    [InlineData("He said nothing", "aid")]
    [InlineData("An executive order", "cut")]
    [InlineData("The spendthrift governor", "spend")]
    [InlineData("Reappropriation is different", "appropriation")]
    public void DoesNotMatchInsideAnotherWord(string text, string term)
    {
        RoomCandidateService.ContainsTerm(text, term).Should().BeFalse();
    }

    [Fact]
    public void MatchesMultiWordTermsAsPhrases()
    {
        RoomCandidateService.ContainsTerm(
            "Congress passed a continuing resolution today", "continuing resolution")
            .Should().BeTrue();

        RoomCandidateService.ContainsTerm(
            "The resolution continuing into next week", "continuing resolution")
            .Should().BeFalse();
    }

    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        RoomCandidateService.ContainsTerm("APPROPRIATIONS bill", "appropriations").Should().BeTrue();
    }

    [Fact]
    public void AnEmptyTermNeverMatches()
    {
        // A blank entry in MatchTerms would otherwise match every document ever ingested.
        RoomCandidateService.ContainsTerm("anything at all", "").Should().BeFalse();
        RoomCandidateService.ContainsTerm("anything at all", "   ").Should().BeFalse();
    }

    [Fact]
    public void MatchedTerms_ReportsEveryDistinctHit()
    {
        var terms = new[] { "shutdown", "appropriations", "medicare", "appropriations" };

        var hits = RoomCandidateService.MatchedTerms(
            "A shutdown looms as appropriations stall", terms);

        hits.Should().BeEquivalentTo(new[] { "shutdown", "appropriations" });
    }

    [Fact]
    public void CandidateSlug_IsPrefixedByTheRoomAndStaysInsideTheColumn()
    {
        // Two rooms can legitimately match the same briefing, and Room.Slug is unique, so
        // an unprefixed slug would make the second room's candidate silently fail to insert.
        var a = RoomCandidateService.CandidateSlug("federal-appropriations", "some-briefing");
        var b = RoomCandidateService.CandidateSlug("defense-policy", "some-briefing");

        a.Should().NotBe(b);
        a.Should().StartWith("federal-appropriations-");

        var long_ = RoomCandidateService.CandidateSlug(new string('a', 100), new string('b', 100));
        long_.Length.Should().BeLessThanOrEqualTo(160);
    }
}

/// <summary>
/// Passage verification — the one thing standing between this pipeline and a fabricated
/// quotation. A paraphrase presented as a verbatim span looks exactly like the good case,
/// so nothing downstream can catch it.
/// </summary>
public class ClaimPassageVerificationTests
{
    private const string Source =
        "The House of Representatives voted to pass a defense appropriations bill totaling "
      + "$1.15 trillion, one of the largest military spending packages in U.S. history.";

    [Fact]
    public void AVerbatimPassageIsAccepted()
    {
        ClaimExtractionService.PassageAppearsIn(
            Source, "voted to pass a defense appropriations bill totaling $1.15 trillion")
            .Should().BeTrue();
    }

    [Fact]
    public void LineWrappingAndCurlyQuotesAreForgiven()
    {
        // Neither changes what was said, and both happen on the way through a model.
        ClaimExtractionService.PassageAppearsIn(
            Source, "defense appropriations bill\n  totaling  $1.15 trillion")
            .Should().BeTrue();

        ClaimExtractionService.PassageAppearsIn(
            "The chair called it a “clean” continuing resolution for the agencies",
            "called it a \"clean\" continuing resolution")
            .Should().BeTrue();
    }

    [Fact]
    public void ASmoothedParaphraseIsRejected()
    {
        // Same meaning, different words. This is the failure the check exists for.
        ClaimExtractionService.PassageAppearsIn(
            Source, "The House approved a $1.15 trillion defense appropriations bill")
            .Should().BeFalse();
    }

    [Fact]
    public void ADroppedWordIsRejected()
    {
        ClaimExtractionService.PassageAppearsIn(
            Source, "voted to pass a appropriations bill totaling $1.15 trillion")
            .Should().BeFalse();
    }

    [Fact]
    public void AVeryShortPassageIsRejectedEvenWhenPresent()
    {
        // "the bill" appears in every document ever written about Congress. A span that
        // short is not evidence that the claim came from this source.
        ClaimExtractionService.PassageAppearsIn(Source, "the House").Should().BeFalse();
    }

    [Fact]
    public void AnEmptyPassageIsRejected()
    {
        ClaimExtractionService.PassageAppearsIn(Source, "").Should().BeFalse();
        ClaimExtractionService.PassageAppearsIn(Source, "   ").Should().BeFalse();
    }

    [Fact]
    public void NothingDraftedByAModelIsEverConfirmed()
    {
        // Confirmed means a primary document settles it. The pipeline holds a briefing, so
        // the ceiling applies even when the passage checks out.
        ClaimExtractionService.Cap(Civic.API.Models.Rooms.ClaimStatus.Confirmed)
            .Should().Be(Civic.API.Models.Rooms.ClaimStatus.StronglySupported);

        // Everything else passes through untouched — the cap is a ceiling, not a flattener.
        foreach (var status in Enum.GetValues<Civic.API.Models.Rooms.ClaimStatus>())
        {
            if (status == Civic.API.Models.Rooms.ClaimStatus.Confirmed) continue;
            ClaimExtractionService.Cap(status).Should().Be(status);
        }
    }
}
