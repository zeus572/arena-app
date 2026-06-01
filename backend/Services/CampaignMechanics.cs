using Arena.API.Models;

namespace Arena.API.Services;

/// <summary>
/// Pure, deterministic, DB-free formula engine for the Campaign Manager.
/// All randomness (variance) is passed in by callers so this stays fully unit-testable.
/// </summary>
public static class CampaignMechanics
{
    public static double Clamp(double v, double min, double max)
        => v < min ? min : (v > max ? max : v);

    public static double ClampApproval(double v) => Clamp(v, 0, 100);

    /// <summary>
    /// Momentum acts as a soft multiplier on approval gains, centered at 50 → 1.0.
    /// 100 → 1.2, 0 → 0.8 (with default amplification 0.004).
    /// </summary>
    public static double MomentumAmplifier(double momentum, CampaignTuningOptions t)
        => 1 + (momentum - 50) * t.MomentumAmplification;

    public static double AdvertisingApproval(double spendDollars, double momentum, CampaignTuningOptions t)
        => (spendDollars / 1000.0) * t.AdvertisingApprovalPer1k * MomentumAmplifier(momentum, t);

    public static double TownHallApproval(int count, double momentum, CampaignTuningOptions t)
        => count * t.TownHallApprovalEach * MomentumAmplifier(momentum, t);

    public static double Fundraising(int staff, CampaignTuningOptions t)
        => staff * t.FundraisingPerStaff;

    public static double DifficultyBase(CampaignDifficulty difficulty, CampaignTuningOptions t)
        => difficulty switch
        {
            CampaignDifficulty.Easy => t.DifficultyPressureEasy,
            CampaignDifficulty.Hard => t.DifficultyPressureHard,
            _ => t.DifficultyPressureNormal,
        };

    /// <summary>Per-week approval drag, scaling up slightly with the week number.</summary>
    public static double DifficultyPressure(CampaignDifficulty difficulty, int week, CampaignTuningOptions t)
        => DifficultyBase(difficulty, t) * (1 + week * 0.1);

    /// <summary>
    /// New momentum after one week: decays toward 50, then adds this week's momentum gains.
    /// </summary>
    public static double UpdateMomentum(double prev, double gains, CampaignTuningOptions t)
        => Clamp(50 + (prev - 50) * t.MomentumDecay + gains, 0, 100);

    /// <summary>
    /// Simulate a debate milestone outcome. Variance is supplied by the caller
    /// (tests pass 0 for determinism).
    /// </summary>
    public static DebatePerformance DebatePerformanceResult(
        double momentum, CampaignDifficulty difficulty, int week, double variance, CampaignTuningOptions t)
    {
        var playerScore = 50 + (momentum - 50) * 0.5 + variance;
        var opponentDifficultyBase = difficulty switch
        {
            CampaignDifficulty.Easy => 0.0,
            CampaignDifficulty.Hard => 16.0,
            _ => 8.0,
        };
        var opponentScore = 50 + opponentDifficultyBase + week * 2;
        var signed = Clamp(playerScore - opponentScore, -40, 40);
        return new DebatePerformance
        {
            PlayerScore = playerScore,
            OpponentScore = opponentScore,
            Signed = signed,
            Won = playerScore > opponentScore,
            Margin = playerScore - opponentScore,
        };
    }

    public static CampaignOutcome ComputeOutcome(double finalApproval, CampaignTuningOptions t)
    {
        var won = finalApproval >= t.WinThreshold;
        var outcome = won
            ? $"Victory — finished with {finalApproval:F1}% approval, at or above the {t.WinThreshold:F0}% threshold."
            : $"Defeat — finished with {finalApproval:F1}% approval, below the {t.WinThreshold:F0}% threshold.";
        return new CampaignOutcome
        {
            Won = won,
            FinalApproval = finalApproval,
            Outcome = outcome,
        };
    }

    /// <summary>
    /// Aggregate one week of decisions into a new approval and momentum, with a component breakdown.
    /// </summary>
    public static WeekResult ComputeWeek(WeekInput input, CampaignTuningOptions t)
    {
        var advertising = AdvertisingApproval(input.AdvertisingSpend, input.Momentum, t);
        var townhall = TownHallApproval(input.TownHallCount, input.Momentum, t);
        var momentumBonus = (input.Momentum - 50) * 0.02;
        var difficultyPressure = DifficultyPressure(input.Difficulty, input.Week, t);

        var change = t.BaseApprovalChange
            + advertising
            + townhall
            + input.EventApprovalEffect
            + input.DebateApprovalEffect
            + momentumBonus
            - difficultyPressure;

        var newApproval = ClampApproval(input.PrevApproval + change);
        var newMomentum = UpdateMomentum(input.Momentum, input.ExtraMomentumGain, t);

        return new WeekResult
        {
            NewApproval = newApproval,
            NewMomentum = newMomentum,
            Components = new WeekComponents
            {
                Base = t.BaseApprovalChange,
                Advertising = advertising,
                TownHall = townhall,
                Event = input.EventApprovalEffect,
                Debate = input.DebateApprovalEffect,
                MomentumBonus = momentumBonus,
                DifficultyPressure = difficultyPressure,
                NetChange = change,
            },
        };
    }
}

public struct DebatePerformance
{
    public double PlayerScore { get; set; }
    public double OpponentScore { get; set; }
    public double Signed { get; set; }
    public bool Won { get; set; }
    public double Margin { get; set; }
}

public struct CampaignOutcome
{
    public bool Won { get; set; }
    public double FinalApproval { get; set; }
    public string Outcome { get; set; }
}

public class WeekInput
{
    public double PrevApproval { get; set; }
    public double Momentum { get; set; }
    public CampaignDifficulty Difficulty { get; set; }
    public int Week { get; set; }
    public double AdvertisingSpend { get; set; }
    public int TownHallCount { get; set; }
    public double EventApprovalEffect { get; set; }
    public double DebateApprovalEffect { get; set; }
    public double ExtraMomentumGain { get; set; }
}

public class WeekResult
{
    public double NewApproval { get; set; }
    public double NewMomentum { get; set; }
    public WeekComponents Components { get; set; } = new();
}

public class WeekComponents
{
    public double Base { get; set; }
    public double Advertising { get; set; }
    public double TownHall { get; set; }
    public double Event { get; set; }
    public double Debate { get; set; }
    public double MomentumBonus { get; set; }
    public double DifficultyPressure { get; set; }
    public double NetChange { get; set; }
}
