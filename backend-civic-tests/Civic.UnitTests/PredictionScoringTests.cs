using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Proper scoring (PRD 06 §7). The whole reason for asking a probability instead of a
/// yes/no is that the scoring rule punishes unjustified confidence — so these tests are
/// really about that property, not about arithmetic.
/// </summary>
public class PredictionScoringTests
{
    [Theory]
    [InlineData(100, PredictionOutcome.Yes, 0.0)]
    [InlineData(0, PredictionOutcome.No, 0.0)]
    [InlineData(0, PredictionOutcome.Yes, 1.0)]
    [InlineData(100, PredictionOutcome.No, 1.0)]
    [InlineData(50, PredictionOutcome.Yes, 0.25)]
    [InlineData(50, PredictionOutcome.No, 0.25)]
    public void Brier_ScoresTheObviousCases(int p, PredictionOutcome outcome, double expected)
    {
        PredictionScoring.Brier(p, outcome).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Brier_PunishesConfidentWrongnessMoreThanHedgedWrongness()
    {
        // The property the whole feature rests on. If this ever inverted, the optimal play
        // would become "always say 100%", and the numbers would stop meaning anything.
        var confidentAndWrong = PredictionScoring.Brier(95, PredictionOutcome.No)!.Value;
        var hedgedAndWrong = PredictionScoring.Brier(60, PredictionOutcome.No)!.Value;

        confidentAndWrong.Should().BeGreaterThan(hedgedAndWrong);
    }

    [Fact]
    public void Brier_RewardsConfidenceWhenItIsJustified()
    {
        // The other half: hedging is not free either, or nobody would ever commit.
        var confidentAndRight = PredictionScoring.Brier(95, PredictionOutcome.Yes)!.Value;
        var hedgedAndRight = PredictionScoring.Brier(60, PredictionOutcome.Yes)!.Value;

        confidentAndRight.Should().BeLessThan(hedgedAndRight);
    }

    [Fact]
    public void ACoinFlipScoresTheSameWhicheverWayItLands()
    {
        PredictionScoring.Brier(50, PredictionOutcome.Yes)
            .Should().Be(PredictionScoring.Brier(50, PredictionOutcome.No));
    }

    [Fact]
    public void CancelledAndUnresolved_AreNotScored()
    {
        // A cancelled question is excluded from calibration rather than counted as a miss.
        // The forecaster was not wrong; the question stopped applying.
        PredictionScoring.Brier(80, PredictionOutcome.Cancelled).Should().BeNull();
        PredictionScoring.Brier(80, PredictionOutcome.Unresolved).Should().BeNull();
    }

    [Fact]
    public void Brier_ClampsOutOfRangeInput()
    {
        PredictionScoring.Brier(150, PredictionOutcome.Yes).Should().Be(0);
        PredictionScoring.Brier(-20, PredictionOutcome.No).Should().Be(0);
    }

    // ---------------------------------------------------------------- calibration

    private static IEnumerable<(int, PredictionOutcome)> Band(
        int probability, int yes, int no)
    {
        for (var i = 0; i < yes; i++) yield return (probability, PredictionOutcome.Yes);
        for (var i = 0; i < no; i++) yield return (probability, PredictionOutcome.No);
    }

    [Fact]
    public void SmallBands_AreSuppressedRatherThanShownNoisy()
    {
        // Telling someone they are badly calibrated on the strength of two forecasts would
        // be worse than telling them nothing.
        var bands = PredictionScoring.CalibrationBands(Band(85, yes: 1, no: 1));

        bands.Should().BeEmpty();
    }

    [Fact]
    public void AWellCalibratedBand_IsNotFlaggedOverconfident()
    {
        // Said 80%, happened 80% of the time.
        var bands = PredictionScoring.CalibrationBands(Band(80, yes: 8, no: 2));

        bands.Should().ContainSingle();
        bands[0].ActualRate.Should().BeApproximately(0.8, 1e-9);
        bands[0].Overconfident.Should().BeFalse();
    }

    [Fact]
    public void AnOverconfidentBand_IsFlagged()
    {
        // Said 90%, happened half the time.
        var bands = PredictionScoring.CalibrationBands(Band(90, yes: 5, no: 5));

        bands.Should().ContainSingle();
        bands[0].Overconfident.Should().BeTrue();
    }

    [Fact]
    public void OverconfidenceBelowTheMidpoint_MeansPredictingNoTooStrongly()
    {
        // Said 10% — i.e. "almost certainly not" — and it happened half the time. That is
        // the same failure as saying 90% and being wrong, so the comparison has to flip
        // below 0.5 or half the chart would silently never flag anything.
        var bands = PredictionScoring.CalibrationBands(Band(10, yes: 5, no: 5));

        bands.Should().ContainSingle();
        bands[0].Overconfident.Should().BeTrue();
    }

    [Fact]
    public void TheTopBandIncludesAHundred()
    {
        // A forecast of exactly 100 has to land somewhere rather than falling off the end.
        var bands = PredictionScoring.CalibrationBands(Band(100, yes: 6, no: 0));

        bands.Should().ContainSingle();
        bands[0].UpperBound.Should().Be(100);
        bands[0].Count.Should().Be(6);
    }

    [Fact]
    public void CancelledForecasts_DoNotEnterTheBands()
    {
        var mixed = Band(80, yes: 8, no: 2)
            .Concat(Enumerable.Repeat((80, PredictionOutcome.Cancelled), 50));

        var bands = PredictionScoring.CalibrationBands(mixed);

        bands.Should().ContainSingle();
        bands[0].Count.Should().Be(10, "the 50 cancelled questions are excluded entirely");
    }

    [Fact]
    public void Summarize_SaysNothingWhenThereIsNothingToSay()
    {
        PredictionScoring.Summarize(Array.Empty<CalibrationBand>()).Should().BeNull();
    }

    [Fact]
    public void MeanBrier_IgnoresUnscoredForecasts()
    {
        PredictionScoring.MeanBrier(new double?[] { 0.1, null, 0.3 })
            .Should().BeApproximately(0.2, 1e-9);
        PredictionScoring.MeanBrier(new double?[] { null, null }).Should().BeNull();
    }
}
