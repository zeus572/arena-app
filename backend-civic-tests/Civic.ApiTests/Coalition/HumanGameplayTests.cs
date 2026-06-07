using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Human;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2H.1 gate: human input produces identical machine behavior to
/// scripted/agent input (A6 — one machine, two kinds of player), and the spectrum
/// bar reflects the geometry. Pure unit tests — no DB, no LLM.
/// </summary>
public class HumanGameplayTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static PlayerGeometry P(string id, string bucket, params (string key, string[] labels)[] region) =>
        new(id, AcceptanceRegion.FromConstraints(region), bucket);

    private static ProvisionLoopState FreshBridgeState() =>
        new("prov", new[]
            {
                P("M", "left", ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" })),
                P("P", "right", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" })),
            },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

    // The same canonical bridge script, expressed once.
    private static readonly VersionPoint VBridge = V("vBridge", ("scope", "large-only"), ("gf", "exempt"));

    [Fact]
    public void HumanInput_ProducesIdenticalMachineBehavior_ToScriptedInput()
    {
        var sm = new ProvisionStateMachine();

        // (1) scripted/agent path: raw LoopActs.
        var a = FreshBridgeState();
        sm.Apply(a, new TakePositionAct("M", "x", AnswerIntensity.Medium));
        sm.Apply(a, new TakePositionAct("P", "y", AnswerIntensity.High));
        sm.Apply(a, new CastAcceptanceAct("P", a.Versions[0], false, AnswerIntensity.High));
        sm.Apply(a, new ProposeAmendmentAct("P", VBridge));
        sm.Apply(a, new CastAcceptanceAct("M", VBridge, true, AnswerIntensity.Medium));
        sm.Apply(a, new CastAcceptanceAct("P", VBridge, true, AnswerIntensity.High));

        // (2) human path: the SAME content, expressed as HumanActs through the translator.
        var h = FreshBridgeState();
        var humanScript = new HumanAct[]
        {
            new HumanPosition("M", "x", AnswerIntensity.Medium),
            new HumanPosition("P", "y", AnswerIntensity.High),
            new HumanReactionWithReason("P", "I can't accept a blanket fee"), // engagement-only, must be a no-op
            new HumanDecline("P", h.Versions[0], AnswerIntensity.High),
            new HumanSteelman("M", "The market-side concern is real"),        // engagement-only no-op
            new HumanAmendment("P", VBridge),
            new HumanCoSign("M", VBridge, AnswerIntensity.Medium),
            new HumanCoSign("P", VBridge, AnswerIntensity.High),
        };
        foreach (var act in humanScript)
        {
            var loopAct = HumanActTranslator.ToLoopAct(act);
            if (loopAct is not null) sm.Apply(h, loopAct);
        }

        // Identical machine behavior.
        a.State.Should().Be(ProvisionState.Passed);
        h.State.Should().Be(a.State);
        h.Outcome!.Plank!.Canonical().Should().Be(a.Outcome!.Plank!.Canonical());
        h.Outcome.Signers.Should().BeEquivalentTo(a.Outcome.Signers);
        h.Outcome.Breadth!.CoveredBuckets.Should().Be(a.Outcome.Breadth!.CoveredBuckets);
    }

    [Fact]
    public void HumanDriven_And_AgentDriven_ReachTheSameOutcome()
    {
        // Agent-driven via self-play.
        var agentState = FreshBridgeState();
        var m = new CoalitionAgent("M", "left", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" })));
        var p = new CoalitionAgent("P", "right", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" })));
        var agentResult = new SelfPlayRunner().Run(agentState, new[] { m, p });

        // Human-driven via the translator (canonical script).
        var sm = new ProvisionStateMachine();
        var humanState = FreshBridgeState();
        foreach (var act in new HumanAct[]
                 {
                     new HumanPosition("M", "x", AnswerIntensity.Medium),
                     new HumanPosition("P", "y", AnswerIntensity.High),
                     new HumanDecline("P", humanState.Versions[0], AnswerIntensity.High),
                     new HumanAmendment("P", VBridge),
                     new HumanCoSign("M", VBridge, AnswerIntensity.Medium),
                     new HumanCoSign("P", VBridge, AnswerIntensity.High),
                 })
        {
            var la = HumanActTranslator.ToLoopAct(act);
            if (la is not null) sm.Apply(humanState, la);
        }

        agentResult.Outcome!.FinalState.Should().Be(ProvisionState.Passed);
        humanState.State.Should().Be(ProvisionState.Passed);
        humanState.Outcome!.Plank!.Canonical().Should().Be(agentResult.Outcome.Plank!.Canonical());
    }

    [Fact]
    public void SpectrumBar_ReflectsGeometry()
    {
        var sm = new ProvisionStateMachine();
        var s = FreshBridgeState();
        sm.Apply(s, new TakePositionAct("M", "x", AnswerIntensity.Medium));
        sm.Apply(s, new TakePositionAct("P", "y", AnswerIntensity.High));

        // Only the base exists: M (left) reaches it, P (right) does not -> right is a dark corner.
        var bar0 = SpectrumBarBuilder.Build(s);
        bar0.TotalBuckets.Should().Be(2);
        bar0.CoveredBuckets.Should().Be(1);
        bar0.Cells.Single(c => c.Bucket == "left").Covered.Should().BeTrue();
        bar0.Cells.Single(c => c.Bucket == "right").Covered.Should().BeFalse();
        bar0.Distance.Should().BeApproximately(0.5, 1e-9);

        // Add the bridge both accept -> the whole spectrum lights up, distance -> 0.
        sm.Apply(s, new ProposeAmendmentAct("P", VBridge));
        var bar1 = SpectrumBarBuilder.Build(s);
        bar1.CoveredBuckets.Should().Be(2);
        bar1.Cells.Should().OnlyContain(c => c.Covered);
        bar1.Distance.Should().Be(0.0);
        bar1.LeadingVersionId.Should().Be("vBridge");
    }
}
