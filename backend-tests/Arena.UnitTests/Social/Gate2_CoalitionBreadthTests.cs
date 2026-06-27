using Arena.API.Social.Selection;
using FluentAssertions;
using Xunit;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 2 (part A): the §2.1 deterministic fallback geometry, tested in isolation with
/// hand-computed breadth/bipartisan values. Pure functions — no DB, no LLM.
/// </summary>
public class Gate2_CoalitionBreadthTests
{
    [Fact]
    public void ComputeBreadth_two_members_each_axis_delta_0_6_normalizes_to_0_6()
    {
        // delta on every one of 5 axes is 0.6 → distance = 0.6*sqrt(5); /sqrt(5) diameter = 0.6.
        var a = new[] { 0.2, 0.2, 0.2, 0.2, 0.2 };
        var b = new[] { 0.8, 0.8, 0.8, 0.8, 0.8 };
        CoalitionSignalProvider.ComputeBreadth(new[] { a, b }).Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void ComputeBreadth_identical_positions_is_zero()
    {
        var a = new[] { 0.5, 0.5, 0.5, 0.5, 0.5 };
        CoalitionSignalProvider.ComputeBreadth(new[] { a, (double[])a.Clone() }).Should().Be(0);
    }

    [Fact]
    public void ComputeBreadth_opposite_corners_is_one()
    {
        var lo = new[] { 0.0, 0.0, 0.0 };
        var hi = new[] { 1.0, 1.0, 1.0 };
        CoalitionSignalProvider.ComputeBreadth(new[] { lo, hi }).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void IsBipartisan_true_when_members_straddle_a_midpoint()
    {
        var a = new[] { 0.2, 0.2, 0.2, 0.2, 0.2 }; // all below 0.5
        var b = new[] { 0.8, 0.8, 0.8, 0.8, 0.8 }; // all above 0.5
        CoalitionSignalProvider.IsBipartisanInternal(new[] { a, b }).Should().BeTrue();
    }

    [Fact]
    public void IsBipartisan_false_when_all_members_same_side()
    {
        var a = new[] { 0.1, 0.1, 0.1, 0.1, 0.1 };
        var b = new[] { 0.3, 0.3, 0.3, 0.3, 0.3 }; // both below 0.5 on every axis
        CoalitionSignalProvider.IsBipartisanInternal(new[] { a, b }).Should().BeFalse();
    }
}
