using Civic.API.Models;
using Civic.API.Services.Coalition;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Human;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2H.2 gate: a mixed agent+human provision reaches a coalition, and the
/// broadcast-only safety invariant (A8) holds — no mechanic creates a private
/// channel between two users. Pure — no DB, no LLM.
/// </summary>
public class MixedPlayTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    [Fact]
    public void MixedAgentAndHuman_ReachesCoalition()
    {
        // M is a HUMAN (left corner); P is an AGENT (right corner, the bridgeable pair).
        var mHuman = new PlayerGeometry("M",
            AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" })), "left");
        var pAgent = new CoalitionAgent("P", "right",
            AcceptanceRegion.FromConstraints(("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" })));

        var vBridge = V("vBridge", ("scope", "large-only"), ("gf", "exempt"));
        var state = new ProvisionLoopState("mixed", new[] { mHuman, pAgent.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

        // The human's daily acts: engage, decline the base, (a broadcast reaction), then co-sign
        // the bridge once the agent has tabled it.
        var humanScript = new HumanAct[]
        {
            new HumanPosition("M", "for, narrow", AnswerIntensity.Medium),
            new HumanDecline("M", state.Versions[0], AnswerIntensity.Low, "prefer a carve-out"),
            new HumanReactionWithReason("M", "open to a grandfather clause"),
            new HumanCoSign("M", vBridge, AnswerIntensity.Medium),
        };

        var result = new MixedPlayRunner().Run(state, new[] { pAgent }, humanScript);

        result.Outcome!.FinalState.Should().Be(ProvisionState.Passed, "the mixed loop reaches a coalition");
        result.Outcome.Plank!.Positions["gf"].Should().Be("exempt");
        result.Outcome.Signers.Should().BeEquivalentTo(new[] { "M", "P" });
        result.Outcome.Breadth!.CoveredBuckets.Should().Be(2);
    }

    [Fact]
    public void BroadcastOnlyInvariant_HoldsAcrossEveryCoalitionAct()
    {
        // The act surface must never carry a second-user recipient (= a private channel).
        CoalitionSafety.AllActsBroadcastOnly(out var violations).Should().BeTrue(
            "no coalition act may open a private channel between two users; offenders: {0}",
            string.Join(", ", violations));

        // Sanity: the reflective scan actually found the act types it is guarding.
        var actTypes = CoalitionSafety.CoalitionActTypes().Select(t => t.Name).ToList();
        actTypes.Should().Contain(new[]
        {
            nameof(TakePositionAct), nameof(ProposeAmendmentAct), nameof(CastAcceptanceAct),
            nameof(HumanPosition), nameof(HumanCoSign), nameof(HumanReactionWithReason),
        });

        // And the guard would catch a violation if one were introduced.
        CoalitionSafety.IsBroadcastOnly(typeof(PrivateChannelActProbe)).Should().BeFalse();
    }

    // A probe type representing the kind of act the invariant forbids (a DM to another user).
    private sealed record PrivateChannelActProbe(string UserId, string RecipientUserId);
}
