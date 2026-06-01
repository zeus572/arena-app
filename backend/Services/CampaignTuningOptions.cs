namespace Arena.API.Services;

/// <summary>
/// Tuning knobs for the Campaign Manager game. Bound from the "CampaignTuning"
/// configuration section; all values have sensible defaults so an empty section works.
/// </summary>
public class CampaignTuningOptions
{
    // Campaign length
    public int DefaultTotalWeeks { get; set; } = 4;
    public int MinTotalWeeks { get; set; } = 4;
    public int MaxTotalWeeks { get; set; } = 24;

    // Starting resources
    public double StartingBudget { get; set; } = 100000;
    public int StartingTimeUnits { get; set; } = 40;
    public int StartingStaff { get; set; } = 5;
    public double StartingMomentum { get; set; } = 50;
    public double StartingApproval { get; set; } = 50;

    // Win condition
    public double WinThreshold { get; set; } = 50;

    // Advertising
    public double AdvertisingApprovalPer1k { get; set; } = 0.2;

    // Town hall
    public double TownHallApprovalEach { get; set; } = 2.0;
    public int TownHallTimeCost { get; set; } = 5;

    // Fundraising
    public double FundraisingPerStaff { get; set; } = 8000;
    public int FundraisingTimeCost { get; set; } = 4;

    // Opposition research
    public double OppResearchMomentum { get; set; } = 6;
    public int OppResearchStaffTimeCost { get; set; } = 3;

    // Debate prep
    public double DebatePrepMomentum { get; set; } = 6;
    public int DebatePrepTimeCost { get; set; } = 5;

    // Polling
    public double PollingBudgetCost { get; set; } = 5000;

    // Momentum dynamics
    public double MomentumAmplification { get; set; } = 0.004;
    public double MomentumDecay { get; set; } = 0.85;

    // Difficulty pressure (per-week approval drag)
    public double DifficultyPressureEasy { get; set; } = 0.5;
    public double DifficultyPressureNormal { get; set; } = 1.5;
    public double DifficultyPressureHard { get; set; } = 3.0;

    // Debate milestones
    public int DebateMilestoneEveryNWeeks { get; set; } = 2;
    public bool DebatesMandatory { get; set; } = false;
    public double DebateSkipPenalty { get; set; } = 4.0;
    public double DebatePerformanceWeight { get; set; } = 0.15;
    public int TurnsPerDebate { get; set; } = 4;

    // Events
    public double EventChancePerWeek { get; set; } = 0.6;

    // Misc
    public double BaseApprovalChange { get; set; } = 0.0;
}
