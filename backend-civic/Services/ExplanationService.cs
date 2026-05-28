using Civic.API.Models;

namespace Civic.API.Services;

public interface IExplanationService
{
    /// <summary>Plain-English insights about a profile, ordered most-confident first.</summary>
    List<string> InsightsForProfile(UserProfile profile, ICivicCatalog catalog);

    /// <summary>Axis keys whose magnitude is small enough to count as "uncertain."</summary>
    List<string> UncertainAxes(UserProfile profile);

    /// <summary>Gentle framing copy for a detected tension. Single sentence per spec §13.</summary>
    string FrameTension(DetectedTension tension, AxisDefinition axis);
}

/// <summary>
/// Default rule-based explanations. No LLM dependency. An LLM-backed
/// implementation can be slotted in via DI without changing callers.
/// </summary>
public class RuleBasedExplanationService : IExplanationService
{
    private const double Threshold = 0.25;

    public List<string> InsightsForProfile(UserProfile profile, ICivicCatalog catalog)
    {
        var insights = new List<string>();

        var scoresByAxis = profile.AxisScores.ToDictionary(s => s.AxisKey);
        var ordered = catalog.Axes
            .Select(a => new
            {
                Axis = a,
                Score = scoresByAxis.TryGetValue(a.Key, out var s) ? s : null,
            })
            .Where(x => x.Score is { SupportingAnswerIds.Length: > 0 })
            .OrderByDescending(x => Math.Abs(x.Score!.Score))
            .ToList();

        foreach (var row in ordered)
        {
            var s = row.Score!;
            if (Math.Abs(s.Score) < Threshold) continue;
            var leaning = s.Score > 0 ? row.Axis.HighLabel : row.Axis.LowLabel;
            var qualifier = Math.Abs(s.Score) > 0.7 ? "strongly" : "moderately";
            insights.Add(
                $"On {row.Axis.Name.ToLowerInvariant()}, you {qualifier} lean toward " +
                $"{leaning.ToLowerInvariant()}.");
        }

        if (profile.ArchetypeBlend is { Count: > 0 })
        {
            var top = profile.ArchetypeBlend.OrderByDescending(b => b.Percent).First();
            var topDef = catalog.ArchetypeFor(top.ArchetypeKey);
            if (topDef is not null && top.Percent > 0)
            {
                insights.Add(
                    $"Your strongest civic tendency right now is \"{topDef.Name}\" — " +
                    $"about {(int)Math.Round(top.Percent)}% of your profile.");
            }
        }

        if (insights.Count == 0)
        {
            insights.Add("Answer a few more questions and we'll have something to say about your profile.");
        }
        return insights;
    }

    public List<string> UncertainAxes(UserProfile profile) =>
        profile.AxisScores
            .Where(s => s.SupportingAnswerIds.Length == 0 || Math.Abs(s.Score) < Threshold)
            .Select(s => s.AxisKey)
            .ToList();

    public string FrameTension(DetectedTension tension, AxisDefinition axis) =>
        $"On {axis.Name.ToLowerInvariant()}, some of your answers pull toward " +
        $"{axis.LowLabel.ToLowerInvariant()} while others lean toward " +
        $"{axis.HighLabel.ToLowerInvariant()}. That's not necessarily a contradiction — " +
        "it may mean your real value sits between these or depends on context.";
}
