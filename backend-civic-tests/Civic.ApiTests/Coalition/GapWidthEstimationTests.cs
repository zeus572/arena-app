using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Curriculum;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 3.1 gate: the birth-time gap-width estimate correlates with the observed
/// difficulty-to-close in self-play (above chance) across a constructed ladder of
/// provisions. Pure — no DB, no LLM.
/// </summary>
public class GapWidthEstimationTests
{
    private static VersionPoint V(string id, IReadOnlyDictionary<string, string> pos) => new(id, pos);

    /// <summary>
    /// A ladder provision with k demanding corners (one per spectrum bucket). The base
    /// version sets each corner's key to a value that corner won't accept, so each must
    /// contribute a carve-out — more corners ⇒ wider gap ⇒ more bridging to close.
    /// </summary>
    private static (List<CoalitionAgent> agents, ProvisionLoopState state, VersionPoint baseV) BuildLadder(int k)
    {
        var basePos = new Dictionary<string, string>();
        for (var i = 0; i < k; i++) basePos[$"k{i}"] = "bad";
        var baseV = V("base", basePos);

        var agents = new List<CoalitionAgent>();
        for (var i = 0; i < k; i++)
        {
            var region = AcceptanceRegion.FromConstraints(($"k{i}", new[] { "good" }));
            agents.Add(new CoalitionAgent($"c{i}", $"b{i}", region)); // Medium default intensity
        }

        var spectrum = new ComposedSpectrum(Enumerable.Range(0, k).Select(i => $"b{i}"));
        var state = new ProvisionLoopState($"ladder-{k}", agents.Select(a => a.ToPlayer()),
            spectrum, lifetime: TimeSpan.FromDays(7), initialVersions: new[] { baseV });
        return (agents, state, baseV);
    }

    [Fact]
    public void EstimatedGapWidth_CorrelatesWithObservedClosureDifficulty()
    {
        var estimates = new List<double>();
        var observed = new List<double>();

        foreach (var k in new[] { 2, 3, 4, 5 })
        {
            var (agents, state, baseV) = BuildLadder(k);
            var estimate = GapWidthEstimator.EstimateAtBirth(agents, baseV);

            var initialVersionCount = state.Versions.Count;
            var result = new SelfPlayRunner().Run(state, agents);
            result.Outcome!.FinalState.Should().Be(ProvisionState.Passed, $"ladder k={k} is bridgeable");

            // Observed difficulty-to-close = how many carve-out versions the loop had to create.
            var amendmentsNeeded = state.Versions.Count - initialVersionCount;

            estimates.Add(estimate);
            observed.Add(amendmentsNeeded);
        }

        var rho = Spearman(estimates, observed);
        rho.Should().BeGreaterThan(0.5, "the birth gap estimate should predict observed closure difficulty above chance");
    }

    [Fact]
    public void Estimator_FlagsAnUnbridgeableProvision_AsWiderThanAnEasyOne()
    {
        var (easyAgents, _, easyBase) = BuildLadder(2);
        var easy = GapWidthEstimator.EstimateAtBirth(easyAgents, easyBase);

        // Unbridgeable: two corners with disjoint NonNegotiable positions on one key.
        var hardBase = V("base", new Dictionary<string, string> { ["scope"] = "large-only" });
        var hardAgents = new List<CoalitionAgent>
        {
            new("L", "left", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })),
                new Dictionary<string, AnswerIntensity> { ["scope"] = AnswerIntensity.NonNegotiable }),
            new("R", "right", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })),
                new Dictionary<string, AnswerIntensity> { ["scope"] = AnswerIntensity.NonNegotiable }),
        };
        var hard = GapWidthEstimator.EstimateAtBirth(hardAgents, hardBase);

        hard.Should().BeGreaterThan(easy, "a disjoint NonNegotiable provision is a wider gap than an easy bridgeable one");
    }

    // Spearman rank correlation (rank, then Pearson on ranks).
    private static double Spearman(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        var ra = Ranks(a);
        var rb = Ranks(b);
        double n = a.Count, sa = ra.Sum(), sb = rb.Sum();
        double sab = ra.Zip(rb, (x, y) => x * y).Sum();
        double saa = ra.Sum(x => x * x), sbb = rb.Sum(x => x * x);
        var num = n * sab - sa * sb;
        var den = Math.Sqrt((n * saa - sa * sa) * (n * sbb - sb * sb));
        return den == 0 ? 0 : num / den;
    }

    private static double[] Ranks(IReadOnlyList<double> xs)
    {
        var idx = Enumerable.Range(0, xs.Count).OrderBy(i => xs[i]).ToArray();
        var ranks = new double[xs.Count];
        for (var r = 0; r < idx.Length; r++) ranks[idx[r]] = r + 1;
        return ranks;
    }
}
