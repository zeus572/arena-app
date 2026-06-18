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
    /// <summary>Ready-to-publish post body (a few sentences; see CivicCampaignOptions.ResponseMaxChars).</summary>
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
    /// <summary>
    /// Bump this whenever the prompt or option shape changes meaningfully. Cached options with an
    /// older version are regenerated on next view, so improvements roll out without a manual wipe.
    /// </summary>
    public const int Version = 3;

    public static (string System, string User) Build(VirtualCandidate candidate, Briefing briefing, int optionCount, int maxChars)
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
            choose between. Each is a genuinely different strategic stance — e.g. go on the attack,
            stay disciplined and on-message, pivot to a signature strength, or stake out common
            ground. Make them feel like real, sharply-worded campaign statements with a clear point
            of view: punchy, opinionated, and willing to be provocative or take a controversial
            angle. They should read like something that would actually make news — not bland,
            hedged, or interchangeable.

            Length & substance (IMPORTANT — most drafts are too short and shallow; go deeper):
            - Each "body" MUST be a meaty statement of FOUR TO SIX full sentences, between 350 and
              {{maxChars}} characters. Anything under ~300 characters is too thin — reject your own
              draft and expand it. This is a speech-style statement, NOT a tweet or a slogan.
            - Build a real argument: (1) a hook that reacts to the specific story, (2) a concrete
              claim with a specific reason or example, (3) the candidate's position tied to a named
              policy plank, and (4) a sharp, memorable closing line. Name specifics from the story
              and the candidate's platform — avoid generic filler that could apply to any issue.
            - Distinct voice per option — the tones, arguments, and angles should clearly differ.

            Hard rules (non-negotiable):
            - FICTIONAL candidate — never claim to be a real person.
            - Plain text. No markdown, no hashtags spam.
            - Controversial/provocative is welcome, but: no slurs, no hate, no naming or targeting of
              real individuals, no endorsement of violence, and no real-election voting instructions.
            - Stay in character and consistent with the candidate's platform.
            - The options must be genuinely different stances, not rewordings.
            - Respond with ONLY a single JSON object, no prose or fences.

            Output JSON shape:
            {
              "options": [
                {
                  "label": "<2-4 word strategic label, e.g. 'Go on offense'>",
                  "angle": "<one sentence describing the strategy>",
                  "tone": "<one of: Stern, Angry, Casual, Hopeful, Sarcastic, Presidential, Folksy, Wonkish>",
                  "body": "<the statement, 4-6 full sentences, 350-{{maxChars}} chars>"
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
