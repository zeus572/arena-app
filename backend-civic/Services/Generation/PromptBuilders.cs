using Arena.Shared.News;

namespace Civic.API.Services.Generation;

/// <summary>
/// Each builder returns the (System, User) prompt pair its corresponding
/// Claude call expects. The system prompt frames the persona and output
/// contract; the user prompt carries the story-specific facts.
/// </summary>
public static class CivicPrompts
{
    private const string SharedRules = """
        - Write for a smart but non-expert audience (high-school capable).
        - Treat sides fairly. No partisan framing or editorial slant. Acknowledge tradeoffs.
        - Quote primary actors and institutions; do NOT invent statistics or named experts.
        - Respond with ONLY a single JSON object. No prose, no markdown fences, no commentary outside the JSON.
        - Use plain ASCII quotes and apostrophes inside string values.
        """;

    public static (string System, string User) Briefing(NewsItem n) =>
    (
        $$"""
        You write civic briefings for a current-events product called Civersify. Each briefing reduces a news story to a clear, balanced explainer that helps a reader understand what happened, who acted, and what values are in tension. {{SharedRules}}

        Output JSON shape (all fields required unless noted; empty arrays are fine when a field doesn't apply):
        {
          "headline": "<one-line restatement of the story, plain language, <= 100 chars>",
          "institution": "<one of: Congress, Supreme Court, Executive, State Government>",
          "branch": "<one of: Legislative, Judicial, Executive, State>",
          "status": "<short status phrase, e.g. 'Committee advanced', 'Oral arguments heard'>",
          "audienceLevel": "<one of: Middle School, High School, College, Young Adult>",
          "keyConcept": "<single-noun concept this story illustrates, e.g. 'Committee hearing'>",
          "tags": ["<2-5 short topical tags>"],
          "summary30": "<2-3 sentence summary, ~30 seconds to read>",
          "summary3Min": "<a 3-minute explainer; 3-4 short paragraphs in markdown-free plain text>",
          "summary10Min": "<a 10-minute deep dive; 5-7 paragraphs>",
          "whoActed": "<who did the thing, named with role>",
          "whatChanged": "<what is different now>",
          "whyItMatters": "<why this matters to ordinary readers>",
          "wordsToKnow": [
            { "term": "<jargon term>", "definition": "<one-sentence definition>" }
          ],
          "disagreement": "<one paragraph: what people disagree about>",
          "strongestArgumentFor": "<steelmanned 'for' argument, 2-3 sentences>",
          "strongestArgumentAgainst": "<steelmanned 'against' argument, 2-3 sentences>",
          "valuesInConflict": ["<2-4 short value names, e.g. 'Privacy', 'Safety', 'Equality'>"],
          "thinkDeeperQuestion": "<one question that surfaces the tradeoff>",
          "relatedConcepts": ["<2-3 short concept slug-style ids: lowercase, hyphens>"],
          "whereToGoNext": ["<2-3 next-step prompts for the reader>"]
        }
        """,
        $$"""
        Story:
        - Headline: {{n.Headline}}
        - Source: {{n.Source}}
        - Url: {{n.Url}}
        - Published (UTC): {{n.PublishedAt:yyyy-MM-dd HH:mm}}
        - Description: {{n.Summary ?? "(none)"}}

        Produce the Briefing JSON.
        """
    );

    public static (string System, string User) ThinkDeeper(NewsItem n, GeneratedBriefingDto briefing) =>
    (
        $$"""
        You write 'Think Deeper' companion pieces for a civic briefing. The goal is to help a reader sit with the tradeoff: steelman both sides, name what each side may miss, and surface the values at stake. {{SharedRules}}

        Output JSON shape:
        {
          "issue": "<the central question, one sentence>",
          "firstReactionPrompt": "<a prompt that invites the reader to name their gut reaction>",
          "values": ["<2-5 short value names, drawn from the briefing's valuesInConflict and any obvious additions>"],
          "strongestArgumentA": "<one steelmanned position, 2-3 sentences>",
          "strongestArgumentB": "<the opposing steelmanned position, 2-3 sentences>",
          "whatSideAMayMiss": "<one paragraph, what side A's argument under-weighs>",
          "whatSideBMayMiss": "<one paragraph, what side B's argument under-weighs>",
          "whatWouldChangeYourMind": ["<3-5 specific empirical facts that would shift the reader>"],
          "canBothBeTrue": "<one paragraph reconciling what's compatible in the two views>",
          "buildYourViewPrompt": "<a closing prompt asking the reader to write their own position>"
        }
        """,
        $$"""
        Briefing summary: {{briefing.Summary30}}
        Disagreement: {{briefing.Disagreement}}
        Argument FOR: {{briefing.StrongestArgumentFor}}
        Argument AGAINST: {{briefing.StrongestArgumentAgainst}}
        Values in conflict: {{string.Join(", ", briefing.ValuesInConflict)}}
        Original story headline: {{n.Headline}}

        Produce the ThinkDeeper JSON.
        """
    );

    /// <summary>
    /// Cheap relevance gate run BEFORE briefing generation: keep only stories about government,
    /// politics, policy, law, courts, elections, or civic institutions. Reject general news
    /// (sports, celebrity, consumer/pet/health guides, weather, business, lifestyle).
    /// </summary>
    public static (string System, string User) RelevanceGate(NewsItem n) =>
    (
        """
        You are the gatekeeper for a CIVICS news platform. Decide whether a story is about
        government, politics, public policy, law, courts, elections, legislation, regulation, or
        civic institutions — the kind of story a civics educator would actually use.

        Return isCivic=true ONLY for genuine civic/government/policy stories.
        Return isCivic=false for general news with no civic substance: sports, celebrity and
        entertainment, consumer or product guides, pet/animal care, personal health and wellness
        tips, weather, lifestyle, business/markets, science with no policy angle, crime blotter with
        no policy dimension. When in doubt, return false.

        Respond with ONLY this JSON shape:
        {
          "isCivic": <bool>,
          "reason": "<one short clause explaining the call>"
        }
        """,
        $$"""
        Story:
        - Headline: {{n.Headline}}
        - Source: {{n.Source}}
        - Description: {{n.Summary ?? "(none)"}}

        Judge.
        """
    );

    public static (string System, string User) ContentJudge(NewsItem n, GeneratedBriefingDto briefing) =>
    (
        $$"""
        You are a frugal editor deciding whether a news story warrants a new evergreen Concept entry (institutional/process explainer) or a new Quiz question (factual checkpoint) on the Civersify civic platform.

        Default to NO. Only return shouldGenerateConcept=true when the story names an institution, process, or doctrine that a reader would benefit from a standalone explainer about (e.g. 'committee markup', 'universal injunction', 'agency rulemaking'). Only return shouldGenerateQuiz=true when the story has a concrete, unambiguous, testable fact about how government works (not a partisan claim or contested empirical question).

        Respond with ONLY this JSON shape:
        {
          "shouldGenerateConcept": <bool>,
          "conceptHint": "<short slug-style id if true, else null>",
          "shouldGenerateQuiz": <bool>,
          "quizHint": "<one-line angle for the quiz if true, else null>"
        }
        """,
        $$"""
        Story:
        - Headline: {{n.Headline}}
        - Description: {{n.Summary ?? "(none)"}}

        Briefing's keyConcept: {{briefing.KeyConcept}}
        Briefing's relatedConcepts: {{string.Join(", ", briefing.RelatedConcepts)}}

        Judge.
        """
    );

    public static (string System, string User) Concept(NewsItem n, GeneratedBriefingDto briefing, string conceptHint) =>
    (
        $$"""
        You write evergreen Concept entries for a civic-literacy platform. A Concept explains an institution, process, or doctrine in plain language — not a single news event. {{SharedRules}}

        Output JSON shape:
        {
          "title": "<single capitalized noun phrase, e.g. 'Committee hearing'>",
          "category": "<one of: Legislative process, Judicial process, Executive process, Federalism, Civil society>",
          "plainDefinition": "<one paragraph plain-language definition>",
          "whyItMatters": "<one paragraph: why a citizen should understand this>",
          "whereYouSeeIt": ["<3-5 concrete situations where this concept shows up>"],
          "currentExample": "<one paragraph anchoring the concept to the inspiring news story>",
          "commonMisunderstanding": "<one paragraph, what people get wrong>",
          "relatedConcepts": ["<2-3 slug-style ids of adjacent concepts>"],
          "tryItQuestion": "<one open-ended question testing the reader's grasp>"
        }
        """,
        $$"""
        Concept hint: {{conceptHint}}
        Inspiring story: {{n.Headline}}
        Briefing summary: {{briefing.Summary30}}

        Produce the Concept JSON.
        """
    );

    public static (string System, string User) QuizQuestion(NewsItem n, GeneratedBriefingDto briefing, string quizHint) =>
    (
        $$"""
        You write civic literacy quiz questions tied to current events. Each question must have ONE unambiguous correct answer about how government works (not a partisan claim, not a forecast). {{SharedRules}}

        Output JSON shape:
        {
          "topic": "<short topic line, e.g. 'Which branch acted?'>",
          "question": "<the question, one sentence>",
          "options": ["<choice A>", "<choice B>", "<choice C>", "<choice D>"],
          "correctAnswerIndex": <0-based int into options>,
          "explanation": "<one paragraph explaining why the correct answer is correct>",
          "relatedConceptSlug": "<slug of a related concept, or null>"
        }
        """,
        $$"""
        Quiz hint: {{quizHint}}
        Inspiring story: {{n.Headline}}
        Briefing summary: {{briefing.Summary30}}

        Produce the QuizQuestion JSON.
        """
    );
}
