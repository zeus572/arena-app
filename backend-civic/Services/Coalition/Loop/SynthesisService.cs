using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>The synthesized plank: its configuration (point), a rendered text, and its would-be signers.</summary>
public sealed record SynthesisResult(VersionPoint Plank, string Text, IReadOnlyList<string> WouldBeSigners);

/// <summary>
/// Phase 2.3 — near-coalition synthesis. Pure: the plank is SELECTED from the live
/// amendment versions (the bounded, precomputed set — A5 precomputed-choices
/// discipline) as the best toothful version that sits in enough acceptance regions
/// and spans the spectrum. The plank text is rendered from the chosen configuration
/// by a template here; producing polished prose is the LLM seam (deferred), and it
/// does NOT change the structured plank the geometry/gates operate on.
/// </summary>
public static class SynthesisService
{
    public static SynthesisResult? Synthesize(ProvisionLoopState s)
    {
        var required = s.RequiredPlayers;
        var candidates = s.Versions.Where(v => v.Specificity >= s.Config.MinTeethSpecificity).ToList();

        var best = DistanceCalculator.DistanceToCoalition(candidates, required);
        if (best.BestVersion is null || best.Uncovered > s.Config.NearCoalitionMaxUncovered)
            return null; // no spanning plank to synthesize yet

        var supporters = OverlapCalculator.Supporters(required, best.BestVersion);
        var breadth = BreadthCalculator.Breadth(supporters, s.Spectrum);
        if (breadth.CoveredBuckets < s.Config.NearCoalitionMinBuckets)
            return null;

        return new SynthesisResult(best.BestVersion, RenderText(best.BestVersion),
            supporters.Select(p => p.UserId).ToList());
    }

    /// <summary>Deterministic template rendering of the plank's configuration (LLM prose is a deferred seam).</summary>
    private static string RenderText(VersionPoint plank)
    {
        var clauses = plank.Positions
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} = {kv.Value}");
        return "Synthesized plank — " + string.Join("; ", clauses);
    }
}
