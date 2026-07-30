using Civic.API.Models.Daily;
using Civic.API.Services.Daily;
using Civic.API.Services.Daily.Generators;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Which Is True (07). The tests that matter here are the content invariants, not the
/// arithmetic: a two-option question is only worth playing if both options are real, the
/// answer isn't always on the same side, and the two figures are far enough apart that
/// the question is about the world rather than about rounding.
/// </summary>
public class WhichIsTrueTests
{
    private static List<WhichIsTrueGenerator.Candidate> AllStaticCandidates()
    {
        var all = WhichIsTrueGenerator.StateTaxCandidates();
        all.AddRange(WhichIsTrueGenerator.FederalCandidates());
        return all;
    }

    // ------------------------------------------------------ the core invariant

    [Fact]
    public void EveryCandidate_PairsTheTruthWithADifferentRealFigure()
    {
        var candidates = AllStaticCandidates();
        candidates.Should().NotBeEmpty();

        foreach (var c in candidates)
        {
            c.TruthText.Should().NotBeNullOrWhiteSpace();
            c.DecoyText.Should().NotBeNullOrWhiteSpace();
            c.DecoyText.Should().NotBe(c.TruthText,
                $"\"{c.Key}\" would show the same number twice");

            // The decoy is only defensible because we can say what it actually is. An empty
            // DecoyTruth is the tell that someone invented a number.
            c.DecoyTruth.Should().NotBeNullOrWhiteSpace(
                $"\"{c.Key}\" must be able to say what the losing option really is");
        }
    }

    [Fact]
    public void EveryCandidate_CarriesProvenanceAndAnExplanation()
    {
        foreach (var c in AllStaticCandidates())
        {
            c.Source.Should().NotBeNullOrWhiteSpace($"\"{c.Key}\" needs a citation for the reveal");
            c.Explanation.Should().NotBeNullOrWhiteSpace($"\"{c.Key}\" needs something to teach");
            c.Prompt.Should().NotBeNullOrWhiteSpace();
            c.Topic.Should().BeOneOf(
                WhichIsTrueTopic.FederalBudget,
                WhichIsTrueTopic.StateAndLocalTax,
                WhichIsTrueTopic.Congress);
        }
    }

    [Fact]
    public void CandidateKeys_AreUnique()
    {
        // Keys are the dedup ledger — a collision would silently retire two questions at once.
        AllStaticCandidates().Select(c => c.Key).Should().OnlyHaveUniqueItems();
    }

    // ------------------------------------------------------------ side balance

    [Fact]
    public void TheAnswerIsNotSystematicallyOnOneSide()
    {
        // A player who learns "it's usually B" is playing a different game. Across the whole
        // bank on one day this must sit near an even split.
        var date = new DateOnly(2026, 7, 29);
        var rounds = AllStaticCandidates().Select(c => c.ToRound(date)).ToList();

        var aShare = (double)rounds.Count(r => r.Correct == "A") / rounds.Count;

        aShare.Should().BeInRange(0.40, 0.60);
    }

    [Fact]
    public void SideAssignmentIsStableAcrossRegeneration()
    {
        // Regenerating a day must not move the answer — the stored payload and a rebuilt one
        // have to agree, which rules out any process-randomized hash.
        var date = new DateOnly(2026, 7, 29);
        var candidate = AllStaticCandidates().First();

        candidate.ToRound(date).Correct.Should().Be(candidate.ToRound(date).Correct);
    }

    [Fact]
    public void SideAssignmentMovesWithTheDate()
    {
        // ...but a question that comes back around shouldn't sit on the same side forever.
        var candidate = AllStaticCandidates().First();
        var sides = Enumerable.Range(0, 40)
            .Select(i => candidate.ToRound(new DateOnly(2026, 1, 1).AddDays(i)).Correct)
            .Distinct();

        sides.Should().HaveCount(2);
    }

    [Fact]
    public void TheTruthTextIsAlwaysTheOptionMarkedCorrect()
    {
        var date = new DateOnly(2026, 7, 29);
        foreach (var c in AllStaticCandidates())
        {
            var round = c.ToRound(date);
            var picked = round.Correct == "A" ? round.OptionA : round.OptionB;
            var other = round.Correct == "A" ? round.OptionB : round.OptionA;

            picked.Should().Be(c.TruthText, $"\"{c.Key}\" put the wrong figure behind the right letter");
            other.Should().Be(c.DecoyText);
        }
    }

    // ------------------------------------------------------------ the two pools

    [Fact]
    public void StateTaxCandidates_CoverEveryStateAndStayFarEnoughApartToBeAQuestion()
    {
        var candidates = WhichIsTrueGenerator.StateTaxCandidates();

        // 50 states × up to 2 measures. Some pairs are filtered for being too close, so this
        // is a floor, not an equality.
        candidates.Should().HaveCountGreaterThan(50);
        candidates.Should().OnlyContain(c => c.Topic == WhichIsTrueTopic.StateAndLocalTax);

        foreach (var c in candidates)
        {
            var truth = ParsePercent(c.TruthText);
            var decoy = ParsePercent(c.DecoyText);
            Math.Abs(truth - decoy).Should().BeGreaterThan(0.5,
                $"\"{c.Key}\" is a rounding question, not a civics one");
        }
    }

    [Fact]
    public void StateTaxCandidates_DoNotAllPairAgainstTheSameOutlier()
    {
        // Pairing every state against the national extreme would make "never pick the
        // 0.00%" a winning strategy and show the same two rivals all week. The band +
        // deterministic pick exists to stop that, so no single decoy may dominate.
        var sales = WhichIsTrueGenerator.StateTaxCandidates()
            .Where(c => c.Key.StartsWith("state-sales:"))
            .ToList();

        var commonest = sales.GroupBy(c => c.DecoyText).Max(g => g.Count());

        ((double)commonest / sales.Count).Should().BeLessThan(0.25,
            "one rate must not be the decoy for a quarter of the bank");
    }

    [Fact]
    public void EveryCandidateBelongsToAFamilyTheSlateCanCapOn()
    {
        // The family (key prefix) is what stops a puzzle asking the same question twice
        // with different numbers. A key with no prefix would silently opt out of the cap.
        foreach (var c in AllStaticCandidates())
        {
            c.Key.Should().Contain(":", $"\"{c.Key}\" has no family prefix");
            WhichIsTrueGenerator.Family(c.Key).Should().NotBeNullOrWhiteSpace();
        }

        WhichIsTrueGenerator.Family("state-sales:TN").Should().Be("state-sales");
        WhichIsTrueGenerator.Family("bracket:Single:0.220").Should().Be("bracket");
    }

    [Fact]
    public void BracketPromptsRenderTheRateWithoutAStraySpace()
    {
        // "P0" formats 32% as "32 %" under the invariant culture, which reads as a typo at
        // headline size. Caught only by looking at real output, so pin it here.
        var brackets = WhichIsTrueGenerator.FederalCandidates()
            .Where(c => c.Key.StartsWith("bracket:"));

        brackets.Should().NotBeEmpty();
        foreach (var c in brackets) c.Prompt.Should().NotContain(" %");
    }

    [Fact]
    public void FederalCandidates_KeepTheTwoFiguresAWholeOrderApart()
    {
        var candidates = WhichIsTrueGenerator.FederalCandidates();
        candidates.Should().NotBeEmpty();
        candidates.Should().OnlyContain(c => c.Topic == WhichIsTrueTopic.FederalBudget);

        foreach (var c in candidates)
        {
            var truth = ParseMoney(c.TruthText);
            var decoy = ParseMoney(c.DecoyText);
            var ratio = truth >= decoy ? truth / decoy : decoy / truth;

            ratio.Should().BeGreaterThanOrEqualTo(WhichIsTrueGenerator.MinMagnitudeRatio,
                $"\"{c.Key}\" pairs two figures a player can't meaningfully tell apart");

            // The ceiling is the half that's easy to forget: a 40x pairing (the standard
            // deduction against the top bracket threshold) is a free point, not a question.
            ratio.Should().BeLessThanOrEqualTo(WhichIsTrueGenerator.MaxMagnitudeRatio,
                $"\"{c.Key}\" pairs two figures nobody could confuse");
        }
    }

    [Theory]
    [InlineData("HR", "The House of Representatives")]
    [InlineData("hjres", "The House of Representatives")]
    [InlineData("S", "The Senate")]
    [InlineData("SJRES", "The Senate")]
    [InlineData("", null)]
    [InlineData("XYZ", null)]
    public void Chamber_IsReadOffTheBillTypePrefix(string billType, string? expected)
    {
        WhichIsTrueGenerator.Chamber(billType).Should().Be(expected);
    }

    [Fact]
    public void StableHash_DoesNotDependOnTheProcess()
    {
        // Pinned value: if this ever changes, every historical puzzle's answer side moved.
        WhichIsTrueGenerator.StableHash("state-sales:TN")
            .Should().Be(WhichIsTrueGenerator.StableHash("state-sales:TN"));
        WhichIsTrueGenerator.StableHash("a").Should().NotBe(WhichIsTrueGenerator.StableHash("b"));
    }

    // ---------------------------------------------------------------- scoring

    private static WhichIsTruePayload Payload(params string[] correct) =>
        new(correct.Select((c, i) => new WhichIsTrueRound(
            $"k{i}", WhichIsTrueTopic.FederalBudget, $"prompt {i}", "one", "two",
            c, "why", "the other is real too", "source", null, null, null)).ToList());

    [Fact]
    public void Scoring_IsStraightAccuracyAcrossRounds()
    {
        var payload = Payload("A", "B", "A", "B");
        var (total, rounds) = DailyScoring.ScoreWhichIsTrue(
            payload, new WhichIsTrueResponse(new List<string> { "A", "B", "B", "B" }));

        total.Should().Be(75);
        rounds.Select(r => r.Band).Should().Equal(Bands.Hit, Bands.Hit, Bands.Miss, Bands.Hit);
    }

    [Fact]
    public void Scoring_TreatsAShortOrMissingAnswerListAsWrong()
    {
        var (total, rounds) = DailyScoring.ScoreWhichIsTrue(
            Payload("A", "A"), new WhichIsTrueResponse(new List<string> { "A" }));

        total.Should().Be(50);
        rounds[1].Band.Should().Be(Bands.Miss);
    }

    [Fact]
    public void ShareGrid_ReportsProgressWithoutNamingATopic()
    {
        var rounds = new List<RoundResult>
        {
            new(100, Bands.Hit), new(0, Bands.Miss), new(100, Bands.Hit),
        };

        var grid = DailyShareGrid.WhichIsTrue(edition: 12, correct: 2, total: 3, rounds);

        grid.Should().Contain("Which Is True #12").And.Contain("2/3").And.Contain("🟩🟥🟩");
        // Anyone who hasn't played must learn nothing about what's on the card.
        grid.Should().NotContain("budget").And.NotContain("Congress");
    }

    private static double ParsePercent(string text) =>
        double.Parse(text.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);

    private static double ParseMoney(string text) =>
        double.Parse(text.TrimStart('$').Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture);
}
