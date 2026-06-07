using Civic.API.Services.Coalition.Geometry;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Multi-axis / per-axis breadth (doc 06): a coalition must span EACH relevant axis;
/// incomplete cross-axis coverage is flagged as a fork trigger. Pure unit tests.
/// </summary>
public class PerAxisBreadthTests
{
    private static readonly MultiAxisSpectrum Spectrum = new(new Dictionary<string, IEnumerable<string>>
    {
        ["econ"] = new[] { "left", "center", "right" },
        ["social"] = new[] { "a", "b", "c" },
    });

    private static IReadOnlyDictionary<string, string> P(string econ, string social) =>
        new Dictionary<string, string> { ["econ"] = econ, ["social"] = social };

    [Fact]
    public void FullCoverageBothAxes_IsBroad_NotAForkTrigger()
    {
        var signers = new[] { P("left", "a"), P("center", "b"), P("right", "c") };
        var r = PerAxisBreadthCalculator.Breadth(signers, Spectrum);

        r.PerAxis.Should().OnlyContain(a => a.Covered == 3);
        r.OverallCoverage.Should().Be(1.0);
        r.IncompleteCrossAxisCoverage.Should().BeFalse();
        r.ForkTrigger.Should().BeFalse();
    }

    [Fact]
    public void OneAxisBroad_OtherLagging_FlagsForkTrigger()
    {
        // econ fully covered; social stuck in one corner.
        var signers = new[] { P("left", "a"), P("center", "a"), P("right", "a") };
        var r = PerAxisBreadthCalculator.Breadth(signers, Spectrum);

        var econ = r.PerAxis.Single(a => a.Axis == "econ");
        var social = r.PerAxis.Single(a => a.Axis == "social");
        econ.Coverage.Should().Be(1.0);
        social.Coverage.Should().BeApproximately(1.0 / 3.0, 1e-9);
        social.Uncovered.Should().BeEquivalentTo(new[] { "b", "c" });

        r.OverallCoverage.Should().BeApproximately(1.0 / 3.0, 1e-9, "overall = the worst-covered axis");
        r.IncompleteCrossAxisCoverage.Should().BeTrue("econ is broad but social lags — a natural fork trigger");
    }
}
