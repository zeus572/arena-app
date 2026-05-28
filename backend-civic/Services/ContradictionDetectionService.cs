using Civic.API.Models;

namespace Civic.API.Services;

public class DetectedTension
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public Guid[] AnswerIdsLow { get; set; } = Array.Empty<Guid>();
    public Guid[] AnswerIdsHigh { get; set; } = Array.Empty<Guid>();
}

public interface IContradictionDetectionService
{
    List<DetectedTension> Detect(IReadOnlyList<CivicAnswer> answers);
}

public class ContradictionDetectionService : IContradictionDetectionService
{
    private readonly ICivicCatalog _catalog;

    public ContradictionDetectionService(ICivicCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// Per spec §13, surface tensions where the user holds high-confidence answers
    /// that point in opposite directions on the same civic axis. We flag an axis
    /// when there is at least one strong "low" contribution and at least one strong
    /// "high" contribution from high-confidence (Somewhat or VerySure) answers.
    /// </summary>
    public List<DetectedTension> Detect(IReadOnlyList<CivicAnswer> answers)
    {
        var lowsByAxis = new Dictionary<string, List<Guid>>();
        var highsByAxis = new Dictionary<string, List<Guid>>();

        foreach (var a in answers)
        {
            if (a.Question is null) continue;
            if (a.Confidence == AnswerConfidence.NotSure) continue;

            var choice = a.Question.Choices.FirstOrDefault(c => c.Key == a.SelectedChoiceKey);
            if (choice is null) continue;

            foreach (var d in choice.AxisDeltas)
            {
                // Only count contributions with non-trivial magnitude
                if (Math.Abs(d.Delta) < 0.3) continue;

                if (d.Delta < 0)
                {
                    if (!lowsByAxis.TryGetValue(d.AxisKey, out var list))
                    {
                        list = new List<Guid>();
                        lowsByAxis[d.AxisKey] = list;
                    }
                    list.Add(a.Id);
                }
                else
                {
                    if (!highsByAxis.TryGetValue(d.AxisKey, out var list))
                    {
                        list = new List<Guid>();
                        highsByAxis[d.AxisKey] = list;
                    }
                    list.Add(a.Id);
                }
            }
        }

        var tensions = new List<DetectedTension>();
        foreach (var axis in _catalog.Axes)
        {
            lowsByAxis.TryGetValue(axis.Key, out var lows);
            highsByAxis.TryGetValue(axis.Key, out var highs);
            if (lows is { Count: > 0 } && highs is { Count: > 0 })
            {
                tensions.Add(new DetectedTension
                {
                    AxisKey = axis.Key,
                    AxisName = axis.Name,
                    AnswerIdsLow = lows.ToArray(),
                    AnswerIdsHigh = highs.ToArray(),
                });
            }
        }
        return tensions;
    }
}
