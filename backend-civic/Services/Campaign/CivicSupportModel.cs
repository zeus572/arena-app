using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Pure, deterministic, DB-free formula engine for the Campaign Manager support simulation.
/// Models a multi-candidate race as a shared pool of support shares (summing to ~100). The
/// managed candidate's weekly actions move support; opponents drift on a difficulty curve; the
/// pool is renormalized each week. All randomness is passed in by callers (tests pass 0) so the
/// model is fully unit-testable.
/// </summary>
public static class CivicSupportModel
{
    public static double Clamp(double v, double min, double max)
        => v < min ? min : (v > max ? max : v);

    /// <summary>Momentum (0..100, centered at 50) softly amplifies a candidate's gains. 50→1.0.</summary>
    public static double MomentumAmplifier(double momentum, CivicCampaignOptions o)
        => 1 + (momentum - 50) * o.MomentumAmplification;

    /// <summary>Decay momentum toward 50, then add this week's gains; clamped to [0,100].</summary>
    public static double UpdateMomentum(double prev, double gains, CivicCampaignOptions o)
        => Clamp(50 + (prev - 50) * o.MomentumDecay + gains, 0, 100);

    /// <summary>
    /// How well a candidate fits an issue, in [-1, 1]. Positive = the issue plays to the
    /// candidate's platform/values strengths; negative = off-brand. <paramref name="fitRaw"/> is
    /// the caller-computed alignment (e.g. from plank tag overlap and axis scores).
    /// </summary>
    public static double NormalizeFit(double fitRaw) => Clamp(fitRaw, -1, 1);

    /// <summary>
    /// The raw support points an action yields for the managed candidate, before pool normalization.
    /// Combines base magnitude × fit × salience × momentum × per-action modifiers.
    /// </summary>
    public static double ActionPoints(
        CivicCampaignActionType actionType,
        double fit,
        double salience,
        double momentum,
        CivicCampaignOptions o)
    {
        var f = NormalizeFit(fit);
        var s = Clamp(salience, 0, 1);

        // Fit contributes multiplicatively around 1.0: fit=+1 → (1+FitWeight); fit=-1 → (1-FitWeight).
        var fitFactor = 1 + f * o.FitWeight;
        var salienceFactor = 1 + (s - 0.5) * 2 * o.SalienceWeight; // s=0.5 → 1.0; s=1 → 1+W; s=0 → 1-W
        var amp = MomentumAmplifier(momentum, o);

        var points = o.BaseActionPoints * fitFactor * salienceFactor * amp;

        points *= actionType switch
        {
            CivicCampaignActionType.RespondToNews => o.NewsResponseMultiplier,
            CivicCampaignActionType.RapidResponse => o.RapidResponseMultiplier,
            CivicCampaignActionType.TargetIssue => o.TargetIssueFocusBonus,
            CivicCampaignActionType.ShoreUpAxis => o.ShoreUpAxisDefense,
            _ => 1.0,
        };

        // Off-brand actions (negative fit) are damped further so they can hurt.
        if (f < 0) points *= o.OffBrandPenalty;

        return points;
    }

    public static double OpponentDriftBase(CivicCampaignDifficulty difficulty, CivicCampaignOptions o)
        => difficulty switch
        {
            CivicCampaignDifficulty.Easy => o.OpponentDriftEasy,
            CivicCampaignDifficulty.Hard => o.OpponentDriftHard,
            _ => o.OpponentDriftNormal,
        };

    /// <summary>
    /// One opponent's pre-normalization support change for a week. Opponents trend upward on the
    /// difficulty curve (they're campaigning too), modulated by their own fit to the week's issues
    /// and a caller-supplied variance term. A player's ShoreUpAxis action reduces this via
    /// <paramref name="defenseFactor"/> (1.0 = no defense, &lt;1 = blunted).
    /// </summary>
    public static double OpponentDelta(
        CivicCampaignDifficulty difficulty,
        double opponentFit,
        double opponentMomentum,
        double variance,
        double defenseFactor,
        CivicCampaignOptions o)
    {
        var basis = OpponentDriftBase(difficulty, o);
        var fitFactor = 1 + NormalizeFit(opponentFit) * 0.5;
        var amp = MomentumAmplifier(opponentMomentum, o);
        return (basis * fitFactor * amp + variance) * Clamp(defenseFactor, 0, 1);
    }

    /// <summary>
    /// Apply this week's raw deltas to current shares and renormalize so the field sums to 100.
    /// <paramref name="deltas"/> and <paramref name="current"/> are index-aligned. Shares are
    /// floored at a small epsilon so no candidate goes negative before normalization.
    /// </summary>
    public static double[] ApplyAndNormalize(double[] current, double[] deltas)
    {
        if (current.Length != deltas.Length)
            throw new ArgumentException("current and deltas must be the same length.");

        var raw = new double[current.Length];
        for (var i = 0; i < current.Length; i++)
            raw[i] = Math.Max(0.01, current[i] + deltas[i]);

        var sum = raw.Sum();
        if (sum <= 0)
        {
            // Degenerate: distribute evenly.
            var even = 100.0 / current.Length;
            return current.Select(_ => even).ToArray();
        }

        var result = new double[raw.Length];
        for (var i = 0; i < raw.Length; i++)
            result[i] = raw[i] / sum * 100.0;
        return result;
    }

    /// <summary>Index of the winning (highest-share) candidate.</summary>
    public static int WinnerIndex(double[] shares)
    {
        var best = 0;
        for (var i = 1; i < shares.Length; i++)
            if (shares[i] > shares[best]) best = i;
        return best;
    }

    /// <summary>An even split across <paramref name="count"/> candidates.</summary>
    public static double EvenShare(int count) => count <= 0 ? 0 : 100.0 / count;
}
