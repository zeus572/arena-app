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
    string? LeadingVersionId,
    IReadOnlyList<string> UncoveredBuckets,
    int? DaysLeft,
    string CallToAction);

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
        var uncovered = cells.Where(c => !c.Covered).Select(c => c.Bucket).ToList();

        int? daysLeft = s.Deadline is { } dl ? Math.Max(0, (int)Math.Ceiling((dl - s.Now).TotalDays)) : null;
        var cta = BuildCallToAction(s.State, covered.Count, s.Spectrum.TotalBuckets, uncovered, daysLeft, best.BestVersion is not null);

        return new SpectrumBarView(
            cells,
            covered.Count,
            s.Spectrum.TotalBuckets,
            best.Normalized,
            s.Deadline,
            best.BestVersion?.Id,
            uncovered,
            daysLeft,
            cta);
    }

    /// <summary>The directional call-to-action (doc 06 surfacing): which corner is missing + clock.</summary>
    private static string BuildCallToAction(Civic.API.Models.ProvisionState state, int covered, int total,
        IReadOnlyList<string> uncovered, int? daysLeft, bool hasSpanningVersion)
    {
        var clock = daysLeft is { } d ? (d <= 0 ? "deadline reached" : $"{d} day{(d == 1 ? "" : "s")} left") : "";
        if (state is Civic.API.Models.ProvisionState.Passed) return "Coalition passed — it spans the spectrum.";
        if (state is Civic.API.Models.ProvisionState.Forked) return "Forked into two governable answers.";
        if (state is Civic.API.Models.ProvisionState.Died) return "No bridge this week — the spectrum was too far apart.";
        if (uncovered.Count == 0 && hasSpanningVersion) return $"A spanning version is on the table — get it co-signed. {clock}".Trim();
        if (uncovered.Count == 0) return $"Engaged across the spectrum — now find a version with teeth. {clock}".Trim();
        if (uncovered.Count == total) return $"No coalition yet — table a carve-out. {clock}".Trim();
        return $"The {uncovered[0]} corner hasn't signed — one bridge to go. {clock}".Trim();
    }
}
