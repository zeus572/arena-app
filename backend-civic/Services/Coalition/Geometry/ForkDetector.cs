namespace Civic.API.Services.Coalition.Geometry;

/// <summary>A candidate coalition basin: a version and the values-breadth of its supporters.</summary>
public sealed record Basin(
    VersionPoint Representative,
    IReadOnlyList<string> SupporterIds,
    BreadthResult Breadth);

public enum ForkClassification { None, Convergent, Fork }

public sealed record ForkResult(
    ForkClassification Classification,
    IReadOnlyList<Basin> Basins)
{
    public bool IsFork => Classification == ForkClassification.Fork;
}

/// <summary>Tunable thresholds for fork detection (recorded assumptions; defaults are sane starts).</summary>
public sealed record ForkOptions(
    double BroadCoverage = 0.6,   // fraction of the composed spectrum a basin must cover to be "values-broad"
    double OverlapTolerance = 0.34, // two broad basins are "non-overlapping" if they share <= this fraction of the smaller camp
    int MinSpecificity = 1);      // ignore fully-empty (toothless catch-all) versions

/// <summary>
/// Phase 1.3 — fork detection. Pure computation (no LLM).
///
/// Forking falls out of the math (doc 06): a fork is simply "no single
/// configuration in a spanning intersection, but two." We compute, for each
/// candidate version, its supporter set and the values-breadth of those
/// supporters. Then:
///   - if there exist two values-broad basins whose supporter sets are
///     NON-OVERLAPPING (two distinct cross-spectrum camps) -> FORK;
///   - else if there is a single values-broad basin -> CONVERGENT;
///   - else -> NONE (no broad coalition yet).
///
/// Convergent vs. fork therefore turns on whether a uniting broad version exists
/// among the candidates: if one version draws a spectrum-spanning camp and no
/// second disjoint broad camp exists, that's convergence; if instead two disjoint
/// broad camps form around incompatible versions, that's a fork.
/// </summary>
public static class ForkDetector
{
    public static ForkResult Detect(
        IEnumerable<VersionPoint> versions,
        IReadOnlyList<PlayerGeometry> players,
        ComposedSpectrum spectrum,
        ForkOptions? options = null)
    {
        var opt = options ?? new ForkOptions();
        var broadThreshold = Math.Max(2, (int)Math.Ceiling(opt.BroadCoverage * spectrum.TotalBuckets));

        // Build a basin per candidate version (ignoring degenerate toothless ones).
        var basins = new List<Basin>();
        foreach (var v in versions)
        {
            if (v.Specificity < opt.MinSpecificity) continue;
            var supporters = OverlapCalculator.Supporters(players, v);
            var breadth = BreadthCalculator.Breadth(supporters, spectrum);
            basins.Add(new Basin(v, supporters.Select(s => s.UserId).ToList(), breadth));
        }

        // Values-broad basins, widest first.
        var broad = basins
            .Where(b => b.Breadth.CoveredBuckets >= broadThreshold)
            .OrderByDescending(b => b.Breadth.CoveredBuckets)
            .ThenByDescending(b => b.SupporterIds.Count)
            .ToList();

        if (broad.Count == 0)
            return new ForkResult(ForkClassification.None, Array.Empty<Basin>());

        // Greedily collect basins that are pairwise non-overlapping (distinct camps).
        var distinct = new List<Basin>();
        foreach (var b in broad)
        {
            if (distinct.All(d => AreNonOverlapping(d, b, opt.OverlapTolerance)))
                distinct.Add(b);
        }

        if (distinct.Count >= 2)
            return new ForkResult(ForkClassification.Fork, distinct);

        // One broad camp (possibly several overlapping versions of the same camp).
        return new ForkResult(ForkClassification.Convergent, new[] { broad[0] });
    }

    private static bool AreNonOverlapping(Basin a, Basin b, double tolerance)
    {
        var sa = a.SupporterIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sb = b.SupporterIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shared = sa.Count(id => sb.Contains(id));
        var smaller = Math.Min(sa.Count, sb.Count);
        if (smaller == 0) return true;
        return (double)shared / smaller <= tolerance;
    }
}
