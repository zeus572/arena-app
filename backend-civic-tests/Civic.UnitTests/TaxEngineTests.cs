using Civic.API.Services.TaxModel;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Phase 1 gate — golden-value tests for the deterministic tax engine (§6).
///
/// NOTE ON THE SPEC'S §6 FEDERAL INCOME-TAX COLUMN: the spec instructs us to treat
/// the §3 brackets/standard-deduction as the source of truth and to "compute exactly."
/// Three of the four §6 income-tax figures are internally inconsistent with those §3
/// brackets (a correct marginal walk over the genuine 2025 single brackets — cumulative
/// bases $1,192.50 / $5,578.50 / $17,651 / $40,199 — yields different numbers):
///
///   income   taxable     §6 income tax   correct (per §3 brackets)
///   $30,000  $14,250     $1,471.50       $1,471.50   (matches)
///   $60,000  $44,250     $5,065.50       $5,071.50
///   $100,000 $84,250     $13,966.50      $13,449.00
///   $250,000 $234,250    $51,648.50      $52,023.00
///
/// Every FICA value and every state spot-check in §6 is correct. We implement the
/// engine per §3 (the declared source of truth) and assert the CORRECT computed
/// values here rather than baking in the erroneous §6 figures. The SS cap, Medicare,
/// and Additional-Medicare assertions below match §6 verbatim.
/// </summary>
public class TaxEngineTests
{
    private const double Cent = 0.005; // tolerance: half a cent

    public static IEnumerable<object[]> FederalGolden() => new[]
    {
        // income, incomeTax, ss, medicare, addlMed, fica, total
        new object[] { 30_000d, 1_471.50, 1_860.00, 435.00, 0.00, 2_295.00, 3_766.50 },
        new object[] { 60_000d, 5_071.50, 3_720.00, 870.00, 0.00, 4_590.00, 9_661.50 },
        new object[] { 100_000d, 13_449.00, 6_200.00, 1_450.00, 0.00, 7_650.00, 21_099.00 },
        new object[] { 250_000d, 52_023.00, 10_918.20, 3_625.00, 450.00, 14_993.20, 67_016.20 },
    };

    [Theory]
    [MemberData(nameof(FederalGolden))]
    public void ComputeFederal_Single_ReproducesGoldenRows(
        double income, double incomeTax, double ss, double medicare, double addlMed, double fica, double total)
    {
        var r = TaxEngine.ComputeFederal(income, FilingStatus.Single);

        r.IncomeTax.Should().BeApproximately(incomeTax, Cent);
        r.SocialSecurity.Should().BeApproximately(ss, Cent);
        r.Medicare.Should().BeApproximately(medicare, Cent);
        r.AddlMedicare.Should().BeApproximately(addlMed, Cent);
        r.Fica.Should().BeApproximately(fica, Cent);
        r.Total.Should().BeApproximately(total, Cent);
    }

    [Fact]
    public void ComputeFederal_SocialSecurity_CapsAtWageBase()
    {
        // At $250k single, SS is capped: 176,100 × 6.2% = $10,918.20 (§6 spot-check rule).
        var r = TaxEngine.ComputeFederal(250_000, FilingStatus.Single);
        r.SocialSecurity.Should().BeApproximately(176_100 * 0.062, Cent);

        // Far above the cap, SS does not keep growing.
        var high = TaxEngine.ComputeFederal(2_000_000, FilingStatus.Single);
        high.SocialSecurity.Should().BeApproximately(176_100 * 0.062, Cent);
    }

    [Fact]
    public void ComputeFederal_AdditionalMedicare_Single_AppliesOver200k()
    {
        // (250,000 − 200,000) × 0.9% = $450 (§6 spot-check rule).
        var r = TaxEngine.ComputeFederal(250_000, FilingStatus.Single);
        r.AddlMedicare.Should().BeApproximately(450.00, Cent);

        // Just under the threshold → no additional Medicare.
        var under = TaxEngine.ComputeFederal(200_000, FilingStatus.Single);
        under.AddlMedicare.Should().Be(0);
    }

    [Fact]
    public void ComputeFederal_AdditionalMedicare_Mfj_AppliesOver250k()
    {
        var r = TaxEngine.ComputeFederal(300_000, FilingStatus.MarriedFilingJointly);
        r.AddlMedicare.Should().BeApproximately((300_000 - 250_000) * 0.009, Cent);
    }

    [Fact]
    public void ComputeFederal_ZeroAndBelowDeduction_ProducesNoIncomeTax()
    {
        TaxEngine.ComputeFederal(0, FilingStatus.Single).Total.Should().Be(0);
        // Income below the standard deduction → taxable 0 → no income tax (FICA still applies).
        var low = TaxEngine.ComputeFederal(10_000, FilingStatus.Single);
        low.IncomeTax.Should().Be(0);
        low.Fica.Should().BeApproximately(10_000 * (0.062 + 0.0145), Cent);
    }

    [Theory]
    [InlineData("TX", 0.00, 3_280.00, 5_056.00, 8_336.00)]   // §6: no income tax
    [InlineData("PA", 3_070.00, 2_536.00, 4_321.00, 9_927.00)] // §6: flat 3.07%, no std ded
    public void ComputeState_Single100k_ReproducesGoldenSpotChecks(
        string code, double incomeTax, double salesTax, double propertyTax, double total)
    {
        var profile = StateProfiles.Find(code)!;
        var r = TaxEngine.ComputeState(100_000, profile);

        r.IncomeTax.Should().BeApproximately(incomeTax, Cent);
        r.SalesTax.Should().BeApproximately(salesTax, Cent);
        r.PropertyTax.Should().BeApproximately(propertyTax, Cent);
        r.Total.Should().BeApproximately(total, Cent);
    }

    [Fact]
    public void ComputeState_California100k_MatchesSpecFormula()
    {
        var ca = StateProfiles.Find("CA")!;
        var r = TaxEngine.ComputeState(100_000, ca);

        // §6: salesTax = 100,000 × 0.40 × 0.0882 = $3,528; propertyTax = 100,000 × 4.5 × 0.0068 = $3,060.
        r.SalesTax.Should().BeApproximately(3_528.00, Cent);
        r.PropertyTax.Should().BeApproximately(3_060.00, Cent);

        // incomeTax = progressive over CA brackets on (100,000 − 5,540) = 94,460.
        r.IncomeTax.Should().BeApproximately(5_327.142, 0.01);
    }

    [Fact]
    public void Combine_FederalAndStateShares_SumToOne()
    {
        foreach (var profile in StateProfiles.All)
        {
            var federal = TaxEngine.ComputeFederal(100_000, FilingStatus.Single);
            var state = TaxEngine.ComputeState(100_000, profile);
            var combined = TaxEngine.Combine(100_000, federal, state);

            combined.Total.Should().BeApproximately(federal.Total + state.Total, Cent);
            (combined.FederalShare + combined.StateShare).Should().BeApproximately(1.0, 1e-9);
            combined.FederalShare.Should().BeGreaterThan(0).And.BeLessThan(1);
        }
    }

    [Fact]
    public void Progressive_FederalShareRisesWithIncome()
    {
        // The scaling-table caption's claim (§7.4): federal share rises with income
        // because federal income tax is steeply progressive while sales/property are flat.
        var tx = StateProfiles.Find("TX")!;
        double ShareAt(double income) =>
            TaxEngine.Combine(income,
                TaxEngine.ComputeFederal(income, FilingStatus.Single),
                TaxEngine.ComputeState(income, tx)).FederalShare;

        ShareAt(750_000).Should().BeGreaterThan(ShareAt(30_000));
    }

    [Fact]
    public void IncomeRules_NoneFlatProgressive_BehaveAsSpecified()
    {
        TaxEngine.ComputeStateIncomeTax(100_000, IncomeRule.None()).Should().Be(0);
        TaxEngine.ComputeStateIncomeTax(100_000, IncomeRule.Flat(0.05, 10_000))
            .Should().BeApproximately(90_000 * 0.05, Cent);
        // Flat with income below std deduction clamps at 0.
        TaxEngine.ComputeStateIncomeTax(5_000, IncomeRule.Flat(0.05, 10_000)).Should().Be(0);
    }

    [Fact]
    public void StateProfiles_ShipsAllFiftyStates()
    {
        StateProfiles.All.Should().HaveCount(50);
        StateProfiles.All.Select(s => s.Code).Should().OnlyHaveUniqueItems();
        StateProfiles.All.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Notes));
        StateProfiles.All.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.IncomeSummary));
        StateProfiles.All.Should().OnlyContain(s => s.ConsumptionShare == 0.40);
        // The 8 fully-verified spotlight states must remain present.
        StateProfiles.All.Select(s => s.Code).Should()
            .Contain(new[] { "CA", "NY", "TX", "FL", "WA", "CO", "PA", "IL" });
    }

    [Fact]
    public void StateProfiles_EveryState_ComputesWithoutError()
    {
        foreach (var profile in StateProfiles.All)
        {
            var r = TaxEngine.ComputeState(100_000, profile);
            r.Total.Should().BeGreaterOrEqualTo(0);
            double.IsNaN(r.Total).Should().BeFalse($"{profile.Code} produced NaN");
        }
    }
}
