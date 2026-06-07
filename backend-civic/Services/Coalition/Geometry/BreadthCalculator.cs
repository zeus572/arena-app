namespace Civic.API.Services.Coalition.Geometry;

/// <summary>
/// The league's COMPOSED spectrum: the set of Values-axis segments ("buckets")
/// the league is designed to span. Breadth is measured against THIS (a known,
/// supplied fixture — leagues are Layer 3), not against the self-selected set of
/// people who happened to respond (doc 06: avoids sampling artifacts).
///
/// Recorded assumption: the spectrum is modeled as a flat set of bucket ids and
/// each player carries their bucket (<see cref="PlayerGeometry.SpectrumBucket"/>).
/// Multi-axis spectra (doc 06 "still open") can be encoded as composite bucket
/// ids later without changing this interface.
/// </summary>
public sealed class ComposedSpectrum
{
    private readonly List<string> _ordered;
    private readonly HashSet<string> _set;

    public ComposedSpectrum(IEnumerable<string> buckets)
    {
        _set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _ordered = new List<string>();
        foreach (var b in buckets)
            if (_set.Add(b)) _ordered.Add(b); // preserve insertion order, de-duplicated
    }

    public int TotalBuckets => _ordered.Count;
    /// <summary>Buckets in stable insertion order (so the spectrum bar renders deterministically).</summary>
    public IReadOnlyList<string> Buckets => _ordered;
    public bool Contains(string bucket) => _set.Contains(bucket);
}

/// <summary>Result of <see cref="BreadthCalculator.Breadth"/>.</summary>
public sealed record BreadthResult(
    int CoveredBuckets,
    int TotalBuckets,
    double Coverage,
    IReadOnlyList<string> Covered,
    IReadOnlyList<string> Uncovered);

/// <summary>
/// Phase 1.2 — breadth. Pure computation (no LLM).
///
/// breadth(coalition) = the Values-axis span of the SIGNERS, measured as how many
/// distinct buckets of the composed spectrum they cover. It is coverage-based, so
/// it IGNORES HEADCOUNT: adding another signer in an already-covered bucket does
/// not change breadth (doc 06: "Adding signers in an already-covered region does
/// nothing").
/// </summary>
public static class BreadthCalculator
{
    public static BreadthResult Breadth(IEnumerable<PlayerGeometry> signers, ComposedSpectrum spectrum)
    {
        var covered = signers
            .Select(s => s.SpectrumBucket)
            .Where(b => b is not null && spectrum.Contains(b))
            .Select(b => b!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = spectrum.Buckets.Where(b => !covered.Contains(b)).ToList();
        var coverage = spectrum.TotalBuckets == 0 ? 0.0 : (double)covered.Count / spectrum.TotalBuckets;

        return new BreadthResult(covered.Count, spectrum.TotalBuckets, coverage,
            covered.ToList(), uncovered);
    }
}
