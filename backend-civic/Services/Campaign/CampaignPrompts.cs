using System.Text;
using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Builds the (System, User) prompt pair for generating a single campaign post.
/// The system prompt anchors the persona + hard rules; the user prompt carries
/// the trigger (briefing), the resolved tone/intensity, and the candidate's
/// citable planks and source-library items.
/// </summary>
public static class CampaignPrompts
{
    private const string HardRules = """
        Hard rules (non-negotiable):
        - This is a FICTIONAL candidate in a civic-education simulation. Never claim to be a real person.
        - Body MUST be 160 characters or fewer. Shorter is better.
        - Reference at least one of the candidate's platform planks or source-library items (by its idea, not a citation footnote).
        - Even at high intensity: no slurs, no targeting or naming of real individuals, no endorsement of violence, no calls to register or vote that could be mistaken for real-election guidance.
        - Plain text only. No markdown, no hashtags spam (one topical hashtag max), at most 2 emoji.
        - Stay in character and on the candidate's platform. Sharp, never cruel.
        - Respond with ONLY a single JSON object, no prose or fences.
        """;

    public static (string System, string User) CampaignPost(
        VirtualCandidate candidate,
        CampaignTone tone,
        int intensity,
        IReadOnlyList<string> issueTags,
        IReadOnlyList<PlatformPlank> planks,
        IReadOnlyList<CandidateSource> sources,
        Briefing? briefing,
        bool lengthReminder = false)
    {
        var office = candidate.Office switch
        {
            CandidateOffice.President => "running for President",
            CandidateOffice.Senate => $"running for U.S. Senate in {candidate.State}",
            CandidateOffice.House => $"running for U.S. House, {candidate.State}-{candidate.District}",
            _ => "a candidate",
        };

        var system = $$"""
            You are writing a single short campaign post AS a fictional candidate for a civic-literacy platform.

            Candidate: {{candidate.Name}} ({{candidate.Party}}), {{office}}.
            Background: {{candidate.Background}}

            {{HardRules}}

            Output JSON shape:
            {
              "body": "<the post, <= 160 chars, in the requested tone>",
              "citedReference": "<title of the plank or source you drew on>"
            }
            """;

        var sb = new StringBuilder();
        sb.AppendLine($"Tone: {CampaignToneGuide.Label(tone)} — {CampaignToneGuide.ToneInstruction(tone)}");
        sb.AppendLine($"Intensity {intensity} ({CampaignToneGuide.IntensityLabel(intensity)}): {CampaignToneGuide.IntensityInstruction(intensity)}");
        sb.AppendLine($"Issue focus: {string.Join(", ", issueTags.DefaultIfEmpty("general"))}");
        sb.AppendLine();

        if (briefing is not null)
        {
            sb.AppendLine("Respond to this news briefing (real news; your post is the fictional reaction):");
            sb.AppendLine($"- Headline: {briefing.Headline}");
            sb.AppendLine($"- Summary: {briefing.Summary30}");
            sb.AppendLine($"- Values in conflict: {string.Join(", ", briefing.ValuesInConflict)}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("No news trigger — make a platform statement that advances the candidate's agenda.");
            sb.AppendLine();
        }

        if (planks.Count > 0)
        {
            sb.AppendLine("Platform planks you can draw on:");
            foreach (var p in planks.Take(4))
            {
                sb.AppendLine($"- {p.Title}: {Trim(p.Body, 240)}");
            }
            sb.AppendLine();
        }

        if (sources.Count > 0)
        {
            sb.AppendLine("Source library excerpts you can draw on:");
            foreach (var s in sources.Take(4))
            {
                sb.AppendLine($"- [{s.Kind}] {s.Title}: {Trim(s.Excerpt, 240)}");
            }
            sb.AppendLine();
        }

        if (lengthReminder)
        {
            sb.AppendLine("REMINDER: your previous attempt was too long. The body MUST be 160 characters or fewer. Count carefully and cut hard.");
            sb.AppendLine();
        }

        sb.Append("Produce the campaign post JSON.");

        return (system, sb.ToString());
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
