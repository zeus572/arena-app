using Civic.API.Models;

namespace Civic.API.Services.Coalition;

/// <summary>
/// Prompt builders for the coalition game's LLM tiers. Each returns the
/// (System, User) pair its call expects. Phase 0.2 uses <see cref="ProvisionBirth"/>;
/// Phase 0.3 uses <see cref="Extract"/>.
/// </summary>
public static class CoalitionPrompts
{
    private const string SharedRules = """
        - Treat all sides fairly. No partisan framing, no editorial slant.
        - Respond with ONLY a single JSON object. No prose, no markdown fences.
        - Use plain ASCII quotes and apostrophes inside string values.
        """;

    /// <summary>
    /// Provision birth (Phase 0.2). Turns a briefing into ONE neutral-surface,
    /// real-tradeoff proposition, its initial sub-questions, and its Values-axis
    /// tags — a single birth call (A5: one LLM call at birth).
    ///
    /// The craft (doc 03 §1): neutral surface, real tradeoff underneath,
    /// specific enough to position on. NOT a partisan slogan, NOT a toothless
    /// platitude everyone already agrees with.
    /// </summary>
    public static (string System, string User) ProvisionBirth(Briefing b) =>
    (
        $$"""
        You frame "provisions" for a civic coalition-building game. A provision is a SINGLE
        concrete proposition drawn from a news briefing that players will then take positions
        on, amend, and try to build a cross-spectrum coalition around.

        A good provision is:
          - NEUTRAL ON ITS SURFACE: phrased so neither side would call it loaded. It states a
            proposal or question, not a verdict. Avoid value-laden adjectives.
          - A REAL TRADEOFF underneath: reasonable people across the spectrum genuinely disagree;
            it costs something to whichever side concedes. NOT a platitude everyone endorses.
          - SPECIFIC ENOUGH TO POSITION ON: concrete enough to constrain a real institution's
            behavior (it has teeth), not a vague "we should think about X".

        Then identify the SUB-QUESTIONS: the latent dimensions of disagreement that any worded
        version of this provision implicitly resolves (e.g. for a data-center grid fee: which
        facilities are covered? marginal or average cost? are existing facilities grandfathered?).
        Each sub-question is a real crux with 2+ discrete positions. Give 2-5 of them.

        Then tag the relevant VALUES AXES: the 1-3 spectrum axes on which people will spread
        (slug-style, e.g. "liberty-vs-order", "market-vs-regulation", "local-vs-national",
        "individual-vs-collective", "change-vs-tradition"). These measure breadth later.

        {{SharedRules}}

        Output JSON shape:
        {
          "title": "<short neutral title, <= 90 chars>",
          "neutralText": "<1-3 sentence neutral-surface proposition with a real tradeoff and teeth>",
          "relevantAxes": ["<1-3 slug-style values axes>"],
          "subQuestions": [
            {
              "key": "<short stable slug, e.g. 'facility-scope'>",
              "prompt": "<the crux phrased as a question>",
              "tradeoff": "<one line naming what each side gives up>",
              "positionOptions": ["<2-4 short discrete position labels, slug-style>"]
            }
          ]
        }
        """,
        $$"""
        Briefing:
        - Headline: {{b.Headline}}
        - Institution / branch: {{b.Institution}} / {{b.Branch}}
        - Status: {{b.Status}}
        - Summary: {{b.Summary30}}
        - What changed: {{b.WhatChanged}}
        - Why it matters: {{b.WhyItMatters}}
        - What people disagree about: {{b.Disagreement}}
        - Strongest argument FOR: {{b.StrongestArgumentFor}}
        - Strongest argument AGAINST: {{b.StrongestArgumentAgainst}}
        - Values in conflict: {{string.Join(", ", b.ValuesInConflict)}}

        Produce the provision birth JSON.
        """
    );

    /// <summary>
    /// Extraction (Phase 0.3). Maps a free-form version's text to resolved
    /// positions on the currently-known sub-questions, and flags any NEW
    /// sub-question the text introduces that isn't in the known set (A4).
    /// </summary>
    public static (string System, string User) Extract(string versionText, IReadOnlyList<SubQuestion> knownSubQuestions)
    {
        var known = knownSubQuestions.Count == 0
            ? "(none yet)"
            : string.Join("\n", knownSubQuestions.Select(q =>
                $"- key=\"{q.Key}\": {q.Prompt}" +
                (q.PositionOptions.Length > 0 ? $" [options: {string.Join(", ", q.PositionOptions)}]" : "")));

        return
        (
            $$"""
            You are the EXTRACTION step of a civic coalition game. You map a free-form version of a
            provision to a STRUCTURED representation the game computes over: for each KNOWN
            sub-question, what position (if any) does THIS text take? And does the text introduce a
            crux that is NOT in the known list?

            Rules:
              - Only resolve a sub-question if the text actually takes a position on it. If the text
                is silent on a sub-question, OMIT it from "positions" (do not guess).
              - Prefer a known sub-question's existing option labels when they fit; otherwise use a
                short slug-style label that captures the position.
              - "newSubQuestions" = cruxes the text raises that are genuinely absent from the known
                list (a bargaining surprise). Use [] when there are none. Do NOT duplicate a known
                sub-question under a new key.
            {{SharedRules}}

            Output JSON shape:
            {
              "positions": { "<knownSubQuestionKey>": "<position label>", ... },
              "newSubQuestions": [
                {
                  "key": "<short stable slug>",
                  "prompt": "<the new crux as a question>",
                  "tradeoff": "<one line>",
                  "positionOptions": ["<2-4 labels>"],
                  "positionInThisText": "<the label THIS text takes on the new crux>"
                }
              ]
            }
            """,
            $$"""
            Known sub-questions:
            {{known}}

            Version text:
            ---
            {{versionText}}
            ---

            Produce the extraction JSON.
            """
        );
    }
}
