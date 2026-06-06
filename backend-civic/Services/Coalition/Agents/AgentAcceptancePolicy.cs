using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Agents;

/// <summary>The agent's acceptance decision for a version: accept?, how hard, and why.</summary>
public sealed record WouldSignResult(bool Accept, AnswerIntensity Intensity, string Reasoning)
{
    /// <summary>A decline anchored to a NonNegotiable position = principled dissent (doc 06), not a failed bridge.</summary>
    public bool IsPrincipledDissent { get; init; }
}

/// <summary>
/// Phase 2.2 — `wouldSign(agent, version)` (Part C core function). Pure geometry:
/// the agent accepts iff the version sits in its acceptance region; the intensity
/// and reasoning come from which sub-question drove the decision. This is the
/// operational definition of an acceptance set and supports honest partial
/// acceptance ("I'd accept the size-threshold version but not the blanket one").
/// </summary>
public static class AgentAcceptancePolicy
{
    public static WouldSignResult WouldSign(CoalitionAgent agent, VersionPoint version)
    {
        // Sub-questions where the version takes a position the agent will not accept.
        var violated = version.Positions
            .Where(kv => agent.Region.ConstrainedKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase)
                         && !agent.Region.AcceptableLabels(kv.Key).Contains(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        if (violated.Count == 0)
        {
            // Intensity of an acceptance = the strongest position this version positively
            // satisfies (a costly accept on a high-intensity axis is worth more later).
            var satisfiedKeys = version.Positions.Keys
                .Where(k => agent.Region.ConstrainedKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();
            var intensity = satisfiedKeys.Count == 0
                ? AnswerIntensity.Low
                : satisfiedKeys.Max(agent.IntensityFor);
            var what = satisfiedKeys.Count == 0 ? "no constrained sub-questions" : string.Join(", ", satisfiedKeys);
            return new WouldSignResult(true, intensity, $"accepts: satisfies {what}; no conflicts");
        }

        // Decline is driven by the strongest violated position.
        var bindingKey = violated.OrderByDescending(agent.IntensityFor).First();
        var bindingIntensity = agent.IntensityFor(bindingKey);
        return new WouldSignResult(
            false,
            bindingIntensity,
            $"declines: conflicts with {bindingIntensity} position on '{bindingKey}'")
        {
            IsPrincipledDissent = bindingIntensity == AnswerIntensity.NonNegotiable,
        };
    }
}
