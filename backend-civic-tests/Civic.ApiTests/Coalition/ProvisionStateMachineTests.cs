using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2.1 gate: scripted acts drive the coalition state machine through every
/// transition and all three resolutions (PASSED / FORKED / DIED). Pure unit tests
/// — no DB, no LLM.
/// </summary>
public class ProvisionStateMachineTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static PlayerGeometry Agent(string id, string bucket, params (string key, string[] labels)[] region) =>
        new(id, AcceptanceRegion.FromConstraints(region), bucket);

    private readonly ProvisionStateMachine _sm = new();

    // OPEN -> CONTESTED -> NEAR-COALITION -> PASSED
    [Fact]
    public void Passed_Path_TraversesOpenContestedNearPassed()
    {
        var m = Agent("M", "left", ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }));
        var p = Agent("P", "right", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }));
        var vBase = V("vBase", ("scope", "large-only"), ("gf", "none"));     // M accepts, P rejects
        var vBridge = V("vBridge", ("scope", "large-only"), ("gf", "exempt")); // both accept

        var s = new ProvisionLoopState("prov-pass", new[] { m, p },
            new ComposedSpectrum(new[] { "left", "right" }),
            lifetime: TimeSpan.FromDays(7), initialVersions: new[] { vBase });

        s.State.Should().Be(ProvisionState.Open);
        _sm.Apply(s, new TakePositionAct("M", "for, narrow", AnswerIntensity.Medium)).Should().Be(ProvisionState.Open);
        _sm.Apply(s, new TakePositionAct("P", "against blanket", AnswerIntensity.High)).Should().Be(ProvisionState.Contested);

        // P declines the base (the move it will later bargain away from).
        _sm.Apply(s, new CastAcceptanceAct("P", vBase, Accept: false, AnswerIntensity.High)).Should().Be(ProvisionState.Contested);

        // P proposes the grandfather carve-out -> a spanning, broad version -> NEAR.
        _sm.Apply(s, new ProposeAmendmentAct("P", vBridge)).Should().Be(ProvisionState.NearCoalition);

        // M signs the bridge; not all required have signed yet.
        _sm.Apply(s, new CastAcceptanceAct("M", vBridge, Accept: true, AnswerIntensity.Medium)).Should().Be(ProvisionState.NearCoalition);

        // P signs the bridge (having declined the base earlier => P moved) -> PASSED.
        _sm.Apply(s, new CastAcceptanceAct("P", vBridge, Accept: true, AnswerIntensity.High)).Should().Be(ProvisionState.Passed);

        var o = s.Outcome!;
        o.FinalState.Should().Be(ProvisionState.Passed);
        o.Plank!.Id.Should().Be("vBridge");
        o.Signers.Should().BeEquivalentTo(new[] { "M", "P" });
        o.Specificity.Should().Be(2);
        o.MovedSigners.Should().BeGreaterThanOrEqualTo(1);
        o.Breadth!.CoveredBuckets.Should().Be(2, "signers span both spectrum buckets");
    }

    // OPEN -> CONTESTED -> FORKED
    [Fact]
    public void Forked_Path_TwoBroadDisjointCamps()
    {
        var spectrum = new ComposedSpectrum(new[] { "left", "center", "right" });
        var campA = new[]
        {
            Agent("aL", "left", ("scope", new[] { "all" })),
            Agent("aC", "center", ("scope", new[] { "all" })),
            Agent("aR", "right", ("scope", new[] { "all" })),
        };
        var campB = new[]
        {
            Agent("bL", "left", ("scope", new[] { "large-only" })),
            Agent("bC", "center", ("scope", new[] { "large-only" })),
            Agent("bR", "right", ("scope", new[] { "large-only" })),
        };
        var s = new ProvisionLoopState("prov-fork", campA.Concat(campB),
            spectrum, lifetime: TimeSpan.FromDays(7));

        foreach (var a in campA.Concat(campB))
            _sm.Apply(s, new TakePositionAct(a.UserId, "stance", AnswerIntensity.High));
        s.State.Should().Be(ProvisionState.Open, "no version on the table yet");

        _sm.Apply(s, new ProposeAmendmentAct("aL", V("v-all", ("scope", "all")))).Should().Be(ProvisionState.Contested);
        _sm.Apply(s, new ProposeAmendmentAct("bL", V("v-large", ("scope", "large-only")))).Should().Be(ProvisionState.Forked);

        s.Outcome!.ForkChildren.Should().HaveCount(2);
        s.Outcome.ForkChildren!.Select(c => c.BasinVersion.Id).Should().BeEquivalentTo(new[] { "v-all", "v-large" });
    }

    // OPEN -> CONTESTED -> DIED (deadline, unbridgeable)
    [Fact]
    public void Died_Path_DeadlineInContested()
    {
        var m = Agent("M", "left", ("scope", new[] { "large-only" }));
        var p = Agent("P", "right", ("scope", new[] { "all" })); // irreconcilable on scope
        var s = new ProvisionLoopState("prov-die", new[] { m, p },
            new ComposedSpectrum(new[] { "left", "right" }),
            lifetime: TimeSpan.FromDays(7), initialVersions: new[] { V("vBase", ("scope", "large-only")) });

        _sm.Apply(s, new TakePositionAct("M", "x", AnswerIntensity.High));
        _sm.Apply(s, new TakePositionAct("P", "y", AnswerIntensity.High)).Should().Be(ProvisionState.Contested);
        _sm.Apply(s, new ProposeAmendmentAct("P", V("vAll", ("scope", "all")))).Should().Be(ProvisionState.Contested);

        _sm.Apply(s, new AdvanceToDeadlineAct()).Should().Be(ProvisionState.Died);
        s.Outcome!.DiedReason.Should().Contain("deadline");
    }

    // Deadline can send any active state to DIED (here straight from OPEN).
    [Fact]
    public void Died_FromOpen_OnDeadline()
    {
        var m = Agent("M", "left", ("scope", new[] { "large-only" }));
        var p = Agent("P", "right", ("scope", new[] { "all" }));
        var s = new ProvisionLoopState("prov-die-open", new[] { m, p },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7));

        _sm.Apply(s, new AdvanceToDeadlineAct()).Should().Be(ProvisionState.Died);
    }

    // NEAR -> DIED (a spanning version formed but was never approved before the deadline).
    [Fact]
    public void Died_FromNear_WhenPlankNotApproved()
    {
        var m = Agent("M", "left", ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }));
        var p = Agent("P", "right", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }));
        var s = new ProvisionLoopState("prov-near-die", new[] { m, p },
            new ComposedSpectrum(new[] { "left", "right" }),
            lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vBase", ("scope", "large-only"), ("gf", "none")) });

        _sm.Apply(s, new TakePositionAct("M", "x", AnswerIntensity.Medium));
        _sm.Apply(s, new TakePositionAct("P", "y", AnswerIntensity.High));
        _sm.Apply(s, new ProposeAmendmentAct("P", V("vBridge", ("scope", "large-only"), ("gf", "exempt"))))
            .Should().Be(ProvisionState.NearCoalition);

        // Nobody co-signs the plank before the clock runs out.
        _sm.Apply(s, new AdvanceToDeadlineAct()).Should().Be(ProvisionState.Died);
    }

    [Fact]
    public void TerminalState_IgnoresFurtherActs()
    {
        var m = Agent("M", "left", ("scope", new[] { "large-only" }));
        var p = Agent("P", "right", ("scope", new[] { "all" }));
        var s = new ProvisionLoopState("prov-terminal", new[] { m, p },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7));
        _sm.Apply(s, new AdvanceToDeadlineAct()).Should().Be(ProvisionState.Died);

        // Further acts are no-ops once terminal.
        _sm.Apply(s, new TakePositionAct("M", "x", AnswerIntensity.Low)).Should().Be(ProvisionState.Died);
    }
}
