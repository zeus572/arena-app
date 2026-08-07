using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The Money Trail invariants (PRD 05 §9).
///
/// These are not arithmetic tests. The Money Trail exists because coverage routinely
/// describes a REQUEST using verbs that belong to an OUTLAY, and every assertion here is
/// about refusing to make that mistake in code.
/// </summary>
public class MoneyLadderTests
{
    private static MoneyItem Item(
        MoneyItemKind kind = MoneyItemKind.GovernmentOutlay,
        decimal amount = 1_000m,
        int fyStart = 2026,
        int? fyEnd = null) => new()
    {
        Id = Guid.NewGuid(),
        Slug = $"item-{Guid.NewGuid():N}",
        Title = "A funding item",
        Kind = kind,
        AmountUsd = amount,
        FiscalYearStart = fyStart,
        FiscalYearEnd = fyEnd ?? fyStart,
        WhatThisDoesNotMean = "It does not mean the money has been spent.",
    };

    // ---------------------------------------------------------------- the ladder

    [Fact]
    public void TheLadderHasExactlyFiveRungs()
    {
        // PRD 05 also names Allocated, Estimated and Economic effect. Those are not rungs —
        // Allocated folds into Appropriated, and the other two are MoneyItemKind values.
        // Keeping them out is what makes "never summed" mechanically true.
        MoneyMath.Ladder.Should().HaveCount(5);
        MoneyMath.Ladder.Should().ContainInOrder(
            FundingStage.Requested,
            FundingStage.Authorized,
            FundingStage.Appropriated,
            FundingStage.Obligated,
            FundingStage.Spent);

        Enum.GetValues<FundingStage>().Should().HaveCount(5);
    }

    [Fact]
    public void BuildLadder_AlwaysWritesAllFiveRungsIncludingTheEmptyOnes()
    {
        // The guarantee behind "empty stages render as visible empty, never omitted".
        // An item with three rows would hide that two stages are empty — usually the story.
        var rows = MoneyMath.BuildLadder(
            Guid.NewGuid(),
            new Dictionary<FundingStage, decimal> { [FundingStage.Requested] = 4_100m });

        rows.Should().HaveCount(5);
        rows.Select(r => r.Stage).Should().BeEquivalentTo(MoneyMath.Ladder);
        rows.Count(r => r.Applicability == StageApplicability.EmptyPending).Should().Be(4);
    }

    [Fact]
    public void AStageThatDoesNotApply_SaysWhy()
    {
        var rows = MoneyMath.BuildLadder(
            Guid.NewGuid(),
            new Dictionary<FundingStage, decimal> { [FundingStage.Appropriated] = 500m },
            new Dictionary<FundingStage, string>
            {
                [FundingStage.Authorized] = "Mandatory spending needs no separate authorization.",
            });

        var authorized = rows.Single(r => r.Stage == FundingStage.Authorized);
        authorized.Applicability.Should().Be(StageApplicability.NotApplicable);
        authorized.NotApplicableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CurrentStage_IsTheHighestRungActuallyReached()
    {
        var rows = MoneyMath.BuildLadder(
            Guid.NewGuid(),
            new Dictionary<FundingStage, decimal>
            {
                [FundingStage.Requested] = 4_100m,
                [FundingStage.Appropriated] = 3_000m,
            });

        MoneyMath.CurrentStage(rows).Should().Be(FundingStage.Appropriated);
    }

    [Fact]
    public void CurrentStage_OfAnUntouchedItemIsRequested()
    {
        var rows = MoneyMath.BuildLadder(Guid.NewGuid(), new Dictionary<FundingStage, decimal>());

        MoneyMath.CurrentStage(rows).Should().Be(FundingStage.Requested);
    }

    // ---------------------------------------------------------------- refusing to sum

    [Fact]
    public void SummingAcrossStages_Throws()
    {
        // The single most common error in budget coverage: the same dollars appear at
        // Requested, then Appropriated, then Obligated, then Spent as they move. Adding the
        // rungs double-counts them, and the result looks entirely plausible.
        var rows = MoneyMath.BuildLadder(
            Guid.NewGuid(),
            new Dictionary<FundingStage, decimal>
            {
                [FundingStage.Requested] = 4_100m,
                [FundingStage.Appropriated] = 3_000m,
            });

        var act = () => MoneyMath.TotalAcrossStages(rows);

        act.Should().Throw<MoneySumException>()
            .WithMessage("*double-counts*");
    }

    [Fact]
    public void TotalAtStage_RequiresYouToNameTheStage()
    {
        var rows = MoneyMath.BuildLadder(
                Guid.NewGuid(),
                new Dictionary<FundingStage, decimal> { [FundingStage.Appropriated] = 3_000m })
            .Concat(MoneyMath.BuildLadder(
                Guid.NewGuid(),
                new Dictionary<FundingStage, decimal> { [FundingStage.Appropriated] = 1_500m }))
            .ToList();

        MoneyMath.TotalAtStage(rows, FundingStage.Appropriated).Should().Be(4_500m);
        MoneyMath.TotalAtStage(rows, FundingStage.Spent).Should().Be(0m);
    }

    [Fact]
    public void SummingMixedKinds_Throws()
    {
        // Government outlays and modelled economic effects are different quantities and
        // live in separate halves of the page. Adding them produces nonsense.
        var items = new[]
        {
            Item(MoneyItemKind.GovernmentOutlay),
            Item(MoneyItemKind.ModeledEconomicEffect),
        };

        var act = () => MoneyMath.TotalOutlays(items);

        act.Should().Throw<MoneySumException>().WithMessage("*separate halves*");
    }

    [Fact]
    public void TotallingModelledEffects_Throws()
    {
        var act = () => MoneyMath.TotalOutlays(new[] { Item(MoneyItemKind.ModeledEconomicEffect) });

        act.Should().Throw<MoneySumException>().WithMessage("*ranges and assumptions*");
    }

    [Fact]
    public void TotallingEstimates_Throws()
    {
        var act = () => MoneyMath.TotalOutlays(new[] { Item(MoneyItemKind.Estimate) });

        act.Should().Throw<MoneySumException>();
    }

    [Fact]
    public void TotallingOutlays_Works()
    {
        MoneyMath.TotalOutlays(new[] { Item(amount: 100m), Item(amount: 250m) })
            .Should().Be(350m);
    }

    [Fact]
    public void TotallingNothing_IsZeroNotAnError()
    {
        MoneyMath.TotalOutlays(Array.Empty<MoneyItem>()).Should().Be(0m);
    }

    // ---------------------------------------------------------------- periods

    [Fact]
    public void AMultiYearTotal_CannotBeRenderedAsAnAnnualAmount()
    {
        // Dividing by the number of years assumes an even spread that appropriations do not
        // have, and the quotient would be presented as a fact.
        var tenYear = Item(fyStart: 2026, fyEnd: 2035);

        var act = () => MoneyMath.AnnualAmount(tenYear);

        act.Should().Throw<MoneySumException>().WithMessage("*even spread*");
    }

    [Fact]
    public void ASingleYearAmount_IsItsOwnAnnualAmount()
    {
        MoneyMath.AnnualAmount(Item(amount: 900m, fyStart: 2026)).Should().Be(900m);
    }

    [Fact]
    public void AMultiYearPeriodLabel_SaysItIsNotPerYear()
    {
        // A ten-year score shown without its period reads as an annual figure and overstates
        // it by an order of magnitude.
        var label = MoneyMath.PeriodLabel(Item(fyStart: 2026, fyEnd: 2035));

        label.Should().Contain("10 years");
        label.Should().Contain("not per year");
    }

    [Fact]
    public void ASingleYearPeriodLabel_IsJustTheYear()
    {
        MoneyMath.PeriodLabel(Item(fyStart: 2026)).Should().Be("FY2026");
    }

    // ---------------------------------------------------------------- verbs

    [Fact]
    public void OnlySpentMayBeCalledSpent()
    {
        // The room's entire thesis: coverage saying the government "is spending" a requested
        // figure describes the first rung as if it were the last.
        MoneyMath.CanSaySpent(FundingStage.Spent).Should().BeTrue();

        foreach (var stage in MoneyMath.Ladder.Where(s => s != FundingStage.Spent))
        {
            MoneyMath.CanSaySpent(stage).Should().BeFalse("{0} is not spending", stage);
        }
    }

    [Fact]
    public void EveryStageHasATrueVerb()
    {
        foreach (var stage in Enum.GetValues<FundingStage>())
        {
            MoneyMath.VerbFor(stage).Should().NotBeNullOrWhiteSpace();
        }

        MoneyMath.VerbFor(FundingStage.Requested).Should().NotContain("spent");
        MoneyMath.VerbFor(FundingStage.Appropriated).Should().NotContain("spent");
    }

    // ---------------------------------------------------------------- required fields

    [Fact]
    public void WhatThisDoesNotMean_IsRequiredOnTheModel()
    {
        // Design 1s puts it in an inverse panel, not a tooltip. An item that cannot say what
        // it does not mean has not been understood well enough to publish.
        var prop = typeof(MoneyItem).GetProperty(nameof(MoneyItem.WhatThisDoesNotMean))!;

        prop.GetCustomAttributes(
                typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void ARejectedComparison_KeepsItsReason()
    {
        // Design 1s deliberately shows a struck-through per-capita comparison with the
        // reason it was refused. That is content, not a code comment.
        var item = Item();
        item.Comparisons.Add(new MoneyComparison
        {
            Text = "About $12 per household.",
            Accepted = false,
            RejectionReason = "Per-capita framing on a one-time request implies a recurring "
                            + "household cost that does not exist.",
        });

        var rejected = item.Comparisons.Single(c => !c.Accepted);
        rejected.RejectionReason.Should().NotBeNullOrWhiteSpace();
    }
}
