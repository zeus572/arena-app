namespace Civic.API.Services.Bills;

/// <summary>
/// Pure helpers relating a user's compass score to a bill's position on the same
/// axis. Kept free of EF/DTO types so it is trivially unit-testable.
/// </summary>
public static class BillAlignment
{
    /// <summary>Below this magnitude a score counts as "no strong lean" on the axis.</summary>
    public const double NeutralBand = 0.15;

    public const string Aligned = "aligned";
    public const string Mixed = "mixed";
    public const string Tension = "tension";

    /// <summary>
    /// Classify the relationship between a user's score and the bill's score on
    /// one axis. Either side sitting in the neutral band ⇒ "mixed"; same sign ⇒
    /// "aligned"; opposite signs ⇒ "tension".
    /// </summary>
    public static string Classify(double userScore, double billScore)
    {
        if (Math.Abs(userScore) < NeutralBand || Math.Abs(billScore) < NeutralBand)
            return Mixed;
        return Math.Sign(userScore) == Math.Sign(billScore) ? Aligned : Tension;
    }

    /// <summary>
    /// Per-axis closeness in [0,1]: 1 when the user and bill sit at the same point,
    /// 0 when at opposite ends of the axis.
    /// </summary>
    public static double Closeness(double userScore, double billScore) =>
        1.0 - Math.Abs(userScore - billScore) / 2.0;

    /// <summary>
    /// Overall alignment percentage (0..100) across the shared axes, or null when
    /// there are none. Each axis is weighted by the bill's confidence in its own
    /// position so speculative positions count less.
    /// </summary>
    public static int? OverallPercent(IEnumerable<(double UserScore, double BillScore, double BillConfidence)> pairs)
    {
        double weightSum = 0;
        double weighted = 0;
        foreach (var (u, b, conf) in pairs)
        {
            var w = Math.Max(0.05, Math.Clamp(conf, 0.0, 1.0));
            weighted += Closeness(u, b) * w;
            weightSum += w;
        }

        if (weightSum <= 0) return null;
        return (int)Math.Round(100.0 * weighted / weightSum);
    }
}
