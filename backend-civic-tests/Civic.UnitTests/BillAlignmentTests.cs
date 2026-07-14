using Civic.API.Mapping;
using Civic.API.Services.Bills;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class BillAlignmentTests
{
    [Theory]
    [InlineData(0.6, 0.7, BillAlignment.Aligned)]     // same sign, both strong
    [InlineData(-0.6, -0.4, BillAlignment.Aligned)]   // same sign (negative)
    [InlineData(0.6, -0.5, BillAlignment.Tension)]    // opposite signs
    [InlineData(-0.6, 0.5, BillAlignment.Tension)]    // opposite signs
    [InlineData(0.05, 0.8, BillAlignment.Mixed)]      // user neutral
    [InlineData(0.8, 0.05, BillAlignment.Mixed)]      // bill neutral
    public void Classify_ReturnsExpected(double user, double bill, string expected)
    {
        BillAlignment.Classify(user, bill).Should().Be(expected);
    }

    [Fact]
    public void Closeness_IsOneWhenIdentical_ZeroWhenOpposite()
    {
        BillAlignment.Closeness(0.5, 0.5).Should().Be(1.0);
        BillAlignment.Closeness(-1.0, 1.0).Should().Be(0.0);
        BillAlignment.Closeness(0.0, 1.0).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void OverallPercent_NullWhenNoSharedAxes()
    {
        BillAlignment.OverallPercent(System.Array.Empty<(double, double, double)>()).Should().BeNull();
    }

    [Fact]
    public void OverallPercent_PerfectAgreementIs100()
    {
        var pairs = new[] { (0.8, 0.8, 1.0), (-0.5, -0.5, 0.9) };
        BillAlignment.OverallPercent(pairs).Should().Be(100);
    }

    [Fact]
    public void OverallPercent_OppositeEndsIsZero()
    {
        var pairs = new[] { (1.0, -1.0, 1.0) };
        BillAlignment.OverallPercent(pairs).Should().Be(0);
    }

    [Fact]
    public void OverallPercent_WeightsByBillConfidence()
    {
        // A high-confidence disagreement should pull the score down more than a
        // low-confidence one relative to a perfectly-aligned axis.
        var lowConfClash = BillAlignment.OverallPercent(new[] { (1.0, 1.0, 1.0), (1.0, -1.0, 0.1) });
        var highConfClash = BillAlignment.OverallPercent(new[] { (1.0, 1.0, 1.0), (1.0, -1.0, 1.0) });
        highConfClash.Should().BeLessThan(lowConfClash!.Value);
    }
}

public class BillMappingsTests
{
    [Fact]
    public void Identifier_FormatsFederalWithOrdinalCongress()
    {
        BillMappings.Identifier("HR", 1, 118).Should().Be("HR 1 · 118th Congress");
        BillMappings.Identifier("S", 2043, 118).Should().Be("S 2043 · 118th Congress");
    }

    [Fact]
    public void Identifier_OmitsCongressWhenZero()
    {
        BillMappings.Identifier("HR", 1, 0).Should().Be("HR 1");
    }

    [Fact]
    public void Teaser_PrefersSynthesisSummary_AndTruncates()
    {
        BillMappings.Teaser("short synth", "source").Should().Be("short synth");
        BillMappings.Teaser(null, "the source summary").Should().Be("the source summary");
        BillMappings.Teaser("", "fallback").Should().Be("fallback");

        var longText = new string('a', 400);
        var teaser = BillMappings.Teaser(longText, "x");
        teaser.Length.Should().BeLessThan(longText.Length);
        teaser.Should().EndWith("…");
    }
}
