namespace Civic.API.Services.Campaign;

/// <summary>
/// Tunable parameters for the Campaign Manager game mode. All values have sensible defaults so the
/// feature works with no configuration; override via the "CivicCampaign" configuration section.
/// </summary>
public class CivicCampaignOptions
{
    // Campaign length is tied to the live election date, not a custom duration.
    // Safety bound on the number of playable days (does not change the election date shown).
    public int MaxCampaignDays { get; set; } = 200;

    // Daily action budget.
    public int ActionsPerDay { get; set; } = 2;

    // How many recent news briefings to offer the manager to respond to (keep 5-7).
    public int NewsItemsToOffer { get; set; } = 6;

    // How many response options to generate per (candidate, briefing).
    public int ResponseOptionsPerItem { get; set; } = 3;

    // Max length (chars) of a news-response post body. Longer than a tweet so responses can be
    // more substantive and pointed; campaign responses are not bound by the bot posts' 160 limit.
    public int ResponseMaxChars { get; set; } = 600;

    // Base magnitude (in support points) of a single well-aimed action before modifiers.
    public double BaseActionPoints { get; set; } = 3.0;

    // How strongly candidate↔issue fit scales an action (fit is -1..1).
    public double FitWeight { get; set; } = 1.0;

    // How strongly the week's issue salience scales an action (salience is 0..1).
    public double SalienceWeight { get; set; } = 1.0;

    // Momentum amplifier: multiplier = 1 + (momentum-50)*MomentumAmplification. 50->1.0, 100->1.2.
    public double MomentumAmplification { get; set; } = 0.004;

    // Momentum decays toward 50 each week by this factor; positive results add momentum.
    public double MomentumDecay { get; set; } = 0.85;
    public double MomentumGainPerPoint { get; set; } = 4.0;

    // Per-action-type modifiers.
    public double RapidResponseMultiplier { get; set; } = 1.3;  // higher risk/reward on hot news
    public double NewsResponseMultiplier { get; set; } = 1.3;   // responding to real news lands harder
    public double TargetIssueFocusBonus { get; set; } = 1.25;   // concentrated effort
    public double ShoreUpAxisDefense { get; set; } = 0.5;       // reduces opponent gains this day
    public double OffBrandPenalty { get; set; } = 0.5;          // multiplier when fit is negative

    // Base support magnitude per action (daily turns are smaller than the old weekly turns).
    // NOTE: BaseActionPoints below is the base; daily play uses these alongside DailyDriftScale.

    // Opponent AI: per-day support drift toward/away, scaled by difficulty (support points).
    // Smaller than the old weekly values since opponents now move every day.
    public double OpponentDriftEasy { get; set; } = 0.15;
    public double OpponentDriftNormal { get; set; } = 0.30;
    public double OpponentDriftHard { get; set; } = 0.50;

    // Random variance applied to opponent moves (support points, +/-). Deterministic in tests (0).
    public double OpponentVariance { get; set; } = 1.0;

    // Starting standings.
    public double StartingMomentum { get; set; } = 50;
    /// <summary>Incumbents start with this many extra support points over an even split.</summary>
    public double IncumbentBonus { get; set; } = 6.0;
}
