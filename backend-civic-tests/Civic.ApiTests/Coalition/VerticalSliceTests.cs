using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2.4 gate — THE VERTICAL SLICE (de-risk milestone). A bridgeable agent set
/// drives one provision to PASSED autonomously via a sensible amendment; the
/// distance signal shrinks over the run; the passed plank lands in the outcome
/// record. Fully pure self-play — no DB, no LLM.
/// </summary>
public class VerticalSliceTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static CoalitionAgent Agent(string id, string bucket, params (string key, string[] labels)[] region) =>
        new(id, bucket, AcceptanceRegion.FromConstraints(region));

    private static void AssertNonIncreasing(IReadOnlyList<double> xs)
    {
        for (var i = 1; i < xs.Count; i++)
            xs[i].Should().BeLessThanOrEqualTo(xs[i - 1] + 1e-9, $"distance must not increase (step {i})");
    }

    [Fact]
    public void BridgeablePair_DrivesToPassed_DistanceShrinks_PlankRecorded()
    {
        var m = Agent("M", "left", ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }));
        var p = Agent("P", "right", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }));

        var state = new ProvisionLoopState("slice-pair", new[] { m.ToPlayer(), p.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

        var result = new SelfPlayRunner().Run(state, new[] { m, p });

        // Reached a sane coalition autonomously.
        state.State.Should().Be(ProvisionState.Passed);
        result.Outcome!.FinalState.Should().Be(ProvisionState.Passed);

        // ...via the sensible carve-out (grandfather exemption).
        result.Outcome.Plank!.Positions["gf"].Should().Be("exempt");
        result.Outcome.Plank.Positions["scope"].Should().Be("large-only");
        result.Outcome.Signers.Should().BeEquivalentTo(new[] { "M", "P" });
        result.Outcome.Specificity.Should().BeGreaterThanOrEqualTo(1, "the plank has teeth");
        result.Outcome.MovedSigners.Should().BeGreaterThanOrEqualTo(1, "at least one signer bargained in");
        result.Outcome.Breadth!.CoveredBuckets.Should().Be(2, "the coalition spans the spectrum");

        // The distance signal moved: shrank over the run and ended at zero.
        AssertNonIncreasing(result.DistanceHistory);
        result.DistanceHistory[^1].Should().Be(0.0);
        result.DistanceHistory[0].Should().BeGreaterThan(0.0);
        result.Stalled.Should().BeFalse();
    }

    [Fact]
    public void ThreeAgents_BridgeableViaOneCarveOut_PassWithFullSpectrumBreadth()
    {
        // M (left) and P (right) are the bridgeable corners; X (center) is a flexible middle.
        var m = Agent("M", "left", ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }));
        var x = Agent("X", "center", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt", "none" }));
        var p = Agent("P", "right", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }));

        var state = new ProvisionLoopState("slice-trio", new[] { m.ToPlayer(), x.ToPlayer(), p.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "center", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

        var result = new SelfPlayRunner().Run(state, new[] { m, x, p });

        state.State.Should().Be(ProvisionState.Passed);
        result.Outcome!.Plank!.Positions["gf"].Should().Be("exempt");
        result.Outcome.Signers.Should().BeEquivalentTo(new[] { "M", "X", "P" });
        result.Outcome.Breadth!.CoveredBuckets.Should().Be(3, "the passed coalition spans the full composed spectrum");
        AssertNonIncreasing(result.DistanceHistory);
        result.DistanceHistory[^1].Should().Be(0.0);
    }
}
