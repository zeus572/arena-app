using System.Text;
using Civic.API.Models;
using Civic.API.Services;

namespace Civic.API.Services.Bills;

/// <summary>
/// Builds the (System, User) prompt pair for positioning a bill on the Civic
/// Compass axes. Mirrors <c>CivicPrompts</c> in the Generation namespace: the
/// system prompt frames the persona + output contract (and enumerates the live
/// axis catalog so new axes are covered automatically); the user prompt carries
/// the bill-specific facts.
/// </summary>
public static class BillPrompts
{
    private const string SharedRules = """
        - Be strictly neutral. No partisan framing, no editorializing, no advocacy. Describe, don't judge.
        - Base the positioning ONLY on what the bill actually does. Do not invent provisions or statistics.
        - Score ONLY the axes the bill genuinely implicates. Omit axes it does not touch — do not pad.
        - Respond with ONLY a single JSON object. No prose, no markdown fences, no commentary outside the JSON.
        - Use plain ASCII quotes and apostrophes inside string values.
        """;

    public static (string System, string User) Synthesis(Bill bill, ICivicCatalog catalog)
    {
        var axisLines = new StringBuilder();
        foreach (var a in catalog.Axes)
        {
            axisLines.Append("        - \"").Append(a.Key).Append("\" — ").Append(a.Name)
                .Append(": score -1.0 = ").Append(a.LowLabel)
                .Append(", +1.0 = ").Append(a.HighLabel).Append('\n');
        }

        var system =
            $$"""
            You are a neutral legislative analyst for a civic-education product. Given a bill, you place it on a fixed set of "values axes": for each axis the bill implicates, you say which direction the bill pushes (a signed score) and why, in one plain sentence. {{SharedRules}}

            The axes (use these exact keys; score is how the BILL pushes, not any person's opinion):
            {{axisLines}}

            Output JSON shape:
            {
              "summary": "<one neutral paragraph: what the bill does and the core tradeoff it raises>",
              "positions": [
                {
                  "axisKey": "<one of the exact axis keys above>",
                  "score": <number from -1.0 to 1.0>,
                  "confidence": <number from 0.0 to 1.0>,
                  "rationale": "<one sentence: why the bill lands here on this axis>",
                  "evidence": "<short quote or provision reference, or empty string>"
                }
              ]
            }
            """;

        var user =
            $$"""
            Bill:
            - Identifier: {{bill.BillType}} {{bill.Number}} ({{bill.Congress}}th Congress)
            - Title: {{bill.Title}}
            - Sponsor: {{bill.Sponsor}}{{(string.IsNullOrEmpty(bill.Party) ? "" : $" ({bill.Party})")}}
            - Status: {{bill.Status}}
            - Summary: {{(string.IsNullOrWhiteSpace(bill.Summary) ? "(none provided)" : bill.Summary)}}

            Produce the value-positioning JSON.
            """;

        return (system, user);
    }
}
