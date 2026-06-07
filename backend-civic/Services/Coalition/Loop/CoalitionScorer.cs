using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>A passed coalition's score across the four pass dimensions.</summary>
public sealed record CoalitionScore(int Breadth, int Specificity, int MovedSigners, int Cost, double Total);

/// <summary>
/// Phase 2.5 — scores a coalition on breadth · cost · specificity · movement
/// (each in its proper space). Pure computation.
///
/// The over-breadth guard (doc 06) lives in COST: a signer's stake is the weight
/// of the strongest position they hold on the plank's resolved sub-questions. A
/// "low-intensity everywhere" agent that would accept anything constrains none of
/// the cruxes (or only at Low intensity), so it contributes ~minimal cost — its
/// cheap acceptance scores low, while a signer who put a high-intensity position on
/// the line scores high. Thus a mush coalition of cheap acceptances scores below a
/// genuinely costly cross-spectrum one of the same breadth.
/// </summary>
public static class CoalitionScorer
{
    // Intensity -> stake weight. Starting weights; Layer 3 calibrates against observed play.
    private static int Weight(AnswerIntensity i) => i switch
    {
        AnswerIntensity.Low => 1,
        AnswerIntensity.Medium => 2,
        AnswerIntensity.High => 3,
        AnswerIntensity.NonNegotiable => 4,
        _ => 1,
    };

    /// <summary>A signer's stake = the strongest position it holds among the plank's resolved cruxes (Low if none).</summary>
    public static int SignerStake(CoalitionAgent signer, VersionPoint plank)
    {
        var stakedKeys = plank.Positions.Keys
            .Where(k => signer.Region.ConstrainedKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return stakedKeys.Count == 0 ? Weight(AnswerIntensity.Low) : stakedKeys.Max(k => Weight(signer.IntensityFor(k)));
    }

    public static CoalitionScore Score(
        VersionPoint plank,
        IReadOnlyList<CoalitionAgent> signers,
        ComposedSpectrum spectrum,
        IEnumerable<string> movedSignerIds)
    {
        var moved = new HashSet<string>(movedSignerIds, StringComparer.OrdinalIgnoreCase);
        var breadth = BreadthCalculator.Breadth(signers.Select(s => s.ToPlayer()), spectrum).CoveredBuckets;
        var specificity = plank.Specificity;
        var movedCount = signers.Count(s => moved.Contains(s.UserId));
        var cost = signers.Sum(s => SignerStake(s, plank));

        // Weighted sum (not a product, to avoid a single zero dimension collapsing the score).
        // Cost carries the anti-mush guard; weights are a starting point for Layer 3 calibration.
        var total = breadth + specificity + movedCount + cost;
        return new CoalitionScore(breadth, specificity, movedCount, cost, total);
    }
}
