using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>One confidence band in a calibration read-out.</summary>
public record CalibrationBand(
    int LowerBound,
    int UpperBound,
    int Count,
    double MeanProbability,
    double ActualRate,
    bool Overconfident);

/// <summary>
/// Proper scoring for probability forecasts (PRD 06 §7).
///
/// Brier, not accuracy. PRD 06 forbids a rule that rewards unjustified certainty, and
/// accuracy does exactly that — it pays the same for "70%" and "100%" when the thing
/// happens, so the winning strategy is to always say 100%. Brier is (p - o)^2: it punishes
/// confident wrongness quadratically, which is the entire point of asking for a number
/// instead of a yes/no.
///
/// Pure and dependency-free.
/// </summary>
public static class PredictionScoring
{
    /// <summary>
    /// (p - o)^2, where p is the forecast as a fraction and o is 1 for Yes, 0 for No.
    /// Lower is better; 0 is perfect, 1 is maximally wrong, 0.25 is a coin flip.
    ///
    /// Returns null for Cancelled and Unresolved — a cancelled question is excluded from
    /// calibration entirely rather than scored as a miss, because the forecaster was not
    /// wrong, the question stopped applying.
    /// </summary>
    public static double? Brier(int probability, PredictionOutcome outcome)
    {
        if (outcome is PredictionOutcome.Unresolved or PredictionOutcome.Cancelled) return null;

        var p = Math.Clamp(probability, 0, 100) / 100.0;
        var o = outcome == PredictionOutcome.Yes ? 1.0 : 0.0;
        return (p - o) * (p - o);
    }

    /// <summary>Below this many resolved forecasts a band is noise; suppress it.</summary>
    public const int MinBandSize = 5;

    /// <summary>
    /// Accuracy by confidence band (design 1v's chart).
    ///
    /// Ten bands of ten points. A band is "overconfident" when the things people said would
    /// happen at 80% happened less than 80% of the time — those render in --state.
    ///
    /// Bands below <see cref="MinBandSize"/> are dropped rather than shown noisy: telling
    /// someone they are badly calibrated on the strength of two forecasts would be worse
    /// than telling them nothing.
    /// </summary>
    public static IReadOnlyList<CalibrationBand> CalibrationBands(
        IEnumerable<(int Probability, PredictionOutcome Outcome)> forecasts)
    {
        var scored = forecasts
            .Where(f => f.Outcome is PredictionOutcome.Yes or PredictionOutcome.No)
            .ToList();

        var bands = new List<CalibrationBand>();

        for (var lower = 0; lower < 100; lower += 10)
        {
            var upper = lower + 10;

            // The top band is closed on both ends so a forecast of exactly 100 lands
            // somewhere rather than falling off the end.
            var inBand = scored
                .Where(f => f.Probability >= lower &&
                            (upper == 100 ? f.Probability <= 100 : f.Probability < upper))
                .ToList();

            if (inBand.Count < MinBandSize) continue;

            var mean = inBand.Average(f => f.Probability) / 100.0;
            var actual = inBand.Count(f => f.Outcome == PredictionOutcome.Yes) / (double)inBand.Count;

            bands.Add(new CalibrationBand(
                lower, upper, inBand.Count, mean, actual,
                // Overconfident means claiming more certainty than the outcomes justify.
                // Below the midpoint that means predicting "no" too strongly, so the
                // comparison flips.
                Overconfident: mean >= 0.5 ? actual < mean : actual > mean));
        }

        return bands;
    }

    /// <summary>
    /// One plain-language line about a person's calibration, or null when there is not
    /// enough to say anything honest.
    /// </summary>
    public static string? Summarize(IReadOnlyList<CalibrationBand> bands)
    {
        if (bands.Count == 0) return null;

        var over = bands.Count(b => b.Overconfident);

        if (over == 0) return "Your confidence has matched your accuracy so far.";
        if (over == bands.Count) return "You have been consistently more confident than your accuracy supports.";
        return $"You are well calibrated in {bands.Count - over} of {bands.Count} confidence bands.";
    }

    /// <summary>Mean Brier across resolved forecasts, or null if none have resolved.</summary>
    public static double? MeanBrier(IEnumerable<double?> scores)
    {
        var values = scores.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }
}
