using Civic.API.Models;
using Civic.API.Services.Campaign;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class CivicSupportModelTests
{
    private static CivicCampaignOptions Opts() => new();

    [Fact]
    public void MomentumAmplifier_IsCenteredAtFifty()
    {
        var o = Opts();
        CivicSupportModel.MomentumAmplifier(50, o).Should().BeApproximately(1.0, 1e-9);
        CivicSupportModel.MomentumAmplifier(100, o).Should().BeGreaterThan(1.0);
        CivicSupportModel.MomentumAmplifier(0, o).Should().BeLessThan(1.0);
    }

    [Fact]
    public void UpdateMomentum_DecaysTowardFifty()
    {
        var o = Opts();
        // From 100 with no gains, momentum should move down toward 50.
        var down = CivicSupportModel.UpdateMomentum(100, 0, o);
        down.Should().BeLessThan(100).And.BeGreaterThan(50);

        // From 0 with no gains, momentum should move up toward 50.
        var up = CivicSupportModel.UpdateMomentum(0, 0, o);
        up.Should().BeGreaterThan(0).And.BeLessThan(50);
    }

    [Fact]
    public void UpdateMomentum_IsClampedToRange()
    {
        var o = Opts();
        CivicSupportModel.UpdateMomentum(100, 1000, o).Should().BeLessThanOrEqualTo(100);
        CivicSupportModel.UpdateMomentum(0, -1000, o).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ActionPoints_HigherFit_YieldsMorePoints()
    {
        var o = Opts();
        var strong = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 1.0, salience: 1.0, momentum: 50, o);
        var weak = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 0.0, salience: 1.0, momentum: 50, o);
        strong.Should().BeGreaterThan(weak);
    }

    [Fact]
    public void ActionPoints_HigherSalience_YieldsMorePoints()
    {
        var o = Opts();
        var hot = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 0.5, salience: 1.0, momentum: 50, o);
        var cold = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 0.5, salience: 0.2, momentum: 50, o);
        hot.Should().BeGreaterThan(cold);
    }

    [Fact]
    public void ActionPoints_OffBrand_CanBeDamped()
    {
        var o = Opts();
        var onBrand = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 0.8, salience: 0.8, momentum: 50, o);
        var offBrand = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: -0.8, salience: 0.8, momentum: 50, o);
        offBrand.Should().BeLessThan(onBrand);
    }

    [Fact]
    public void ActionPoints_HigherMomentum_Amplifies()
    {
        var o = Opts();
        var hi = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 0.5, salience: 0.8, momentum: 90, o);
        var lo = CivicSupportModel.ActionPoints(CivicCampaignActionType.PublishPost, fit: 0.5, salience: 0.8, momentum: 20, o);
        hi.Should().BeGreaterThan(lo);
    }

    [Fact]
    public void OpponentDelta_OrderedByDifficulty()
    {
        var o = Opts();
        var easy = CivicSupportModel.OpponentDelta(CivicCampaignDifficulty.Easy, 0, 50, 0, 1.0, o);
        var normal = CivicSupportModel.OpponentDelta(CivicCampaignDifficulty.Normal, 0, 50, 0, 1.0, o);
        var hard = CivicSupportModel.OpponentDelta(CivicCampaignDifficulty.Hard, 0, 50, 0, 1.0, o);
        easy.Should().BeLessThan(normal);
        normal.Should().BeLessThan(hard);
    }

    [Fact]
    public void OpponentDelta_DefenseFactor_BluntsGains()
    {
        var o = Opts();
        var undefended = CivicSupportModel.OpponentDelta(CivicCampaignDifficulty.Normal, 0.5, 60, 0, 1.0, o);
        var defended = CivicSupportModel.OpponentDelta(CivicCampaignDifficulty.Normal, 0.5, 60, 0, 0.5, o);
        defended.Should().BeLessThan(undefended);
    }

    [Fact]
    public void ApplyAndNormalize_SumsToOneHundred()
    {
        var current = new[] { 25.0, 25.0, 25.0, 25.0 };
        var deltas = new[] { 10.0, -2.0, 0.0, 1.0 };
        var result = CivicSupportModel.ApplyAndNormalize(current, deltas);
        result.Sum().Should().BeApproximately(100.0, 1e-6);
        result.Should().OnlyContain(x => x > 0);
    }

    [Fact]
    public void ApplyAndNormalize_PositiveDelta_IncreasesThatShare()
    {
        var current = new[] { 50.0, 50.0 };
        var deltas = new[] { 10.0, 0.0 };
        var result = CivicSupportModel.ApplyAndNormalize(current, deltas);
        result[0].Should().BeGreaterThan(50);
        result[1].Should().BeLessThan(50);
    }

    [Fact]
    public void ApplyAndNormalize_NeverGoesNegative()
    {
        var current = new[] { 10.0, 90.0 };
        var deltas = new[] { -1000.0, 0.0 }; // huge negative shouldn't produce a negative share
        var result = CivicSupportModel.ApplyAndNormalize(current, deltas);
        result.Should().OnlyContain(x => x >= 0);
        result.Sum().Should().BeApproximately(100.0, 1e-6);
    }

    [Fact]
    public void WinnerIndex_PicksHighestShare()
    {
        CivicSupportModel.WinnerIndex(new[] { 10.0, 40.0, 30.0, 20.0 }).Should().Be(1);
        CivicSupportModel.WinnerIndex(new[] { 55.0, 45.0 }).Should().Be(0);
    }

    [Fact]
    public void EvenShare_DividesEvenly()
    {
        CivicSupportModel.EvenShare(4).Should().BeApproximately(25.0, 1e-9);
        CivicSupportModel.EvenShare(5).Should().BeApproximately(20.0, 1e-9);
    }
}

public class CivicCampaignFitTests
{
    private static VirtualCandidate Candidate(params string[] plankTags) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        PlatformPlanks = plankTags.Select(t => new PlatformPlank
        {
            Id = Guid.NewGuid(),
            Title = $"Plank {t}",
            Body = "body",
            IssueTags = new[] { t },
        }).ToList(),
    };

    [Fact]
    public void IssueFit_OwnedIssue_IsPositive()
    {
        var c = Candidate("housing", "privacy");
        CivicCampaignFit.IssueFit(c, "housing").Should().BeGreaterThan(0);
    }

    [Fact]
    public void IssueFit_UnknownIssue_IsNegative()
    {
        var c = Candidate("housing");
        CivicCampaignFit.IssueFit(c, "foreign-policy").Should().BeLessThan(0);
    }

    [Fact]
    public void CandidateIssues_DeduplicatesTags()
    {
        var c = Candidate("housing", "housing", "privacy");
        CivicCampaignFit.CandidateIssues(c).Should().BeEquivalentTo(new[] { "housing", "privacy" });
    }
}

public class CivicSalienceTests
{
    private static VirtualCandidate WithIssues(params string[] tags) => new()
    {
        Id = Guid.NewGuid(),
        Name = "C",
        PlatformPlanks = tags.Select(t => new PlatformPlank { Id = Guid.NewGuid(), Title = t, Body = "b", IssueTags = new[] { t } }).ToList(),
    };

    [Fact]
    public void ForWeek_IsDeterministicForSameSeedAndWeek()
    {
        var race = new[] { WithIssues("a", "b", "c", "d", "e"), WithIssues("f", "g") };
        var first = CivicSalience.ForWeek(race, week: 2, seed: 123);
        var second = CivicSalience.ForWeek(race, week: 2, seed: 123);
        first.Should().Equal(second);
    }

    [Fact]
    public void ForWeek_ReturnsRequestedCount_WhenPoolLargeEnough()
    {
        var race = new[] { WithIssues("a", "b", "c", "d", "e", "f") };
        CivicSalience.ForWeek(race, week: 1, seed: 1, count: 3).Should().HaveCount(3);
    }

    [Fact]
    public void Weight_TopSalientIssue_IsHigherThanNonSalient()
    {
        var salient = new List<string> { "housing", "privacy", "jobs" };
        CivicSalience.Weight(salient, "housing").Should().BeGreaterThan(CivicSalience.Weight(salient, "tariffs"));
    }
}
