using System.Text;
using Civic.API.Models;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Prompts for the room drafting pipeline.
///
/// Versioned: <see cref="Version"/> is stamped onto every drafted room as
/// <c>DraftPromptVersion</c>, so a batch of drafts that all went wrong the same way can be
/// identified and re-run without guessing which wording produced them. Bump it whenever the
/// wording changes in a way that could change the output.
/// </summary>
public static class RoomPrompts
{
    public const int Version = 1;

    /// <summary>
    /// The rules that make a draft reviewable rather than merely plausible.
    ///
    /// Most of these exist because the opposite behaviour is what a helpful model does by
    /// default: it smooths a request into a payment, it turns an absence of evidence into a
    /// hedge, and it writes a confident status sentence about a bill that has passed one
    /// chamber. Each line here is a specific failure this product cannot ship.
    /// </summary>
    private const string System = """
You draft structured explainers about U.S. policy for a civics platform. You are drafting
for a human reviewer, not for publication. Accuracy and restraint matter more than fluency.

Hard rules:

1. FUNDING STAGES. Federal money passes through five distinct legal stages: requested,
   authorized, appropriated, obligated, spent. Never describe money at one stage using the
   verbs of a later one. A bill that has passed one chamber has provided nothing. A
   contingency provision has released nothing. Only money that has left the Treasury has
   been "spent".

2. NAME THE STAGE OF EVERY ACTION. "Passed the House", "announced an agreement", "was
   signed into law" are different events. Do not collapse them into "Congress decided".

3. CLAIMS MUST BE SETTLEABLE. Every claim you extract needs a concrete, objective
   whatWouldSettleIt — a document, a vote record, an audit. If you cannot name what would
   settle it, do not emit the claim.

4. STATUS HONESTLY.
   - Confirmed: a primary document or an uncontested public record establishes it.
   - StronglySupported: multiple independent reports, no primary document held.
   - PlausibleButUnresolved: reported, but the thing that would settle it does not exist yet.
   - Disputed: informed parties disagree about the reading, not the facts.
   - Unsupported: asserted with no evidence offered. This does NOT mean false.
   - False: contradicted by evidence.
   - Outdated: was true, superseded by a later figure or event.
   - Prediction: resolves on a future date.
   Prefer the weaker status when unsure. Nothing you draft is Confirmed on the strength of
   a news report alone.

5. SUPPORTING PASSAGES ARE VERBATIM. supportingPassage must be an exact span copied from
   the source text, character for character. Do not tidy, shorten or paraphrase it. If no
   exact span supports the claim, leave it empty rather than inventing one.

6. NO MOTIVE. Report what actors said and did. Do not explain why they "really" did it.
   An assertion about someone's motive is at best an Opinion claim and usually Unsupported.

7. CONTESTED WORDS. Avoid "cut", "spending", "shutdown", "weaponization" and similar
   loaded terms as your own voice. Name the stage or the legal event instead. Where the
   source uses such a word, you may quote it.

8. NO INVENTION. Everything you write must be supported by the source text provided. If a
   section has nothing to say, return it empty. An empty section is reviewable; a plausible
   fabrication is not.

Return ONLY JSON matching the requested shape.
""";

    /// <summary>Draft a Story Room from a briefing's prose.</summary>
    public static (string System, string User) DraftFromBriefing(Briefing briefing, string themeTitle)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Theme room this belongs to: {themeTitle}");
        sb.AppendLine();
        sb.AppendLine("SOURCE DOCUMENT");
        sb.AppendLine($"Headline: {briefing.Headline}");
        sb.AppendLine($"Institution: {briefing.Institution} ({briefing.Branch})");
        sb.AppendLine($"Status: {briefing.Status}");
        sb.AppendLine($"Key concept: {briefing.KeyConcept}");
        sb.AppendLine($"Who acted: {briefing.WhoActed}");
        sb.AppendLine($"What changed: {briefing.WhatChanged}");
        sb.AppendLine($"Why it matters: {briefing.WhyItMatters}");
        sb.AppendLine($"Disagreement: {briefing.Disagreement}");
        sb.AppendLine($"Strongest argument for: {briefing.StrongestArgumentFor}");
        sb.AppendLine($"Strongest argument against: {briefing.StrongestArgumentAgainst}");
        sb.AppendLine();
        sb.AppendLine("Explainer text:");
        sb.AppendLine(briefing.Summary3Min);
        sb.AppendLine();
        sb.AppendLine(briefing.Summary10Min);
        sb.AppendLine();
        sb.Append(Shape());

        return (System, sb.ToString());
    }

    /// <summary>Draft a Story Room from a bill's summary text.</summary>
    public static (string System, string User) DraftFromBill(Bill bill, string themeTitle)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Theme room this belongs to: {themeTitle}");
        sb.AppendLine();
        sb.AppendLine("SOURCE DOCUMENT");
        sb.AppendLine($"Bill: {bill.ShortTitle ?? bill.Title}");
        sb.AppendLine($"Full title: {bill.Title}");
        sb.AppendLine($"Sponsor: {bill.Sponsor}");
        sb.AppendLine($"Status: {bill.Status}");
        sb.AppendLine($"Introduced: {bill.IntroducedDate:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("Summary:");
        sb.AppendLine(bill.Summary);
        sb.AppendLine();
        sb.Append(Shape());

        return (System, sb.ToString());
    }

    private static string Shape() => """
Return JSON:

{
  "title": "plain, specific, no more than 90 characters",
  "dek": "one or two sentences saying what happened and at what stage",
  "storyType": "Legislative | ExecutiveAction | Court | Economic | Regulatory | Military | Diplomatic | Investigation",
  "howItWorksIntro": "how the mechanism at issue actually works, 2-4 sentences",
  "whyItMatters": [
    { "dimension": "Legal|Institutional|Financial|Human|Immediate|Longer term",
      "text": "one sentence", "claimIndex": null }
  ],
  "stakeholders": [
    { "group": "who", "impactSummary": "what changes for them",
      "confidence": 0.0-1.0 }
  ],
  "nextSteps": [
    { "description": "what happens next", "verificationCondition": "objective test",
      "expectedTiming": "optional" }
  ],
  "claims": [
    { "text": "one checkable proposition",
      "kind": "Factual|Interpretation|Opinion|Prediction",
      "status": "Confirmed|StronglySupported|PlausibleButUnresolved|Disputed|Unsupported|False|Outdated|Prediction",
      "evidenceSummary": "what the evidence is and what it is missing",
      "whatWouldSettleIt": "the document or event that would settle it",
      "supportingPassage": "exact verbatim span from the source, or empty" }
  ],
  "contestedTermsNoticed": ["any loaded terms you saw in the source"]
}

Emit between two and six claims. Omit any section you cannot support.
""";
}
