using Civic.API.Models.Daily;
using Civic.API.Services.Daily;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The daily-game scoring math. Pure functions, so this covers the formulas directly —
/// the acceptance criteria in each spec's Verification section land here.
/// </summary>
public class DailyScoringTests
{
    // ------------------------------------------------------------ Crowd Call

    private static CrowdCallPayload CrowdCall(params double[] trueRates) =>
        new(trueRates.Select(r => new CrowdCallRound(
            "prompt", "answer", "explanation", CrowdSource.CivicUsers,
            "Civersify players", null, null, 100, r)).ToList());

    [Fact]
    public void CrowdCall_PerfectGuesses_Score100()
    {
        var payload = CrowdCall(0.62, 0.41, 0.88);
        var response = new CrowdCallResponse(new List<double> { 0.62, 0.41, 0.88 });

        var (total, rounds) = DailyScoring.ScoreCrowdCall(payload, response);

        total.Should().Be(100);
        rounds.Should().OnlyContain(r => r.Band == Bands.Hit);
    }

    [Fact]
    public void CrowdCall_FiftyPointError_ScoresZeroNotNegative()
    {
        var payload = CrowdCall(0.90);
        var response = new CrowdCallResponse(new List<double> { 0.40 });

        var (total, _) = DailyScoring.ScoreCrowdCall(payload, response);

        total.Should().Be(0);
    }

    [Fact]
    public void CrowdCall_HugeError_ClampsAtZero()
    {
        var payload = CrowdCall(1.0);
        var response = new CrowdCallResponse(new List<double> { 0.0 });

        DailyScoring.ScoreCrowdCall(payload, response).Total.Should().Be(0);
    }

    [Theory]
    [InlineData(0.60, 0.55, Bands.Hit)]    // 5 points off
    [InlineData(0.60, 0.40, Bands.Near)]   // 20 points off
    [InlineData(0.60, 0.20, Bands.Miss)]   // 40 points off
    public void CrowdCall_BandsFollowErrorSize(double truth, double guess, string expected)
    {
        var (_, rounds) = DailyScoring.ScoreCrowdCall(
            CrowdCall(truth), new CrowdCallResponse(new List<double> { guess }));

        rounds[0].Band.Should().Be(expected);
    }

    [Fact]
    public void CrowdCall_CountsRoundsWhereTheCountryWasUnderestimated()
    {
        // Guessing BELOW the true correct-rate means expecting more people to get it
        // wrong — i.e. overestimating how divided/uninformed the country is.
        var payload = CrowdCall(0.80, 0.80, 0.30);
        var response = new CrowdCallResponse(new List<double> { 0.50, 0.90, 0.10 });

        DailyScoring.CountOverestimatedDivision(payload, response).Should().Be(2);
    }

    [Fact]
    public void CrowdCall_MissingGuessesAreTreatedAsZeroNotAnException()
    {
        var payload = CrowdCall(0.5, 0.5);
        var response = new CrowdCallResponse(new List<double> { 0.5 });

        var act = () => DailyScoring.ScoreCrowdCall(payload, response);

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------- Priced In

    [Fact]
    public void PricedIn_ExactFirstGuess_ScoresFull()
    {
        DailyScoring.ScorePricedIn(112_400_000_000, 112_400_000_000, 1).Should().Be(100);
    }

    [Fact]
    public void PricedIn_RatioErrorNotAbsoluteError()
    {
        // Same $10B absolute error, wildly different ratios — the small item must score worse.
        var onSmall = DailyScoring.ScorePricedIn(12_000_000_000, 22_000_000_000, 1);
        var onLarge = DailyScoring.ScorePricedIn(900_000_000_000, 910_000_000_000, 1);

        onLarge.Should().BeGreaterThan(onSmall);
    }

    [Fact]
    public void PricedIn_ExtraGuessesTakeATenPercentHaircutEach()
    {
        var first = DailyScoring.ScorePricedIn(1000, 1000, 1);
        var third = DailyScoring.ScorePricedIn(1000, 1000, 3);

        first.Should().Be(100);
        third.Should().Be(80);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PricedIn_NonPositiveGuess_ScoresZeroWithoutThrowingOnTheLog(double guess)
    {
        var act = () => DailyScoring.ScorePricedIn(1000, guess, 1);

        act.Should().NotThrow();
        DailyScoring.ScorePricedIn(1000, guess, 1).Should().Be(0);
    }

    [Fact]
    public void PricedIn_ClosenessIsAlwaysAtLeastOne()
    {
        DailyScoring.Closeness(1000, 500).Should().BeApproximately(2, 0.001);
        DailyScoring.Closeness(1000, 2000).Should().BeApproximately(2, 0.001);
    }

    // -------------------------------------------------------------- Place It

    private static PlaceItPayload PlaceIt(params int[] trueBuckets) =>
        new(Guid.NewGuid(), "title", "summary", "InCommittee",
            trueBuckets.Select((b, i) => new PlaceItAxis(
                $"axis-{i}", $"Axis {i}", "low", "high", b, "rationale", null)).ToList(),
            MaxRounds: 3);

    [Fact]
    public void PlaceIt_AllExactFirstRound_Scores100()
    {
        var (total, axes) = DailyScoring.ScorePlaceIt(PlaceIt(1, 3, 4), new[] { 1, 3, 4 }, 1);

        total.Should().Be(100);
        axes.Should().OnlyContain(a => a.Band == Bands.Hit);
    }

    [Fact]
    public void PlaceIt_AdjacentBucketIsGenerouslyCredited()
    {
        // The truth is a synthesis, not ground truth — "one notch off" is a legitimate
        // reading and scores 70, not 0.
        var (_, axes) = DailyScoring.ScorePlaceIt(PlaceIt(2), new[] { 3 }, 1);

        axes[0].Score.Should().Be(70);
        axes[0].Band.Should().Be(Bands.Near);
    }

    [Fact]
    public void PlaceIt_ExtraRoundsTakeAFifteenPercentHaircutEach()
    {
        var oneRound = DailyScoring.ScorePlaceIt(PlaceIt(2, 2, 2), new[] { 2, 2, 2 }, 1).Total;
        var threeRounds = DailyScoring.ScorePlaceIt(PlaceIt(2, 2, 2), new[] { 2, 2, 2 }, 3).Total;

        oneRound.Should().Be(100);
        threeRounds.Should().Be(70);
    }

    [Fact]
    public void PlaceIt_HintsPointTowardTheTruthWithoutRevealingIt()
    {
        var hints = DailyScoring.PlaceItHints(PlaceIt(4, 0, 2), new[] { 1, 3, 2 });

        hints.Should().Equal("higher", "lower", "exact");
    }

    [Theory]
    [InlineData(-1.0, 0)]
    [InlineData(-0.61, 0)]
    [InlineData(-0.5, 1)]
    [InlineData(0.0, 2)]
    [InlineData(0.2, 2)]
    [InlineData(0.5, 3)]
    [InlineData(0.61, 4)]
    [InlineData(1.0, 4)]
    public void PlaceIt_BucketingRoundTripsAcrossTheCutPoints(double score, int expected)
    {
        DailyScoring.BucketAxisScore(score).Should().Be(expected);
    }

    // ---------------------------------------------------------- Time Machine

    private static readonly string[] Order = { "a", "b", "c", "d", "e" };

    [Fact]
    public void TimeMachine_PerfectOrder_Scores100()
    {
        var (score, concordant, pairs) = DailyScoring.ScoreTimeMachineSort(Order, Order);

        score.Should().Be(100);
        concordant.Should().Be(10);
        pairs.Should().Be(10);
    }

    [Fact]
    public void TimeMachine_FullyReversedOrder_ScoresZero()
    {
        var reversed = Order.Reverse().ToArray();

        DailyScoring.ScoreTimeMachineSort(Order, reversed).Score.Should().Be(0);
    }

    [Fact]
    public void TimeMachine_OneAdjacentSwap_Scores90()
    {
        // Nearly-right orderings are rewarded rather than collapsing to all-or-nothing.
        var almost = new[] { "b", "a", "c", "d", "e" };

        DailyScoring.ScoreTimeMachineSort(Order, almost).Score.Should().Be(90);
    }

    [Fact]
    public void TimeMachine_SlotsMarkExactPositionMatches()
    {
        var guess = new[] { "a", "c", "b", "d", "e" };

        var slots = DailyScoring.TimeMachineSlots(Order, guess);

        slots.Select(s => s.Band).Should()
            .Equal(Bands.Hit, Bands.Miss, Bands.Miss, Bands.Hit, Bands.Hit);
    }

    [Theory]
    [InlineData("d", "d", 100)]
    [InlineData("d", "a", 0)]
    [InlineData("d", null, 0)]
    public void TimeMachine_OddOneOutIsAllOrNothing(string current, string? pick, int expected)
    {
        DailyScoring.ScoreTimeMachineOddOneOut(current, pick).Should().Be(expected);
    }

    // ----------------------------------------------------------- Whose Value

    private static WhoseValuePayload WhoseValue(params string[] correctKeys) =>
        new(correctKeys.Select(k => new WhoseValueRound(
            "argument", "bill", Guid.NewGuid(),
            new List<WhoseValueChoice> { new(k, "Name", "low", "high") }, k)).ToList());

    [Fact]
    public void WhoseValue_ScoresAsShareCorrect()
    {
        var payload = WhoseValue("authority", "risk", "speech", "community", "expertise");
        var response = new WhoseValueResponse(
            new List<string> { "authority", "risk", "wrong", "community", "wrong" });

        var (total, rounds) = DailyScoring.ScoreWhoseValue(payload, response);

        total.Should().Be(60);
        rounds.Count(r => r.Band == Bands.Hit).Should().Be(3);
    }

    [Fact]
    public void WhoseValue_NoPicks_ScoresZeroRatherThanThrowing()
    {
        var payload = WhoseValue("authority", "risk");

        var act = () => DailyScoring.ScoreWhoseValue(payload, new WhoseValueResponse(new List<string>()));

        act.Should().NotThrow();
        DailyScoring.ScoreWhoseValue(payload, new WhoseValueResponse(new List<string>())).Total.Should().Be(0);
    }
}
