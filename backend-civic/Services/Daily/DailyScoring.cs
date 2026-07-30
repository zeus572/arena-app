using Civic.API.Models.Daily;

namespace Civic.API.Services.Daily;

/// <summary>
/// Per-round detail returned alongside a score so the client can render the reveal
/// and the share grid without recomputing (or being told) the answer key.
/// </summary>
public record RoundResult(int Score, string Band);

/// <summary>
/// Share-grid bands. Kept as strings rather than emoji so the grid rendering lives in
/// one place (<see cref="DailyShareGrid"/>) and tests can assert on bands directly.
/// </summary>
public static class Bands
{
    public const string Hit = "hit";      // exact / very close
    public const string Near = "near";     // one notch off
    public const string Miss = "miss";
}

/// <summary>
/// All daily-game scoring. Pure functions — no DB, no clock, no randomness — so the
/// math is unit-testable in isolation. Every scorer returns 0..100.
/// See docs/civic_daily_games/ for the derivation of each formula.
/// </summary>
public static class DailyScoring
{
    // ------------------------------------------------------------ Crowd Call

    /// <summary>
    /// Calibration scoring on percentage points: <c>max(0, 100 - 2 * error)</c>, so a
    /// 50-point error scores 0. The 2x multiplier is deliberate — a flat 100-error
    /// curve scores 60 for pure guessing, which feels unearned.
    /// </summary>
    public static (int Total, List<RoundResult> Rounds) ScoreCrowdCall(
        CrowdCallPayload payload, CrowdCallResponse response)
    {
        var rounds = new List<RoundResult>();
        for (var i = 0; i < payload.Rounds.Count; i++)
        {
            var guess = i < response.Guesses.Count ? Math.Clamp(response.Guesses[i], 0, 1) : 0;
            var errorPoints = Math.Abs(guess - payload.Rounds[i].TrueRate) * 100;
            var score = (int)Math.Round(Math.Max(0, 100 - 2 * errorPoints));
            var band = errorPoints <= 10 ? Bands.Hit : errorPoints <= 25 ? Bands.Near : Bands.Miss;
            rounds.Add(new RoundResult(score, band));
        }
        return (Mean(rounds), rounds);
    }

    /// <summary>
    /// How many rounds the player guessed HIGHER than reality. The systematic
    /// over-estimation of division is the specific bias this game exists to surface,
    /// so the end-card reports signed error, not absolute.
    /// </summary>
    public static int CountOverestimatedDivision(CrowdCallPayload payload, CrowdCallResponse response)
    {
        var count = 0;
        for (var i = 0; i < payload.Rounds.Count; i++)
        {
            var guess = i < response.Guesses.Count ? response.Guesses[i] : 0;
            // A LOWER guessed correct-rate means the player expected more people to get
            // it wrong — i.e. overestimated how divided/uninformed the country is.
            if (guess < payload.Rounds[i].TrueRate) count++;
        }
        return count;
    }

    // ------------------------------------------------------------- Priced In

    /// <summary>
    /// Ratio error, not absolute — being off by $10B means something very different on
    /// a $12B item than a $900B one. 60 at 1.5x, 20 at 6x, 0 beyond 100x, then a 10%
    /// haircut per extra guess used.
    /// </summary>
    public static int ScorePricedIn(double trueValue, double finalGuess, int guessesUsed)
    {
        if (finalGuess <= 0 || trueValue <= 0) return 0;

        var ratioError = Math.Abs(Math.Log10(finalGuess / trueValue));
        var raw = Math.Max(0, 100 - 40 * ratioError);
        var haircut = 1 - 0.1 * Math.Max(0, guessesUsed - 1);
        return (int)Math.Round(Math.Max(0, raw * haircut));
    }

    /// <summary>How close the final guess landed, as a multiple (always >= 1).</summary>
    public static double Closeness(double trueValue, double finalGuess)
    {
        if (finalGuess <= 0 || trueValue <= 0) return double.PositiveInfinity;
        var ratio = finalGuess / trueValue;
        return ratio >= 1 ? ratio : 1 / ratio;
    }

    // -------------------------------------------------------------- Place It

    /// <summary>Bucket-distance credit. Adjacent scores 70 — the truth is a synthesis,
    /// and "one notch off" is a legitimate reading rather than an error.</summary>
    private static readonly int[] BucketCredit = { 100, 70, 40, 15, 0 };

    public static (int Total, List<RoundResult> Axes) ScorePlaceIt(
        PlaceItPayload payload, IReadOnlyList<int> finalGuesses, int roundsUsed)
    {
        var axes = new List<RoundResult>();
        for (var i = 0; i < payload.Axes.Count; i++)
        {
            var guess = i < finalGuesses.Count ? finalGuesses[i] : 0;
            var distance = Math.Min(BucketCredit.Length - 1, Math.Abs(guess - payload.Axes[i].TrueBucket));
            var band = distance == 0 ? Bands.Hit : distance == 1 ? Bands.Near : Bands.Miss;
            axes.Add(new RoundResult(BucketCredit[distance], band));
        }

        var haircut = 1 - 0.15 * Math.Max(0, roundsUsed - 1);
        var total = (int)Math.Round(Math.Max(0, Mean(axes) * haircut));
        return (total, axes);
    }

    /// <summary>Per-axis feedback for a non-final round: is the truth higher or lower?</summary>
    public static string[] PlaceItHints(PlaceItPayload payload, IReadOnlyList<int> guesses)
    {
        var hints = new string[payload.Axes.Count];
        for (var i = 0; i < payload.Axes.Count; i++)
        {
            var guess = i < guesses.Count ? guesses[i] : 0;
            var truth = payload.Axes[i].TrueBucket;
            hints[i] = guess == truth ? "exact" : truth > guess ? "higher" : "lower";
        }
        return hints;
    }

    /// <summary>
    /// Bucket a synthesized axis score (-1..+1) onto the 5-point guessing grid, so
    /// guesses and truth live on the same scale. Cut points: -0.6 / -0.2 / +0.2 / +0.6.
    /// </summary>
    public static int BucketAxisScore(double score) => score switch
    {
        < -0.6 => 0,
        < -0.2 => 1,
        <= 0.2 => 2,
        <= 0.6 => 3,
        _ => 4,
    };

    // ---------------------------------------------------------- Time Machine

    /// <summary>
    /// Pairwise concordance (Kendall tau rescaled to 0..100). Rewards nearly-right
    /// orderings instead of collapsing to all-or-nothing: one adjacent swap of five
    /// items scores 90, a fully reversed order scores 0.
    /// </summary>
    public static (int Score, int Concordant, int TotalPairs) ScoreTimeMachineSort(
        IReadOnlyList<string> trueOrder, IReadOnlyList<string> guessOrder)
    {
        var truthRank = trueOrder.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var guessRank = guessOrder.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

        var concordant = 0;
        var totalPairs = 0;
        for (var a = 0; a < trueOrder.Count; a++)
        {
            for (var b = a + 1; b < trueOrder.Count; b++)
            {
                var idA = trueOrder[a];
                var idB = trueOrder[b];
                if (!guessRank.ContainsKey(idA) || !guessRank.ContainsKey(idB)) continue;

                totalPairs++;
                var truthSaysAFirst = truthRank[idA] < truthRank[idB];
                var guessSaysAFirst = guessRank[idA] < guessRank[idB];
                if (truthSaysAFirst == guessSaysAFirst) concordant++;
            }
        }

        var score = totalPairs == 0 ? 0 : (int)Math.Round(100.0 * concordant / totalPairs);
        return (score, concordant, totalPairs);
    }

    /// <summary>Per-item band for the share grid: did each headline land in its true slot?</summary>
    public static List<RoundResult> TimeMachineSlots(
        IReadOnlyList<string> trueOrder, IReadOnlyList<string> guessOrder)
    {
        var slots = new List<RoundResult>();
        for (var i = 0; i < trueOrder.Count; i++)
        {
            var correct = i < guessOrder.Count && guessOrder[i] == trueOrder[i];
            slots.Add(new RoundResult(correct ? 100 : 0, correct ? Bands.Hit : Bands.Miss));
        }
        return slots;
    }

    public static int ScoreTimeMachineOddOneOut(string? currentItemId, string? pick) =>
        !string.IsNullOrEmpty(pick) && pick == currentItemId ? 100 : 0;

    // ----------------------------------------------------------- Whose Value

    public static (int Total, List<RoundResult> Rounds) ScoreWhoseValue(
        WhoseValuePayload payload, WhoseValueResponse response)
    {
        var rounds = new List<RoundResult>();
        for (var i = 0; i < payload.Rounds.Count; i++)
        {
            var pick = i < response.Picks.Count ? response.Picks[i] : null;
            var correct = pick == payload.Rounds[i].CorrectAxisKey;
            rounds.Add(new RoundResult(correct ? 100 : 0, correct ? Bands.Hit : Bands.Miss));
        }

        var total = payload.Rounds.Count == 0
            ? 0
            : (int)Math.Round(100.0 * rounds.Count(r => r.Score == 100) / payload.Rounds.Count);
        return (total, rounds);
    }

    // --------------------------------------------------------- Which Is True

    /// <summary>
    /// Straight accuracy. Deliberately NOT curved for the 50% floor a two-option
    /// question gives you: the end-card reports the raw count, and "3/5 on coin flips"
    /// is exactly the humbling read the game is for.
    /// </summary>
    public static (int Total, List<RoundResult> Rounds) ScoreWhichIsTrue(
        WhichIsTruePayload payload, WhichIsTrueResponse response)
    {
        var rounds = new List<RoundResult>();
        for (var i = 0; i < payload.Rounds.Count; i++)
        {
            var pick = i < response.Picks.Count ? response.Picks[i] : null;
            var correct = pick == payload.Rounds[i].Correct;
            rounds.Add(new RoundResult(correct ? 100 : 0, correct ? Bands.Hit : Bands.Miss));
        }

        var total = payload.Rounds.Count == 0
            ? 0
            : (int)Math.Round(100.0 * rounds.Count(r => r.Score == 100) / payload.Rounds.Count);
        return (total, rounds);
    }

    private static int Mean(List<RoundResult> rounds) =>
        rounds.Count == 0 ? 0 : (int)Math.Round(rounds.Average(r => r.Score));
}
