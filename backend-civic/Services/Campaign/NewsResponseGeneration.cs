using System.Text;
using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>The internal shape of a single cached response option (serialized to OptionsJson).</summary>
public class NewsResponseOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Angle { get; set; } = "";
    public string Tone { get; set; } = "";
    /// <summary>Ready-to-publish post body (≤160 chars).</summary>
    public string Body { get; set; } = "";
}

/// <summary>JSON shape the LLM returns for per-candidate news response options.</summary>
public class GeneratedNewsResponsesDto
{
    public List<GeneratedNewsResponseItem> Options { get; set; } = new();
}

public class GeneratedNewsResponseItem
{
    public string Label { get; set; } = "";
    public string Angle { get; set; } = "";
    public string Tone { get; set; } = "";
    public string Body { get; set; } = "";
}

/// <summary>Prompt builder for generating a candidate's response options to a news briefing.</summary>
public static class NewsResponsePrompts
{
    public static (string System, string User) Build(VirtualCandidate candidate, Briefing briefing, int optionCount)
    {
        var office = candidate.Office switch
        {
            CandidateOffice.President => "running for President",
            CandidateOffice.Senate => $"running for U.S. Senate in {candidate.State}",
            CandidateOffice.House => $"running for U.S. House, {candidate.State}-{candidate.District}",
            _ => "a candidate",
        };

        var system = $$"""
            You generate distinct strategic response options for a FICTIONAL candidate reacting to a
            real news story, for a civic-education campaign simulation.

            Candidate: {{candidate.Name}} ({{candidate.Party}}), {{office}}.
            Background: {{candidate.Background}}

            Produce exactly {{optionCount}} DISTINCT response options that a campaign manager could
            choose between. Each option is a different strategic stance (e.g. go on offense, stay
            disciplined/on-message, pivot to a strength, find common ground). Each must stay in
            character and on the candidate's platform.

            Hard rules (non-negotiable):
            - FICTIONAL candidate — never claim to be a real person.
            - Each "body" MUST be 160 characters or fewer. Plain text. No markdown.
            - No slurs, no naming/targeting real individuals, no endorsement of violence, no real-election voting instructions.
            - The options must be genuinely different stances, not rewordings.
            - Respond with ONLY a single JSON object, no prose or fences.

            Output JSON shape:
            {
              "options": [
                {
                  "label": "<2-4 word strategic label, e.g. 'Go on offense'>",
                  "angle": "<one sentence describing the strategy>",
                  "tone": "<one of: Stern, Angry, Casual, Hopeful, Sarcastic, Presidential, Folksy, Wonkish>",
                  "body": "<the post, <= 160 chars>"
                }
              ]
            }
            """;

        var sb = new StringBuilder();
        sb.AppendLine("React to this real news briefing:");
        sb.AppendLine($"- Headline: {briefing.Headline}");
        sb.AppendLine($"- Summary: {briefing.Summary30}");
        if (briefing.ValuesInConflict.Length > 0)
            sb.AppendLine($"- Values in conflict: {string.Join(", ", briefing.ValuesInConflict)}");
        if (briefing.Tags.Length > 0)
            sb.AppendLine($"- Tags: {string.Join(", ", briefing.Tags)}");
        sb.AppendLine();

        if (candidate.PlatformPlanks.Count > 0)
        {
            sb.AppendLine("The candidate's platform planks (stay consistent with these):");
            foreach (var p in candidate.PlatformPlanks.Take(4))
                sb.AppendLine($"- {p.Title}");
            sb.AppendLine();
        }

        sb.Append($"Produce the JSON with exactly {optionCount} options.");
        return (system, sb.ToString());
    }
}
