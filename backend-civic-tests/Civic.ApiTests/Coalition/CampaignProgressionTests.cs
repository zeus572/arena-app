using Civic.API.Services.Coalition.Curriculum;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 3.4 gate: a simulated campaign produces sensible records, breadth meters,
/// governance ratios, league movement, and a soft (non-all-or-nothing) cadence.
/// Pure — no DB, no LLM.
/// </summary>
public class CampaignProgressionTests
{
    [Fact]
    public void Milestones_AccrueFromPassedPlanks()
    {
        var planks = new[]
        {
            new PassedPlank(GapWidthAtBirth: 0.2, Breadth: 2, Specificity: 2, MovedSigners: 1, IsGovernance: true),
            new PassedPlank(GapWidthAtBirth: 0.8, Breadth: 3, Specificity: 3, MovedSigners: 3, IsGovernance: true),
            new PassedPlank(GapWidthAtBirth: 0.5, Breadth: 2, Specificity: 1, MovedSigners: 2, IsGovernance: false),
        };

        var s = CampaignMilestones.Accrue(planks);

        s.PlanksPassed.Should().Be(3);
        s.TotalBreadth.Should().Be(7);
        s.AvgBreadth.Should().BeApproximately(7.0 / 3.0, 1e-9);
        s.TotalMovedSigners.Should().Be(6);
        s.GovernanceRatio.Should().BeApproximately(2.0 / 3.0, 1e-9);
        // weighted = 2*1.2 + 3*1.8 + 2*1.5 = 2.4 + 5.4 + 3.0 = 10.8
        s.WeightedScore.Should().BeApproximately(10.8, 1e-9);

        CampaignMilestones.Accrue(Array.Empty<PassedPlank>())
            .Should().Be(new CampaignSummary(0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void PayoutCoupling_HarderProvisionWorthMore_AtEqualBreadth()
    {
        var easy = CampaignMilestones.Accrue(new[] { new PassedPlank(0.1, 3, 2, 2, true) });
        var hard = CampaignMilestones.Accrue(new[] { new PassedPlank(0.9, 3, 2, 2, true) });
        hard.WeightedScore.Should().BeGreaterThan(easy.WeightedScore,
            "bridging a wider gap at the same breadth should be worth more");
    }

    [Fact]
    public void Promotion_MovesOverSkilledPlayersToWiderGaps()
    {
        var tiers = new[] { 0.2, 0.4, 0.6, 0.8 };

        PromotionService.Decide(skill: 0.9, leagueGapTier: 0.3).Should().Be(LeagueMovement.Promote);
        PromotionService.Decide(skill: 0.35, leagueGapTier: 0.3).Should().Be(LeagueMovement.Stay);
        PromotionService.Decide(skill: 0.1, leagueGapTier: 0.6).Should().Be(LeagueMovement.Relegate);

        // A promoted player moves up a tier (wider gap); a relegated one moves down; bounded.
        PromotionService.NextTier(LeagueMovement.Promote, 0.4, tiers).Should().Be(0.6);
        PromotionService.NextTier(LeagueMovement.Relegate, 0.4, tiers).Should().Be(0.2);
        PromotionService.NextTier(LeagueMovement.Promote, 0.8, tiers).Should().Be(0.8, "already at the top");
        PromotionService.NextTier(LeagueMovement.Relegate, 0.2, tiers).Should().Be(0.2, "already at the bottom");
    }

    [Fact]
    public void Cadence_RewardsConsistency_WithoutAllOrNothingBreakage()
    {
        var perfect = Enumerable.Repeat(true, 7).ToList();
        var oneRecentMiss = new[] { true, true, true, true, true, true, false }.ToList();   // missed today
        var oneMidMiss = new[] { true, true, true, false, true, true, true }.ToList();       // missed midweek

        var sPerfect = CampaignCadence.Score(perfect);
        var sRecentMiss = CampaignCadence.Score(oneRecentMiss);
        var sMidMiss = CampaignCadence.Score(oneMidMiss);

        sPerfect.Should().Be(1.0);
        // A single miss does NOT zero the score (unlike a hard streak) — it stays high.
        sRecentMiss.Should().BeGreaterThan(0.6);
        sMidMiss.Should().BeGreaterThan(0.6);
        // Consistency is rewarded smoothly: one miss costs roughly the same wherever it lands.
        Math.Abs(sRecentMiss - sMidMiss).Should().BeLessThan(0.25);

        // Contrast: a hard streak collapses to 0 the moment the most recent day is missed.
        CampaignCadence.HardStreak(oneRecentMiss).Should().Be(0);
        CampaignCadence.HardStreak(perfect).Should().Be(7);
    }

    // ---- the gate: a simulated full campaign ----
    [Fact]
    public void SimulatedFullCampaign_ProducesSensibleProgression()
    {
        // An improving player closes progressively harder, broader coalitions over the campaign,
        // mostly on governance planks.
        var planks = new[]
        {
            new PassedPlank(0.2, 2, 1, 1, true),
            new PassedPlank(0.4, 2, 2, 2, true),
            new PassedPlank(0.6, 3, 2, 2, false),
            new PassedPlank(0.8, 3, 3, 3, true),
        };
        var summary = CampaignMilestones.Accrue(planks);

        // Record + meters are sensible.
        summary.PlanksPassed.Should().Be(4);
        summary.TotalBreadth.Should().Be(10);
        summary.GovernanceRatio.Should().BeApproximately(0.75, 1e-9);
        summary.WeightedScore.Should().BeGreaterThan(summary.TotalBreadth, "payout coupling lifts the harder closes");

        // Skill from this track record (3.2) lands the player above an entry tier -> promotion.
        var history = new LeagueHistory(planks.Select(p => new LeagueOutcome(p.GapWidthAtBirth, Closed: true)).ToList());
        var skill = GroupSkill.Estimate(history);
        skill.Should().BeGreaterThan(0.5);

        var movement = PromotionService.Decide(skill, leagueGapTier: 0.3);
        movement.Should().Be(LeagueMovement.Promote, "an over-skilled player is moved to wider gaps");
        PromotionService.NextTier(movement, 0.3, new[] { 0.2, 0.4, 0.6, 0.8 })
            .Should().BeGreaterThan(0.3);

        // Cadence over the campaign is healthy despite a missed day.
        var cadence = CampaignCadence.Score(new[] { true, true, false, true, true, true, true });
        cadence.Should().BeGreaterThan(0.7);
    }
}
