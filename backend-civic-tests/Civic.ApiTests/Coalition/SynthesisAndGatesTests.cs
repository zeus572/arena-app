using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 2.3 gate: synthesis produces a plank inside the spanning intersection and
/// that plank is accepted by the would-be signers' wouldSign(); the integrity gates
/// reject a cosmetic amendment and a toothless plank. Pure unit tests — no DB, no LLM
/// (structural gates; semantic refinement is a deferred seam).
/// </summary>
public class SynthesisAndGatesTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static CoalitionAgent Agent(string id, string bucket, params (string key, string[] labels)[] region) =>
        new(id, bucket, AcceptanceRegion.FromConstraints(region));

    private static ProvisionLoopState BridgeState(out CoalitionAgent m, out CoalitionAgent p)
    {
        m = Agent("M", "left", ("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" }));
        p = Agent("P", "right", ("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" }));
        return new ProvisionLoopState("prov", new[] { m.ToPlayer(), p.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[]
            {
                V("vBase", ("scope", "large-only"), ("gf", "none")),
                V("vBridge", ("scope", "large-only"), ("gf", "exempt")),
            });
    }

    [Fact]
    public void Synthesis_ProducesPlankInsideSpanningIntersection_AcceptedBySigners()
    {
        var s = BridgeState(out var m, out var p);

        var result = SynthesisService.Synthesize(s);

        result.Should().NotBeNull();
        result!.Plank.Id.Should().Be("vBridge");
        result.WouldBeSigners.Should().BeEquivalentTo(new[] { "M", "P" });
        result.Text.Should().Contain("gf = exempt");

        // The plank sits inside the intersection of the signers' acceptance regions...
        OverlapCalculator.Overlaps(new[] { m.ToPlayer(), p.ToPlayer() }, result.Plank).Should().BeTrue();
        // ...and each would-be signer's wouldSign() accepts it (synthesis is self-consistent).
        AgentAcceptancePolicy.WouldSign(m, result.Plank).Accept.Should().BeTrue();
        AgentAcceptancePolicy.WouldSign(p, result.Plank).Accept.Should().BeTrue();
    }

    [Fact]
    public void Synthesis_ReturnsNull_WhenNoSpanningVersionExists()
    {
        // Unbridgeable on scope: no toothful version sits in both regions.
        var m = Agent("M", "left", ("scope", new[] { "large-only" }));
        var p = Agent("P", "right", ("scope", new[] { "all" }));
        var s = new ProvisionLoopState("prov", new[] { m.ToPlayer(), p.ToPlayer() },
            new ComposedSpectrum(new[] { "left", "right" }), lifetime: TimeSpan.FromDays(7),
            initialVersions: new[] { V("vLarge", ("scope", "large-only")), V("vAll", ("scope", "all")) });

        SynthesisService.Synthesize(s).Should().BeNull();
    }

    [Fact]
    public void Gate_Substantive_RejectsCosmeticAmendment()
    {
        var prior = V("v", ("scope", "large-only"), ("gf", "none"));
        var cosmetic = V("v-reworded", ("scope", "large-only"), ("gf", "none")); // same vector, different id/wording
        var real = V("v2", ("scope", "large-only"), ("gf", "exempt"));

        IntegrityGates.IsSubstantive(prior, cosmetic).Should().BeFalse("same extracted vector = cosmetic restatement");
        IntegrityGates.IsSubstantive(prior, real).Should().BeTrue("the carve-out changed a sub-question position");
    }

    [Fact]
    public void Gate_Teeth_RejectsToothlessPlank()
    {
        IntegrityGates.HasTeeth(V("empty")).Should().BeFalse("a plank resolving no sub-questions is toothless");
        IntegrityGates.HasTeeth(V("real", ("scope", "large-only"))).Should().BeTrue();
        IntegrityGates.HasTeeth(V("one", ("scope", "large-only")), minSpecificity: 2).Should().BeFalse();
    }

    [Fact]
    public void Gate_Movement_CountsSignersWhoBargainedIn()
    {
        var s = BridgeState(out _, out _);
        var sm = new ProvisionStateMachine();
        var vBridge = s.Versions.Single(v => v.Id == "vBridge");
        var vBase = s.Versions.Single(v => v.Id == "vBase");

        sm.Apply(s, new TakePositionAct("M", "x", AnswerIntensity.Medium));
        sm.Apply(s, new TakePositionAct("P", "y", AnswerIntensity.High));
        sm.Apply(s, new CastAcceptanceAct("P", vBase, Accept: false, AnswerIntensity.High)); // P rejects base first
        sm.Apply(s, new CastAcceptanceAct("M", vBridge, Accept: true, AnswerIntensity.Medium)); // M signs straight away
        sm.Apply(s, new CastAcceptanceAct("P", vBridge, Accept: true, AnswerIntensity.High));   // P bargained in

        var report = IntegrityGates.Evaluate(s, vBridge, new[] { "M", "P" });
        report.Teeth.Should().BeTrue();
        report.MovedSigners.Should().Be(1, "only P declined an earlier version before signing");
        report.Moved.Should().BeTrue();
        report.Passed.Should().BeTrue();
    }
}
