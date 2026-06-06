using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2.2 gate: a known-bridgeable agent pair reports overlapping acceptance
/// after a sensible amendment; a known-unbridgeable pair never overlaps without one
/// of them casting a NonNegotiable (principled) decline. Pure unit tests — no DB,
/// no LLM (agents are constructed by hand, the plan's validation mode).
/// </summary>
public class AgentPolicyTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static CoalitionAgent Agent(
        string id, string bucket,
        (string key, string[] labels)[] region,
        (string key, AnswerIntensity i)[]? intensities = null) =>
        new(id, bucket, AcceptanceRegion.FromConstraints(region),
            intensities?.ToDictionary(x => x.key, x => x.i));

    // ---- bridgeable: overlap appears AFTER a sensible amendment ----
    [Fact]
    public void BridgeablePair_OverlapsAfterSensibleAmendment()
    {
        var m = Agent("M", "left",
            new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }) },
            new[] { ("scope", AnswerIntensity.High) });
        var p = Agent("P", "right",
            new[] { ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }) },
            new[] { ("gf", AnswerIntensity.High) });

        var vBase = V("vBase", ("scope", "large-only"), ("gf", "none"));
        AgentAcceptancePolicy.WouldSign(m, vBase).Accept.Should().BeTrue();
        AgentAcceptancePolicy.WouldSign(p, vBase).Accept.Should().BeFalse("P needs the grandfather carve-out");

        // Before any amendment, no overlap on the only toothful version.
        OverlapCalculator.Overlaps(new[] { m.ToPlayer(), p.ToPlayer() }, vBase).Should().BeFalse();

        // P proposes a sensible carve-out (gf -> exempt).
        var bridge = AgentActPolicy.ProposeBridge(p, vBase);
        bridge.Should().NotBeNull();
        bridge!.Positions["gf"].Should().Be("exempt");

        // Now BOTH would sign it -> their acceptance sets overlap.
        AgentAcceptancePolicy.WouldSign(m, bridge).Accept.Should().BeTrue();
        AgentAcceptancePolicy.WouldSign(p, bridge).Accept.Should().BeTrue();
        OverlapCalculator.Overlaps(new[] { m.ToPlayer(), p.ToPlayer() }, bridge).Should().BeTrue();
    }

    // ---- unbridgeable: no toothful version overlaps without a NonNegotiable decline ----
    [Fact]
    public void UnbridgeablePair_NeverOverlapsWithoutViolatingANonNegotiable()
    {
        var m = Agent("M", "left",
            new[] { ("scope", new[] { "large-only" }) },
            new[] { ("scope", AnswerIntensity.NonNegotiable) });
        var p = Agent("P", "right",
            new[] { ("scope", new[] { "all" }) },
            new[] { ("scope", AnswerIntensity.NonNegotiable) });

        var vLarge = V("vLarge", ("scope", "large-only"));
        var vAll = V("vAll", ("scope", "all"));

        // Enumerate every toothful candidate either side could put forward, including
        // each agent's self-serving bridge.
        var candidates = new List<VersionPoint> { vLarge, vAll };
        var mBridge = AgentActPolicy.ProposeBridge(m, vAll); if (mBridge is not null) candidates.Add(mBridge);
        var pBridge = AgentActPolicy.ProposeBridge(p, vLarge); if (pBridge is not null) candidates.Add(pBridge);

        foreach (var v in candidates)
        {
            var sm = AgentAcceptancePolicy.WouldSign(m, v);
            var sp = AgentAcceptancePolicy.WouldSign(p, v);
            var bothAccept = sm.Accept && sp.Accept;
            bothAccept.Should().BeFalse($"no toothful version should satisfy both ({v.Id})");

            // Whoever rejects does so on a NonNegotiable -> principled dissent, not a missed bridge.
            if (!sm.Accept) sm.IsPrincipledDissent.Should().BeTrue();
            if (!sp.Accept) sp.IsPrincipledDissent.Should().BeTrue();
        }

        // And there is genuinely no version (across candidates) both would co-sign.
        candidates.Any(v => AgentAcceptancePolicy.WouldSign(m, v).Accept
                         && AgentAcceptancePolicy.WouldSign(p, v).Accept).Should().BeFalse();
    }

    // ---- act policy basics ----
    [Fact]
    public void ActPolicy_PositionsThenAmendsThenAccepts()
    {
        var m = Agent("M", "left",
            new[] { ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }) });
        var p = Agent("P", "right",
            new[] { ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }) });
        var sm = new ProvisionStateMachine();
        var state = new ProvisionLoopState("prov", new[] { m.ToPlayer(), p.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

        // Unpositioned -> takes a position.
        AgentActPolicy.ChooseAct(p, state).Should().BeOfType<TakePositionAct>();
        sm.Apply(state, new TakePositionAct("M", "x", AnswerIntensity.Medium));
        sm.Apply(state, new TakePositionAct("P", "y", AnswerIntensity.Medium));

        // P rejects the base, so its next act is to propose a carve-out amendment.
        var pAct = AgentActPolicy.ChooseAct(p, state);
        pAct.Should().BeOfType<ProposeAmendmentAct>();
        ((ProposeAmendmentAct)pAct!).Version.Positions["gf"].Should().Be("exempt");

        // M already accepts the base, so its act is to co-sign an acceptable version.
        AgentActPolicy.ChooseAct(m, state).Should().BeOfType<CastAcceptanceAct>()
            .Which.Accept.Should().BeTrue();
    }
}
