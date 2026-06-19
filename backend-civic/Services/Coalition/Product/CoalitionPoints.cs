using Civic.API.Models;
using static Civic.API.Models.CoalitionActType;

namespace Civic.API.Services.Coalition.Product;

/// <summary>
/// The points economy (doc 01/04): daily REASONING XP — low ceiling, diminishing
/// returns within the day — vs the SCARCE premium currency (uncapped) earned by
/// macro/coalition acts. Encodes the agree-vs-amend asymmetry (a bare co-sign is
/// worth almost nothing; an amendment is worth real points). Pure.
/// </summary>
public static class CoalitionPoints
{
    public const int DailyReasoningCap = 30;
    public const double DiminishingFactor = 0.6; // each further reasoning act earns 60% of the previous

    public static string Currency(CoalitionActType t) => t switch
    {
        AuthorProvision or WritePlank or PrincipledDissent or Longform or CoalitionPassReward => "scarce",
        _ => "reasoning",
    };

    public static int BasePoints(CoalitionActType t) => t switch
    {
        ReactionWithReason => 3,
        ClaimTag => 3,
        Position => 5,
        Steelman => 8,
        CultureGovernanceSort => 6,
        ReactAndRoute => 5,
        CoSign => 2,                 // bare co-sign — deliberately low (mush guard)
        Amend => 12,                 // co-sign-with-substance — real points
        AuthorProvision => 25,
        WritePlank => 30,
        PrincipledDissent => 20,
        Longform => 20,
        CoalitionPassReward => 30,   // + breadth bonus, added by the caller
        DiedReasoningPayout => 4,
        CampaignNewsResponse => 5,   // a considered campaign response
        BriefingRead => 2,           // light: showing up to read the briefing
        CampaignReaction => 1,       // cheap; the daily cap is what really bounds it
        _ => 1,
    };

    public static bool QualityGated(CoalitionActType t) =>
        t is Steelman or Longform or PrincipledDissent or WritePlank or AuthorProvision;

    /// <summary>Reasoning XP with within-day diminishing returns and a daily cap.</summary>
    public static int ApplyDiminishing(int basePoints, int priorReasoningActsToday, int reasoningEarnedToday)
    {
        var factor = Math.Pow(DiminishingFactor, priorReasoningActsToday);
        var pts = Math.Max(1, (int)Math.Round(basePoints * factor));
        var remaining = Math.Max(0, DailyReasoningCap - reasoningEarnedToday);
        return Math.Min(pts, remaining);
    }
}
