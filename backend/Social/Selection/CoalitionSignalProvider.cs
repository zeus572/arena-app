using Arena.API.Data;
using Arena.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Arena.API.Social.Selection;

/// <summary>
/// Deterministic, LLM-free coalition breadth + bipartisan signal (SocialPublisher_Spec §2.1).
///
/// DISCOVERY RESULT (Phase 2, §2.1 step 1):
///   • No coalition entity exists. The closest analog is a completed `common_ground`-format
///     debate, whose member set is its two agents {Proponent, Opponent}. A "coalitionId" is
///     therefore a Debate.Id.
///   • No breadth/spread signal exists (DebateAggregate.DiversityScore is hardcoded, not a
///     values spread).
///   • No Values Profile / values-axis position exists on Agent. The only per-agent numeric
///     vector available is the five personality traits (0..10).
///
/// PATH TAKEN: deterministic FALLBACK (§2.1 step 3). Member positions are derived from the
/// available per-agent vector via <see cref="GetValuesPosition"/> — the SINGLE swap point.
/// When the real Values Profile geometry lands, rebind only that method; <see cref="ComputeBreadth"/>
/// and <see cref="IsBipartisanInternal"/> stay unchanged.
/// </summary>
public sealed class CoalitionSignalProvider : ICoalitionSignalProvider
{
    private readonly ArenaDbContext _db;
    private readonly SocialPublisherOptions _options;

    /// <summary>Normalized-axis midpoint. Trait scale is 0..1 after normalization, so the mid is 0.5.</summary>
    private const double AxisMidpoint = 0.5;

    /// <summary>Max trait value on the source scale (Agent personality traits are 0..10).</summary>
    private const double TraitScaleMax = 10.0;

    public CoalitionSignalProvider(ArenaDbContext db, SocialPublisherOptions options)
    {
        _db = db;
        _options = options;
    }

    public bool TryGetBreadth(Guid coalitionId, out double breadthNormalized)
    {
        breadthNormalized = 0;
        var positions = LoadMemberPositions(coalitionId);
        if (positions.Count < 2) return false;
        breadthNormalized = ComputeBreadth(positions);
        return true;
    }

    public bool IsBipartisan(Guid coalitionId)
    {
        var positions = LoadMemberPositions(coalitionId);
        return positions.Count >= 2 && IsBipartisanInternal(positions);
    }

    // -----------------------------------------------------------------------
    // The swappable geometry. Pure functions over position vectors — no DB, no LLM.
    // Made internal-static so they are unit-testable with hand-computed values (Gate 2).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Normalized breadth of a coalition's member positions: the max pairwise Euclidean distance
    /// across the values axes, normalized to 0..1 by the diameter of the unit hypercube
    /// (sqrt(numAxes)). Larger spread → broader coalition.
    /// </summary>
    internal static double ComputeBreadth(IReadOnlyList<double[]> memberPositions)
    {
        if (memberPositions.Count < 2) return 0;
        var dims = memberPositions[0].Length;
        if (dims == 0) return 0;

        double maxDist = 0;
        for (var i = 0; i < memberPositions.Count; i++)
        for (var j = i + 1; j < memberPositions.Count; j++)
        {
            double sumSq = 0;
            for (var d = 0; d < dims; d++)
            {
                var delta = memberPositions[i][d] - memberPositions[j][d];
                sumSq += delta * delta;
            }
            maxDist = Math.Max(maxDist, Math.Sqrt(sumSq));
        }

        var diameter = Math.Sqrt(dims); // max distance between two corners of a [0,1]^dims cube
        return Math.Clamp(maxDist / diameter, 0.0, 1.0);
    }

    /// <summary>Bipartisan iff members fall on both sides of at least one axis's midpoint.</summary>
    internal static bool IsBipartisanInternal(IReadOnlyList<double[]> memberPositions)
    {
        if (memberPositions.Count < 2) return false;
        var dims = memberPositions[0].Length;
        for (var d = 0; d < dims; d++)
        {
            var anyBelow = false;
            var anyAbove = false;
            foreach (var pos in memberPositions)
            {
                if (pos[d] < AxisMidpoint) anyBelow = true;
                else if (pos[d] > AxisMidpoint) anyAbove = true;
            }
            if (anyBelow && anyAbove) return true;
        }
        return false;
    }

    /// <summary>
    /// SINGLE SWAP POINT. Maps an agent to a values-axis position in [0,1]^n.
    /// FALLBACK binding: the five personality traits, normalized. Replace with the real
    /// Values Profile acceptance-set / axis-score representation when it exists.
    /// </summary>
    internal static double[] GetValuesPosition(Agent a) => new[]
    {
        a.Aggressiveness / TraitScaleMax,
        a.Eloquence      / TraitScaleMax,
        a.FactReliance   / TraitScaleMax,
        a.Empathy        / TraitScaleMax,
        a.Wit            / TraitScaleMax,
    };

    private List<double[]> LoadMemberPositions(Guid coalitionId)
    {
        // coalitionId == Debate.Id of a common_ground debate; members are its two agents.
        var debate = _db.Debates
            .AsNoTracking()
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .FirstOrDefault(d => d.Id == coalitionId);

        if (debate?.Proponent == null || debate.Opponent == null)
            return new List<double[]>();

        return new List<double[]>
        {
            GetValuesPosition(debate.Proponent),
            GetValuesPosition(debate.Opponent),
        };
    }
}
