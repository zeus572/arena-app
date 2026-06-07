namespace Civic.API.Services.Coalition.Geometry;

/// <summary>
/// A multi-axis composed spectrum (doc 06 "still open"): a provision can load on 2-3
/// Values axes, each with its own buckets. Breadth is then measured PER AXIS.
/// </summary>
public sealed class MultiAxisSpectrum
{
    private readonly Dictionary<string, List<string>> _ordered;
    private readonly Dictionary<string, HashSet<string>> _set;

    public MultiAxisSpectrum(IReadOnlyDictionary<string, IEnumerable<string>> axisBuckets)
    {
        _ordered = new(StringComparer.OrdinalIgnoreCase);
        _set = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (axis, buckets) in axisBuckets)
        {
            var list = new List<string>();
            var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in buckets) if (hs.Add(b)) list.Add(b);
            _ordered[axis] = list;
            _set[axis] = hs;
        }
    }

    public IReadOnlyCollection<string> Axes => _ordered.Keys;
    public IReadOnlyList<string> Buckets(string axis) => _ordered.TryGetValue(axis, out var l) ? l : new();
    public bool Contains(string axis, string bucket) => _set.TryGetValue(axis, out var s) && s.Contains(bucket);
}

public sealed record AxisCoverage(string Axis, int Covered, int Total, double Coverage, IReadOnlyList<string> Uncovered);

public sealed record PerAxisBreadthResult(
    IReadOnlyList<AxisCoverage> PerAxis,
    double OverallCoverage,
    bool IncompleteCrossAxisCoverage)
{
    /// <summary>Doc 06: incomplete cross-axis coverage is a natural fork trigger.</summary>
    public bool ForkTrigger => IncompleteCrossAxisCoverage;
}

/// <summary>
/// Phase-1-style geometry, extended to multiple axes. A coalition must span EACH
/// relevant axis to be broad (overall coverage = the minimum per-axis coverage). When
/// some axes are well-covered and others are not, that asymmetry is flagged as a fork
/// trigger. Pure computation (no LLM).
/// </summary>
public static class PerAxisBreadthCalculator
{
    public static PerAxisBreadthResult Breadth(
        IEnumerable<IReadOnlyDictionary<string, string>> signerAxisBuckets,
        MultiAxisSpectrum spectrum,
        double broadThreshold = 0.5)
    {
        var signers = signerAxisBuckets.ToList();
        var perAxis = new List<AxisCoverage>();

        foreach (var axis in spectrum.Axes)
        {
            var total = spectrum.Buckets(axis).Count;
            var covered = signers
                .Select(s => s.TryGetValue(axis, out var b) ? b : null)
                .Where(b => b is not null && spectrum.Contains(axis, b))
                .Select(b => b!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var uncovered = spectrum.Buckets(axis).Where(b => !covered.Contains(b)).ToList();
            var coverage = total == 0 ? 0.0 : (double)covered.Count / total;
            perAxis.Add(new AxisCoverage(axis, covered.Count, total, coverage, uncovered));
        }

        var overall = perAxis.Count == 0 ? 0.0 : perAxis.Min(a => a.Coverage);
        // Incomplete cross-axis: at least one axis is broad while another lags below threshold.
        var anyBroad = perAxis.Any(a => a.Coverage >= broadThreshold);
        var anyLagging = perAxis.Any(a => a.Coverage < broadThreshold);
        var incomplete = anyBroad && anyLagging;

        return new PerAxisBreadthResult(perAxis, overall, incomplete);
    }
}
