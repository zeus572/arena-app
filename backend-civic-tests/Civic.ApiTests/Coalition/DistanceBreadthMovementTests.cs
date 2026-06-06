using Civic.API.Services.Coalition.Geometry;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 1.2 gate: distance shrinks when an amendment pulls an excluded corner in;
/// breadth ignores headcount; movement fires on a reject→accept sequence. Pure
/// unit tests — no DB, no LLM.
/// </summary>
public class DistanceBreadthMovementTests
{
    private static VersionPoint V(string id, params (string key, string label)[] pos) =>
        new(id, pos.ToDictionary(p => p.key, p => p.label));

    private static PlayerGeometry Player(string id, AcceptanceRegion region, string? bucket = null) =>
        new(id, region, bucket);

    // ---------------------------------------------------------------
    // (a) distance SHRINKS when an amendment pulls a previously-excluded corner in
    // ---------------------------------------------------------------
    [Fact]
    public void Distance_Shrinks_WhenAmendmentPullsCornerIn()
    {
        // Data-center fee: bridging dimension is the grandfather carve-out the
        // precaution corner (P) needs. M = market corner, X = middle.
        var m = Player("M", AcceptanceRegion.FromConstraints(
            ("scope", new[] { "large-only" }), ("grandfather", new[] { "exempt", "none" })));
        var x = Player("X", AcceptanceRegion.FromConstraints(
            ("scope", new[] { "large-only", "all" }), ("grandfather", new[] { "exempt", "none" })));
        var p = Player("P", AcceptanceRegion.FromConstraints(
            ("scope", new[] { "large-only", "all" }), ("grandfather", new[] { "exempt" })));
        var required = new[] { m, x, p };

        // Existing versions exclude P (both resolve grandfather=none, which P rejects).
        var vBase = V("vBase", ("scope", "large-only"), ("grandfather", "none")); // M,X accept; P rejects
        var vAlt = V("vAlt", ("scope", "all"), ("grandfather", "none"));          // X accepts; M,P reject

        var before = DistanceCalculator.DistanceToCoalition(new[] { vBase, vAlt }, required);
        before.Uncovered.Should().Be(1, "only P is uncovered by the best (vBase) version");
        before.BestVersion!.Id.Should().Be("vBase");
        before.MissingUserIds.Should().Equal("P");
        before.Normalized.Should().BeApproximately(1.0 / 3.0, 1e-9);

        // Amendment: a grandfather carve-out that pulls P in without losing M or X.
        var vBridge = V("vBridge", ("scope", "large-only"), ("grandfather", "exempt"));

        var after = DistanceCalculator.DistanceToCoalition(new[] { vBase, vAlt, vBridge }, required);
        after.Uncovered.Should().Be(0, "the bridge version sits in all three acceptance sets");
        after.BestVersion!.Id.Should().Be("vBridge");
        after.MissingUserIds.Should().BeEmpty();

        after.Uncovered.Should().BeLessThan(before.Uncovered, "distance must shrink when a corner is pulled in");
    }

    [Fact]
    public void Distance_Edges_NoVersionsAndEmptyRequired()
    {
        var p = Player("P", AcceptanceRegion.FromConstraints(("scope", new[] { "large-only" })));

        var noVersions = DistanceCalculator.DistanceToCoalition(Array.Empty<VersionPoint>(), new[] { p });
        noVersions.Uncovered.Should().Be(1);
        noVersions.Normalized.Should().Be(1.0);
        noVersions.BestVersion.Should().BeNull();

        var noRequired = DistanceCalculator.DistanceToCoalition(new[] { V("v", ("scope", "large-only")) },
            Array.Empty<PlayerGeometry>());
        noRequired.Uncovered.Should().Be(0);
        noRequired.Normalized.Should().Be(0.0);
    }

    // ---------------------------------------------------------------
    // (b) breadth IGNORES headcount
    // ---------------------------------------------------------------
    [Fact]
    public void Breadth_IgnoresHeadcount_CountsDistinctSpectrumCoverage()
    {
        var spectrum = new ComposedSpectrum(new[] { "left", "center", "right" });

        var twoBuckets = new[]
        {
            Player("a", AcceptanceRegion.Unconstrained(), "left"),
            Player("b", AcceptanceRegion.Unconstrained(), "center"),
        };
        var b0 = BreadthCalculator.Breadth(twoBuckets, spectrum);
        b0.CoveredBuckets.Should().Be(2);
        b0.Coverage.Should().BeApproximately(2.0 / 3.0, 1e-9);
        b0.Uncovered.Should().Equal("right");

        // Add another signer in an ALREADY-COVERED bucket -> breadth unchanged.
        var plusDuplicate = twoBuckets.Append(Player("c", AcceptanceRegion.Unconstrained(), "center")).ToArray();
        var b1 = BreadthCalculator.Breadth(plusDuplicate, spectrum);
        b1.CoveredBuckets.Should().Be(2, "adding a signer in a covered bucket must not move breadth");
        b1.Coverage.Should().BeApproximately(b0.Coverage, 1e-9);

        // Add a signer in the empty bucket -> breadth increases.
        var plusNewCorner = plusDuplicate.Append(Player("d", AcceptanceRegion.Unconstrained(), "right")).ToArray();
        var b2 = BreadthCalculator.Breadth(plusNewCorner, spectrum);
        b2.CoveredBuckets.Should().Be(3);
        b2.Coverage.Should().Be(1.0);
    }

    [Fact]
    public void Breadth_MeasuredAgainstComposedSpectrum_NotResponders()
    {
        // Denominator is the league's composed spectrum (4 buckets), even though
        // only responders in 2 of them showed up. A responder outside the
        // composed spectrum ("fringe") doesn't count toward coverage.
        var spectrum = new ComposedSpectrum(new[] { "far-left", "left", "right", "far-right" });
        var signers = new[]
        {
            Player("a", AcceptanceRegion.Unconstrained(), "left"),
            Player("b", AcceptanceRegion.Unconstrained(), "right"),
            Player("c", AcceptanceRegion.Unconstrained(), "fringe"), // outside the spectrum
        };
        var b = BreadthCalculator.Breadth(signers, spectrum);
        b.TotalBuckets.Should().Be(4);
        b.CoveredBuckets.Should().Be(2);
        b.Coverage.Should().BeApproximately(0.5, 1e-9);
    }

    // ---------------------------------------------------------------
    // (c) movement fires on reject→accept (and NOT otherwise)
    // ---------------------------------------------------------------
    [Fact]
    public void Movement_Fires_OnRejectThenAccept_SameConfiguration()
    {
        var vX = V("vX", ("scope", "large-only"), ("grandfather", "exempt"));
        var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddDays(1);

        var moved = MovementDetector.DetectFromSignals(new[]
        {
            new AcceptanceSignal(vX, Accept: false, At: t1),
            new AcceptanceSignal(vX, Accept: true, At: t2),
        });
        moved.Moved.Should().BeTrue();
        moved.Movements.Should().ContainSingle()
            .Which.AcceptedAt.Should().Be(t2);
    }

    [Fact]
    public void Movement_DoesNotFire_WithoutAPriorReject()
    {
        var vX = V("vX", ("scope", "large-only"));

        MovementDetector.DetectFromSignals(new[]
        {
            new AcceptanceSignal(vX, Accept: true),
        }).Moved.Should().BeFalse("accept-only is not movement");

        // Contraction (accept then later reject) is not movement.
        MovementDetector.DetectFromSignals(new[]
        {
            new AcceptanceSignal(vX, Accept: true, At: new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)),
            new AcceptanceSignal(vX, Accept: false, At: new DateTime(2026,1,2,0,0,0,DateTimeKind.Utc)),
        }).Moved.Should().BeFalse();

        // Rejecting A then accepting a DIFFERENT configuration B is not, by itself,
        // movement on a previously-rejected configuration.
        var vA = V("vA", ("scope", "all"));
        var vB = V("vB", ("scope", "large-only"));
        MovementDetector.DetectFromSignals(new[]
        {
            new AcceptanceSignal(vA, Accept: false, At: new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)),
            new AcceptanceSignal(vB, Accept: true, At: new DateTime(2026,1,2,0,0,0,DateTimeKind.Utc)),
        }).Moved.Should().BeFalse();
    }

    [Fact]
    public void Movement_RegionExpansionForm()
    {
        var vBridge = V("vBridge", ("scope", "large-only"), ("grandfather", "exempt"));
        var before = AcceptanceRegion.FromConstraints(("grandfather", new[] { "none" }));   // rejects exempt
        var after = AcceptanceRegion.FromConstraints(("grandfather", new[] { "none", "exempt" })); // now accepts

        before.Contains(vBridge).Should().BeFalse();
        MovementDetector.RegionExpandedToInclude(before, after, vBridge).Should().BeTrue();
        // No expansion if it was already acceptable.
        MovementDetector.RegionExpandedToInclude(after, after, vBridge).Should().BeFalse();
    }
}
