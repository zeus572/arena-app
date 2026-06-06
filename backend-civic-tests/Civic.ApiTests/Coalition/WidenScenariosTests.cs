using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2.5 gate: engineered self-play scenarios reproduce FORK and honest DEATH;
/// breadth scales with the spectrum spread of signers; and the over-breadth guard
/// bites (cheap low-intensity acceptances score below costly cross-spectrum ones).
/// Pure — no DB, no LLM.
/// </summary>
public class WidenScenariosTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static CoalitionAgent Agent(
        string id, string bucket,
        (string key, string[] labels)[] region,
        (string key, AnswerIntensity i)[]? intensities = null,
        AnswerIntensity def = AnswerIntensity.Medium) =>
        new(id, bucket, AcceptanceRegion.FromConstraints(region),
            intensities?.ToDictionary(x => x.key, x => x.i), def);

    // ---- engineered to FORK ----
    [Fact]
    public void Engineered_ThreeWay_Forks()
    {
        var spectrum = new ComposedSpectrum(new[] { "left", "center", "right" });
        var nn = new[] { ("scope", AnswerIntensity.NonNegotiable) };
        var campA = new[]
        {
            Agent("aL", "left", new[] { ("scope", new[] { "all" }) }, nn),
            Agent("aC", "center", new[] { ("scope", new[] { "all" }) }, nn),
            Agent("aR", "right", new[] { ("scope", new[] { "all" }) }, nn),
        };
        var campB = new[]
        {
            Agent("bL", "left", new[] { ("scope", new[] { "large-only" }) }, nn),
            Agent("bC", "center", new[] { ("scope", new[] { "large-only" }) }, nn),
            Agent("bR", "right", new[] { ("scope", new[] { "large-only" }) }, nn),
        };
        var agents = campA.Concat(campB).ToList();
        var state = new ProvisionLoopState("fork-scenario", agents.Select(a => a.ToPlayer()),
            spectrum, lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only")) });

        var result = new SelfPlayRunner().Run(state, agents);

        result.Outcome!.FinalState.Should().Be(ProvisionState.Forked);
        result.Outcome.ForkChildren.Should().HaveCount(2);
        result.Outcome.ForkChildren!.Select(c => c.BasinVersion.Positions["scope"])
            .Should().BeEquivalentTo(new[] { "all", "large-only" });
    }

    // ---- engineered to DIE honestly ----
    [Fact]
    public void Engineered_Unbridgeable_DiesAtDeadline()
    {
        var m = Agent("M", "left", new[] { ("scope", new[] { "large-only" }) }, new[] { ("scope", AnswerIntensity.NonNegotiable) });
        var p = Agent("P", "right", new[] { ("scope", new[] { "all" }) }, new[] { ("scope", AnswerIntensity.NonNegotiable) });
        var state = new ProvisionLoopState("die-scenario", new[] { m.ToPlayer(), p.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only")) });

        var result = new SelfPlayRunner().Run(state, new[] { m, p });

        result.Outcome!.FinalState.Should().Be(ProvisionState.Died);
        result.Outcome.DiedReason.Should().Contain("deadline");
    }

    // ---- breadth scales with the spectrum spread of signers ----
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Breadth_ScalesWithNumberOfSpectrumBucketsCovered(int n)
    {
        var buckets = Enumerable.Range(0, n).Select(i => $"b{i}").ToArray();
        var spectrum = new ComposedSpectrum(buckets);

        // n agents, each in a distinct bucket; all accept the gf=exempt bridge. The last is
        // the corner that needs the carve-out (rejects gf=none), forcing a real amendment.
        var agents = Enumerable.Range(0, n).Select(i =>
            Agent($"a{i}", buckets[i],
                i == n - 1
                    ? new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt" }) }
                    : new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }) }))
            .ToList();

        var state = new ProvisionLoopState($"scale-{n}", agents.Select(a => a.ToPlayer()),
            spectrum, lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

        var result = new SelfPlayRunner().Run(state, agents);

        result.Outcome!.FinalState.Should().Be(ProvisionState.Passed);
        result.Outcome.Breadth!.CoveredBuckets.Should().Be(n, "breadth scales with distinct buckets covered");
    }

    // ---- the over-breadth guard bites ----
    [Fact]
    public void OverBreadthGuard_CheapAcceptancesScoreBelowCostlyConcessions()
    {
        var spectrum = new ComposedSpectrum(new[] { "left", "center", "right" });
        var plank = V("plank", ("scope", "large-only"), ("gf", "exempt"));

        // Cheap coalition: "accept anything", low intensity everywhere -> minimal stake.
        var cheap = new[]
        {
            new CoalitionAgent("cL", "left", AcceptanceRegion.Unconstrained(), null, AnswerIntensity.Low),
            new CoalitionAgent("cC", "center", AcceptanceRegion.Unconstrained(), null, AnswerIntensity.Low),
            new CoalitionAgent("cR", "right", AcceptanceRegion.Unconstrained(), null, AnswerIntensity.Low),
        };

        // Costly coalition: each holds a high-intensity position on the resolved cruxes.
        var hi = new[] { ("scope", AnswerIntensity.High), ("gf", AnswerIntensity.High) };
        var costly = new[]
        {
            Agent("hL", "left", new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt" }) }, hi, AnswerIntensity.High),
            Agent("hC", "center", new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt" }) }, hi, AnswerIntensity.High),
            Agent("hR", "right", new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt" }) }, hi, AnswerIntensity.High),
        };

        // Per-signer stake: cheap accept-anything agents are worth the floor; costly ones high.
        CoalitionScorer.SignerStake(cheap[0], plank).Should().Be(1);
        CoalitionScorer.SignerStake(costly[0], plank).Should().Be(3);

        var cheapScore = CoalitionScorer.Score(plank, cheap, spectrum, movedSignerIds: Array.Empty<string>());
        var costlyScore = CoalitionScorer.Score(plank, costly, spectrum, movedSignerIds: costly.Select(a => a.UserId));

        // Identical breadth + specificity, but cost (and total) clearly favors the costly coalition.
        cheapScore.Breadth.Should().Be(costlyScore.Breadth);
        cheapScore.Specificity.Should().Be(costlyScore.Specificity);
        costlyScore.Cost.Should().BeGreaterThan(cheapScore.Cost);
        costlyScore.Total.Should().BeGreaterThan(cheapScore.Total, "the over-breadth guard bites: cheap mush scores below costly bridging");
    }
}
