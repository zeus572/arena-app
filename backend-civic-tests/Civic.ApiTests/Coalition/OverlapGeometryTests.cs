using Civic.API.Services.Coalition.Geometry;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 1.1 gate: acceptance-set membership and overlap on hand-constructed
/// regions match hand-computed truth, including the edge cases the plan names
/// (empty overlap, full overlap, single-dimension disagreement). Pure unit tests
/// — no DB, no LLM.
/// </summary>
public class OverlapGeometryTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    // ---- Region membership (Contains) ----

    [Fact]
    public void Contains_RejectsConflictingLabel_AllowsSilenceAndMatch()
    {
        var region = AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" }));

        region.Contains(V("v1", ("scope", "large-only"))).Should().BeTrue("matches the acceptable label");
        region.Contains(V("v2", ("scope", "all"))).Should().BeFalse("takes a position the player won't accept");
        region.Contains(V("v3", ("cost", "marginal"))).Should().BeTrue("silent on the constrained key");
        region.Contains(V("v4")).Should().BeTrue("empty version violates nothing");
    }

    [Fact]
    public void Contains_MultiLabelAcceptableSet()
    {
        var region = AcceptanceRegion.FromConstraints(("cost", new[] { "marginal", "average" }));
        region.Contains(V("v", ("cost", "marginal"))).Should().BeTrue();
        region.Contains(V("v", ("cost", "average"))).Should().BeTrue();
        region.Contains(V("v", ("cost", "vendor-discretion"))).Should().BeFalse();
    }

    [Fact]
    public void UnconstrainedRegion_AcceptsEverything()
    {
        var region = AcceptanceRegion.Unconstrained();
        region.IsUnconstrained.Should().BeTrue();
        region.Contains(V("v", ("scope", "all"), ("cost", "vendor-discretion"))).Should().BeTrue();
    }

    // ---- overlap(players, version) ----

    [Fact]
    public void Overlap_FullOverlap_AllAccept()
    {
        var players = new[]
        {
            new PlayerGeometry("a", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only", "all" }))),
            new PlayerGeometry("b", AcceptanceRegion.FromConstraints(("cost", new[] { "marginal" }))),
        };
        var version = V("v", ("scope", "large-only"), ("cost", "marginal"));

        OverlapCalculator.Overlaps(players, version).Should().BeTrue();
        OverlapCalculator.SupportCount(players, version).Should().Be(2);
    }

    [Fact]
    public void Overlap_EmptyOverlap_OneRejects()
    {
        var players = new[]
        {
            new PlayerGeometry("a", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" }))),
            new PlayerGeometry("b", AcceptanceRegion.FromConstraints(("scope", new[] { "all" }))),
        };
        // Any version resolving scope satisfies at most one of them.
        var versionLarge = V("v", ("scope", "large-only"));
        OverlapCalculator.Overlaps(players, versionLarge).Should().BeFalse();
        OverlapCalculator.Supporters(players, versionLarge).Select(p => p.UserId).Should().Equal("a");

        var versionAll = V("v", ("scope", "all"));
        OverlapCalculator.Overlaps(players, versionAll).Should().BeFalse();
        OverlapCalculator.Supporters(players, versionAll).Select(p => p.UserId).Should().Equal("b");

        // These two acceptance sets are irreconcilable on 'scope': no version
        // resolving it can satisfy both.
        OverlapCalculator.IrreconcilableKeys(players.Select(p => p.Region))
            .Should().Contain("scope");
    }

    [Fact]
    public void Overlap_SingleDimensionDisagreement()
    {
        // Both agree cost=marginal; they disagree only on scope.
        var a = new PlayerGeometry("a", AcceptanceRegion.FromConstraints(
            ("cost", new[] { "marginal" }), ("scope", new[] { "large-only" })));
        var b = new PlayerGeometry("b", AcceptanceRegion.FromConstraints(
            ("cost", new[] { "marginal" }), ("scope", new[] { "all" })));
        var players = new[] { a, b };

        // A version that resolves the contested dimension fails for one of them.
        OverlapCalculator.Overlaps(players, V("v", ("cost", "marginal"), ("scope", "large-only")))
            .Should().BeFalse();

        // A version silent on the contested dimension is accepted by both
        // (silence is acceptable) — but it is toothless on 'scope'. Teeth is a
        // later-layer pass criterion, not a membership question.
        var silentOnScope = V("v", ("cost", "marginal"));
        OverlapCalculator.Overlaps(players, silentOnScope).Should().BeTrue();
        silentOnScope.Specificity.Should().Be(1);

        // The shared cost agreement is reconcilable; only scope is irreconcilable.
        var irreconcilable = OverlapCalculator.IrreconcilableKeys(players.Select(p => p.Region));
        irreconcilable.Should().Contain("scope");
        irreconcilable.Should().NotContain("cost");
    }

    [Fact]
    public void Intersect_CombinesConstraints()
    {
        var a = AcceptanceRegion.FromConstraints(("cost", new[] { "marginal", "average" }), ("scope", new[] { "large-only" }));
        var b = AcceptanceRegion.FromConstraints(("cost", new[] { "marginal" })); // silent on scope
        var combined = OverlapCalculator.Intersect(new[] { a, b });

        // cost intersects to {marginal}; scope constrained only by 'a' stays {large-only}.
        combined.AcceptableLabels("cost").Should().BeEquivalentTo(new[] { "marginal" });
        combined.AcceptableLabels("scope").Should().BeEquivalentTo(new[] { "large-only" });

        // The combined region accepts a version honoring both, rejects one that doesn't.
        combined.Contains(V("v", ("cost", "marginal"), ("scope", "large-only"))).Should().BeTrue();
        combined.Contains(V("v", ("cost", "average"))).Should().BeFalse();
    }

    // ---- deriving regions from accept/decline signals (Layer 0 data shape) ----

    [Fact]
    public void Derive_BuildsAcceptableUnionFromAcceptedVersions()
    {
        var signals = new[]
        {
            new AcceptanceSignal(V("v1", ("scope", "large-only"), ("cost", "marginal")), Accept: true),
            new AcceptanceSignal(V("v2", ("scope", "large-only"), ("cost", "average")), Accept: true),
            new AcceptanceSignal(V("v3", ("scope", "all")), Accept: false), // decline: not used to subtract
        };
        var region = AcceptanceSetDeriver.Derive(signals);

        region.AcceptableLabels("scope").Should().BeEquivalentTo(new[] { "large-only" });
        region.AcceptableLabels("cost").Should().BeEquivalentTo(new[] { "marginal", "average" });

        region.Contains(V("x", ("scope", "large-only"), ("cost", "average"))).Should().BeTrue();
        region.Contains(V("x", ("scope", "all"))).Should().BeFalse("'all' was never co-signed for scope");
    }
}
