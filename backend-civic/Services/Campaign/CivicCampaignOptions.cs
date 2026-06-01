namespace Civic.API.Services.Campaign;

/// <summary>
/// Tunable parameters for the Campaign Manager game mode. All values have sensible defaults so the
/// feature works with no configuration; override via the "CivicCampaign" configuration section.
/// </summary>
public class CivicCampaignOptions
{
    // Campaign length.
    public int DefaultTotalWeeks { get; set; } = 8;
    public int MinTotalWeeks { get; set; } = 4;
    public int MaxTotalWeeks { get; set; } = 16;

    // Weekly action budget.
    public int ActionsPerWeek { get; set; } = 3;

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
    public double TargetIssueFocusBonus { get; set; } = 1.25;   // concentrated effort
    public double ShoreUpAxisDefense { get; set; } = 0.5;       // reduces opponent gains this week
    public double OffBrandPenalty { get; set; } = 0.5;          // multiplier when fit is negative

    // Opponent AI: weekly support drift toward/away, scaled by difficulty (support points).
    public double OpponentDriftEasy { get; set; } = 0.8;
    public double OpponentDriftNormal { get; set; } = 1.5;
    public double OpponentDriftHard { get; set; } = 2.4;

    // Random variance applied to opponent moves (support points, +/-). Deterministic in tests (0).
    public double OpponentVariance { get; set; } = 1.0;

    // Starting standings.
    public double StartingMomentum { get; set; } = 50;
    /// <summary>Incumbents start with this many extra support points over an even split.</summary>
    public double IncumbentBonus { get; set; } = 6.0;
}
