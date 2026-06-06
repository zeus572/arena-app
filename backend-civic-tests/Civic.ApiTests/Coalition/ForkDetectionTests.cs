using Civic.API.Services.Coalition.Geometry;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 1.3 gate: a constructed scenario that should fork (two non-overlapping
/// values-broad camps) is flagged as a fork; a convergent scenario (one broad
/// camp) is not. Pure unit tests — no DB, no LLM.
/// </summary>
public class ForkDetectionTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static readonly ComposedSpectrum Spectrum = new(new[] { "left", "center", "right" });

    // ---------------------------------------------------------------
    // CONVERGENT: one version draws a spectrum-spanning camp; the only alternative
    // is narrow. Not a fork.
    // ---------------------------------------------------------------
    [Fact]
    public void Convergent_SingleBroadBasin_IsNotAFork()
    {
        // A uniting version everyone across the spectrum accepts.
        var players = new[]
        {
            new PlayerGeometry("L", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" }), ("gf", new[] { "exempt", "none" })), "left"),
            new PlayerGeometry("C", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt", "none" })), "center"),
            new PlayerGeometry("R", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only", "all" }), ("gf", new[] { "exempt" })), "right"),
        };

        var uniting = V("uniting", ("scope", "large-only"), ("gf", "exempt")); // all three accept
        var narrow = V("narrow", ("scope", "all"), ("gf", "none"));            // only C accepts

        var result = ForkDetector.Detect(new[] { uniting, narrow }, players, Spectrum);

        result.IsFork.Should().BeFalse();
        result.Classification.Should().Be(ForkClassification.Convergent);
        result.Basins.Should().ContainSingle()
            .Which.Representative.Id.Should().Be("uniting");
    }

    // ---------------------------------------------------------------
    // FORK: two incompatible versions each assemble a cross-spectrum camp; the
    // camps are disjoint and no single version unites them. The classic
    // data-center fork ("all facilities" vs "large facilities only").
    // ---------------------------------------------------------------
    [Fact]
    public void Fork_TwoNonOverlappingBroadBasins_IsFlagged()
    {
        // Camp A accepts scope=all (and rejects large-only); Camp B the reverse.
        var campA = new[]
        {
            new PlayerGeometry("aL", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "left"),
            new PlayerGeometry("aC", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "center"),
            new PlayerGeometry("aR", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "right"),
        };
        var campB = new[]
        {
            new PlayerGeometry("bL", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "left"),
            new PlayerGeometry("bC", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "center"),
            new PlayerGeometry("bR", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "right"),
        };
        var players = campA.Concat(campB).ToList();

        var vAll = V("v-all", ("scope", "all"));
        var vLarge = V("v-large", ("scope", "large-only"));

        var result = ForkDetector.Detect(new[] { vAll, vLarge }, players, Spectrum);

        result.IsFork.Should().BeTrue();
        result.Classification.Should().Be(ForkClassification.Fork);
        result.Basins.Should().HaveCount(2);
        // Each basin is values-broad (spans all three spectrum buckets).
        result.Basins.Should().OnlyContain(b => b.Breadth.CoveredBuckets == 3);
        result.Basins.Select(b => b.Representative.Id).Should().BeEquivalentTo(new[] { "v-all", "v-large" });
    }

    [Fact]
    public void Fork_CollapsesToConvergent_WhenABridgeVersionUnitesBothCamps()
    {
        // Same two camps, but now a bridge version (silent on the contested
        // 'scope', resolving an uncontested 'transparency') that BOTH camps accept.
        var campA = new[]
        {
            new PlayerGeometry("aL", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "left"),
            new PlayerGeometry("aC", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "center"),
            new PlayerGeometry("aR", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "right"),
        };
        var campB = new[]
        {
            new PlayerGeometry("bL", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "left"),
            new PlayerGeometry("bC", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "center"),
            new PlayerGeometry("bR", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "right"),
        };
        var players = campA.Concat(campB).ToList();

        var vAll = V("v-all", ("scope", "all"));
        var vLarge = V("v-large", ("scope", "large-only"));
        var bridge = V("bridge", ("transparency", "required")); // both camps silent-accept it

        var result = ForkDetector.Detect(new[] { vAll, vLarge, bridge }, players, Spectrum);

        result.IsFork.Should().BeFalse("a uniting broad version now exists");
        result.Classification.Should().Be(ForkClassification.Convergent);
        result.Basins.Should().ContainSingle().Which.Representative.Id.Should().Be("bridge");
    }

    [Fact]
    public void None_WhenNoBroadBasinExists()
    {
        // Two camps, but each only occupies a single spectrum bucket -> neither
        // is values-broad -> not a coalition, not a fork.
        var players = new[]
        {
            new PlayerGeometry("aL", AcceptanceRegion.FromConstraints(("scope", new[] { "all" })), "left"),
            new PlayerGeometry("bL", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })), "left"),
        };
        var result = ForkDetector.Detect(
            new[] { V("v-all", ("scope", "all")), V("v-large", ("scope", "large-only")) },
            players, Spectrum);

        result.Classification.Should().Be(ForkClassification.None);
        result.IsFork.Should().BeFalse();
    }
}
