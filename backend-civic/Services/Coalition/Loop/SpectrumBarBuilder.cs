using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>One segment of the spectrum bar: a composed-spectrum bucket and whether the coalition reaches it.</summary>
public sealed record SpectrumCell(string Bucket, bool Covered);

/// <summary>
/// The spectrum-bar surfacing of distance (doc 06's preferred surface): the
/// coalition's reach lit across the relevant axis, dark corners = unrepresented,
/// plus the distance number and deadline. The visual IS the call to action and
/// makes breadth-not-headcount visceral.
/// </summary>
public sealed record SpectrumBarView(
    IReadOnlyList<SpectrumCell> Cells,
    int CoveredBuckets,
    int TotalBuckets,
    double Distance,
    DateTime? Deadline,
    string? LeadingVersionId);

/// <summary>
/// Phase 2H.1 — builds the spectrum bar from geometry (pure, no LLM). "Covered"
/// buckets = the spectrum segments occupied by the supporters of the current best
/// spanning version; the rest are dark corners still to be bridged.
/// </summary>
public static class SpectrumBarBuilder
{
    public static SpectrumBarView Build(ProvisionLoopState s)
    {
        var required = s.RequiredPlayers;
        var candidates = s.Versions.Where(v => v.Specificity >= s.Config.MinTeethSpecificity).ToList();
        var best = DistanceCalculator.DistanceToCoalition(candidates, required);

        var supporters = best.BestVersion is null
            ? new List<PlayerGeometry>()
            : OverlapCalculator.Supporters(required, best.BestVersion).ToList();

        var covered = supporters
            .Select(p => p.SpectrumBucket)
            .Where(b => b is not null && s.Spectrum.Contains(b))
            .Select(b => b!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cells = s.Spectrum.Buckets.Select(b => new SpectrumCell(b, covered.Contains(b))).ToList();

        return new SpectrumBarView(
            cells,
            covered.Count,
            s.Spectrum.TotalBuckets,
            best.Normalized,
            s.Deadline,
            best.BestVersion?.Id);
    }
}
