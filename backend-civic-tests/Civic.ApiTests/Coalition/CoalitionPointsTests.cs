using Civic.API.Models;
using Civic.API.Services.Coalition.Product;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>The points economy: currencies, the agree-vs-amend asymmetry, quality gating, and
/// diminishing returns with a daily cap. Pure unit tests.</summary>
public class CoalitionPointsTests
{
    [Fact]
    public void BareCoSign_WorthFarLessThan_Amend()
    {
        CoalitionPoints.BasePoints(CoalitionActType.CoSign)
            .Should().BeLessThan(CoalitionPoints.BasePoints(CoalitionActType.Amend));
    }

    [Fact]
    public void Currencies_AreMappedCorrectly()
    {
        CoalitionPoints.Currency(CoalitionActType.ReactionWithReason).Should().Be("reasoning");
        CoalitionPoints.Currency(CoalitionActType.Position).Should().Be("reasoning");
        CoalitionPoints.Currency(CoalitionActType.WritePlank).Should().Be("scarce");
        CoalitionPoints.Currency(CoalitionActType.AuthorProvision).Should().Be("scarce");
        CoalitionPoints.Currency(CoalitionActType.CoalitionPassReward).Should().Be("scarce");
    }

    [Fact]
    public void QualityGated_OnlyForMacroJudgedActs()
    {
        CoalitionPoints.QualityGated(CoalitionActType.Steelman).Should().BeTrue();
        CoalitionPoints.QualityGated(CoalitionActType.Longform).Should().BeTrue();
        CoalitionPoints.QualityGated(CoalitionActType.Position).Should().BeFalse();
        CoalitionPoints.QualityGated(CoalitionActType.CoSign).Should().BeFalse();
    }

    [Fact]
    public void Diminishing_DecaysWithinDay_AndRespectsCap()
    {
        CoalitionPoints.ApplyDiminishing(10, priorReasoningActsToday: 0, reasoningEarnedToday: 0).Should().Be(10);
        CoalitionPoints.ApplyDiminishing(10, priorReasoningActsToday: 1, reasoningEarnedToday: 10).Should().Be(6); // 10*0.6
        CoalitionPoints.ApplyDiminishing(10, priorReasoningActsToday: 2, reasoningEarnedToday: 16).Should().Be(4); // 10*0.36→4
        // Cap: only 2 points of headroom left under the daily cap.
        CoalitionPoints.ApplyDiminishing(10, priorReasoningActsToday: 0, reasoningEarnedToday: CoalitionPoints.DailyReasoningCap - 2)
            .Should().Be(2);
    }
}
