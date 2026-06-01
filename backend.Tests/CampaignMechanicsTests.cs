using Arena.API.Models;
using Arena.API.Services;
using FluentAssertions;
using Xunit;

namespace Arena.API.Tests;

public class CampaignMechanicsTests
{
    private static CampaignTuningOptions T() => new();

    // ---- MomentumAmplifier ----

    [Fact]
    public void MomentumAmplifier_IsOne_AtFifty()
    {
        CampaignMechanics.MomentumAmplifier(50, T()).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void MomentumAmplifier_GreaterThanOne_AtHundred_AndLessThanOne_AtZero()
    {
        var t = T();
        CampaignMechanics.MomentumAmplifier(100, t).Should().BeGreaterThan(1.0);
        CampaignMechanics.MomentumAmplifier(0, t).Should().BeLessThan(1.0);
    }

    // ---- Advertising / TownHall scaling ----

    [Fact]
    public void AdvertisingApproval_GrowsWithSpend()
    {
        var t = T();
        var low = CampaignMechanics.AdvertisingApproval(1000, 50, t);
        var high = CampaignMechanics.AdvertisingApproval(10000, 50, t);
        low.Should().BeGreaterThan(0);
        high.Should().BeGreaterThan(low);
    }

    [Fact]
    public void TownHallApproval_GrowsWithCount()
    {
        var t = T();
        var one = CampaignMechanics.TownHallApproval(1, 50, t);
        var three = CampaignMechanics.TownHallApproval(3, 50, t);
        one.Should().BeGreaterThan(0);
        three.Should().BeGreaterThan(one);
    }

    // ---- UpdateMomentum decay toward 50 ----

    [Fact]
    public void UpdateMomentum_DecaysTowardFifty_FromAbove()
    {
        var t = T();
        var next = CampaignMechanics.UpdateMomentum(100, gains: 0, t);
        next.Should().BeLessThan(100);
        next.Should().BeGreaterThan(50);
    }

    [Fact]
    public void UpdateMomentum_DecaysTowardFifty_FromBelow()
    {
        var t = T();
        var next = CampaignMechanics.UpdateMomentum(0, gains: 0, t);
        next.Should().BeGreaterThan(0);
        next.Should().BeLessThan(50);
    }

    [Fact]
    public void UpdateMomentum_StaysClampedZeroToHundred()
    {
        var t = T();
        CampaignMechanics.UpdateMomentum(100, gains: 1000, t).Should().BeLessThanOrEqualTo(100);
        CampaignMechanics.UpdateMomentum(0, gains: -1000, t).Should().BeGreaterThanOrEqualTo(0);
    }

    // ---- DifficultyPressure ordering + week growth ----

    [Fact]
    public void DifficultyPressure_OrderingEasyLessThanNormalLessThanHard()
    {
        var t = T();
        var easy = CampaignMechanics.DifficultyPressure(CampaignDifficulty.Easy, 1, t);
        var normal = CampaignMechanics.DifficultyPressure(CampaignDifficulty.Normal, 1, t);
        var hard = CampaignMechanics.DifficultyPressure(CampaignDifficulty.Hard, 1, t);
        easy.Should().BeLessThan(normal);
        normal.Should().BeLessThan(hard);
    }

    [Fact]
    public void DifficultyPressure_IncreasesWithWeek()
    {
        var t = T();
        var week1 = CampaignMechanics.DifficultyPressure(CampaignDifficulty.Normal, 1, t);
        var week4 = CampaignMechanics.DifficultyPressure(CampaignDifficulty.Normal, 4, t);
        week4.Should().BeGreaterThan(week1);
    }

    // ---- ComputeOutcome threshold ----

    [Fact]
    public void ComputeOutcome_WonWhenAtOrAboveThreshold_AndBoundaryAtFifty()
    {
        var t = T(); // WinThreshold = 50
        CampaignMechanics.ComputeOutcome(49.9, t).Won.Should().BeFalse();
        CampaignMechanics.ComputeOutcome(50.0, t).Won.Should().BeTrue();   // boundary: >= wins
        CampaignMechanics.ComputeOutcome(75.0, t).Won.Should().BeTrue();
        CampaignMechanics.ComputeOutcome(50.0, t).FinalApproval.Should().Be(50.0);
    }

    // ---- DebatePerformance ----

    [Fact]
    public void DebatePerformance_WonWhenPlayerScoreExceedsOpponent()
    {
        var t = T();
        var perf = CampaignMechanics.DebatePerformanceResult(
            momentum: 100, CampaignDifficulty.Easy, week: 1, variance: 0, t);
        perf.Won.Should().Be(perf.PlayerScore > perf.OpponentScore);
        perf.Margin.Should().Be(perf.PlayerScore - perf.OpponentScore);
    }

    [Fact]
    public void DebatePerformance_HighMomentumEasy_TendsToWin()
    {
        var t = T();
        var perf = CampaignMechanics.DebatePerformanceResult(
            momentum: 100, CampaignDifficulty.Easy, week: 1, variance: 0, t);
        perf.Won.Should().BeTrue();
    }

    [Fact]
    public void DebatePerformance_LowMomentumHard_TendsToLose()
    {
        var t = T();
        var perf = CampaignMechanics.DebatePerformanceResult(
            momentum: 0, CampaignDifficulty.Hard, week: 4, variance: 0, t);
        perf.Won.Should().BeFalse();
    }

    [Fact]
    public void DebatePerformance_SignedIsClampedToPlusMinus40()
    {
        var t = T();
        // Extreme inputs to push the raw margin beyond the clamp.
        var perf = CampaignMechanics.DebatePerformanceResult(
            momentum: 100, CampaignDifficulty.Easy, week: 1, variance: 1000, t);
        perf.Signed.Should().BeLessThanOrEqualTo(40);
        perf.Signed.Should().BeGreaterThanOrEqualTo(-40);
    }

    // ---- ComputeWeek ----

    [Fact]
    public void ComputeWeek_ApprovalStaysClampedWithinZeroToHundred()
    {
        var t = T();

        var highInput = new WeekInput
        {
            PrevApproval = 99,
            Momentum = 100,
            Difficulty = CampaignDifficulty.Easy,
            Week = 1,
            AdvertisingSpend = 1_000_000,
            TownHallCount = 50,
            EventApprovalEffect = 100,
            DebateApprovalEffect = 100,
            ExtraMomentumGain = 0,
        };
        CampaignMechanics.ComputeWeek(highInput, t).NewApproval.Should().BeLessThanOrEqualTo(100);

        var lowInput = new WeekInput
        {
            PrevApproval = 1,
            Momentum = 0,
            Difficulty = CampaignDifficulty.Hard,
            Week = 10,
            AdvertisingSpend = 0,
            TownHallCount = 0,
            EventApprovalEffect = -100,
            DebateApprovalEffect = -100,
            ExtraMomentumGain = 0,
        };
        CampaignMechanics.ComputeWeek(lowInput, t).NewApproval.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ComputeWeek_HigherAdvertisingSpend_YieldsHigherApproval_AllElseEqual()
    {
        var t = T();
        WeekInput Make(double spend) => new()
        {
            PrevApproval = 50,
            Momentum = 50,
            Difficulty = CampaignDifficulty.Normal,
            Week = 1,
            AdvertisingSpend = spend,
            TownHallCount = 0,
            EventApprovalEffect = 0,
            DebateApprovalEffect = 0,
            ExtraMomentumGain = 0,
        };

        var low = CampaignMechanics.ComputeWeek(Make(1000), t).NewApproval;
        var high = CampaignMechanics.ComputeWeek(Make(50000), t).NewApproval;
        high.Should().BeGreaterThan(low);
    }
}
