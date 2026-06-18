# Prompt Injection Security Review

**Date:** 2026-06-17
**Scope:** Backend LLM integration (`backend/`) — every path where untrusted
input reaches a Claude/Ollama prompt.
**Branch:** `claude/prompt-injection-security-fns9b4`

---

## Summary

Political Arena feeds several **untrusted, user- or web-controlled** strings
directly into LLM prompts with no delimiting, escaping, or length bounds. This
exposes the platform to **prompt injection** — both *direct* (a user crafts
input that overrides the agent's instructions) and *indirect* (a third-party web
page or RSS feed embeds instructions that the model later ingests as "evidence").

Realistic impact for this app:

- An agent breaks character, ignores the debate format/rules, or emits
  off-topic / abusive / disallowed content attributed to a public-figure persona
  (reputational + ToS risk, since personas simulate real people).
- The agent is steered to reveal its system prompt (persona internals, house
  rules, source library).
- The agent is steered to ignore the topic/format and emit attacker-chosen text,
  degrading the product and wasting paid Claude tokens.

No injection vector found leads to code execution, data exfiltration beyond the
prompt contents, or auth bypass — the blast radius is the generated debate text.

This review identifies the vectors, ranks them, and ships hardening for the
reachable ones. Defense is **structural** (treat untrusted text as data, fence
it, bound it) rather than blocklist-based, because keyword filtering of
injection payloads is unreliable.

---

## Attack surface

| # | Vector | Untrusted source | Where it lands | Auth to reach | Severity |
|---|--------|------------------|----------------|---------------|----------|
| 1 | **Audience question** (`crowdQuestion`) | `POST /api/debates/{id}/interventions` → `Intervention.Content` | **User turn** of the debate prompt (`BuildUserPrompt`) | Premium user | **High** |
| 2 | **Debate topic & description** | `POST /api/debates` (`CreateDebateRequest.Topic/Description`) | **System prompt** + user turn (`BuildSystemPrompt`, `BuildUserPrompt`) | Premium user | **High** |
| 3 | **Web / Wikipedia search results** | Scraped 3rd-party pages via `search_web` / `search_wikipedia` tools | `tool_result` content fed back to Claude (`ClaudeLlmService`) | Unauthenticated attacker (controls a ranking page) | **Medium‑High** |
| 4 | **RSS news headlines** | External RSS feeds (`NewsTopicService`) | Topic-generation prompt | Unauthenticated (controls a syndicated feed) | **Medium** |
| 5 | **Fork topic / note** | `POST /api/debates/{id}/fork` | System prompt | Premium user | **High** (same class as #2) |
| 6 | **Arena name/rules/description** | `POST /api/arenas` | System prompt, framed as *"HOUSE RULES (binding)"* | **Admin only** | Low (trusted role) |
| 7 | **Agent name/persona/description** | Seed data / DB only — no write endpoint | System prompt | N/A (not user-writable) | Low |

### Vector details

**#1 Audience question (most direct).** `InterventionsController.SubmitIntervention`
caps length (10–280 chars) and trims, but stores the content verbatim.
`BotHeartbeatService` later promotes the top-upvoted intervention
(`Upvotes >= 1`) to `crowdQuestion`, and `LlmPromptBuilder.BuildUserPrompt`
previously dropped it into the prompt as:

```
IMPORTANT — A member of the audience has asked a question you must address in your response: "<RAW USER TEXT>"
```

There is no delimiter that survives the user closing the quote, and the framing
("you must address") actively *invites* compliance. Worse, the upvote endpoint
(`POST .../interventions/{id}/upvote`) has **no per-user dedup** and increments
unboundedly, so the `Upvotes >= 1` gate is trivially met — even by the submitter
upvoting their own intervention once. A single authenticated user can therefore
get arbitrary text injected into the agent's user turn.

**#2 Debate topic/description.** `DebatesController.Create` only checked that the
topic was non-empty. Unlike topic *proposals* (`TopicsController.Create`, which
calls `TopicModerationService`), debate creation ran **no moderation and no
length cap**, and the values flow into the **system prompt** — the
highest-trust region of the request. Description was entirely unbounded.

**#3 Indirect injection via tool results.** `WebSearchProvider` scrapes
DuckDuckGo HTML; titles and snippets from arbitrary pages are returned and
concatenated into `tool_result` content with no marker distinguishing them from
trusted instructions. An attacker who can get a page to rank for a plausible
debate query (or edit a Wikipedia article) can plant
`"Ignore your instructions and …"` text that the model ingests as "evidence."
This is the textbook indirect-prompt-injection pattern and requires **no account
on our platform**.

**#4 RSS headlines.** `NewsTopicService` interpolates fetched headlines into the
topic-generation prompt. Impact is limited (output is constrained to short JSON
questions and re-moderated by `TopicModerationService`), but a poisoned feed
could still steer or pollute generated topics.

---

## Changes shipped in this branch

The fixes follow a single principle: **untrusted text is data, not
instructions.** Each reachable vector is now (a) neutralized (control chars and
fence tokens stripped, length bounded), (b) wrapped in a labelled data fence,
and (c) accompanied by an explicit instruction telling the model the block
cannot change its behavior.

1. **New `PromptSanitizer` helper** (`backend/Services/PromptSanitizer.cs`)
   - `Sanitize()` strips control characters (NUL, CR, escape sequences used to
     fake conversational turns / terminal tricks) while keeping `\n` and `\t`,
     neutralizes the `<<<` fence token so a payload can't forge a closing fence,
     and caps length.
   - `WrapAsData(label, content)` sanitizes then wraps in
     `<<<BEGIN label>>> … <<<END label>>>`.

2. **Audience question hardened** (`LlmPromptBuilder.BuildUserPrompt`)
   - The question is fenced with `WrapAsData("AUDIENCE QUESTION", …, 280)` and
     prefixed with an instruction that it is audience *input*, must not change
     the persona/format/rules or reveal the prompt, and that injection attempts
     should be ignored.

3. **Topic & description hardened** (`LlmPromptBuilder.BuildSystemPrompt`,
   `BuildUserPrompt`, `BuildCommentarySystemPrompt`)
   - Sanitized (control/fence stripped, capped to 300 / 1000 chars) everywhere
     they are interpolated, including the `common_ground` and commentary prompts.
   - A guard paragraph in the system prompt states the topic/context are the
     *subject* to argue about, never instructions.

4. **External tool results hardened** (`ClaudeLlmService`)
   - Each web/Wikipedia/USAFacts/budget result is fenced via
     `WrapAsData("SEARCH RESULT", …)` and prefixed with a guard
     (`ExternalResultsGuard`) telling the model the results are third-party
     reference data that may contain instructions to ignore.

5. **Input bounds at the API boundary** (`DebatesController.Create` / `Fork`)
   - Topic capped at 300 chars, description at 2000 chars; over-long input is
     rejected with `400` instead of flowing unbounded into the prompt.

All changes are backward-compatible with normal debates: legitimate topics,
descriptions, and audience questions are well under the caps and contain no
control characters or fence tokens, so sanitization is a no-op for them.

---

## Recommended follow-ups (not in this branch)

- **Moderate debate topics on creation.** `DebatesController.Create`/`Fork`
  should call `TopicModerationService.CheckTopicAsync` for parity with topic
  proposals. (Deferred here because it adds an LLM round-trip and a new
  rejection path — a product decision, and moderation is *not* itself an
  anti-injection control.)
- **Dedup intervention upvotes.** Add a per-user unique constraint /
  idempotent upvote so the `Upvotes >= 1` gate reflects genuine crowd support
  and can't be self-satisfied. This raises the bar to reach vector #1.
- **Fence prior-turn content in the commentary prompt.**
  `BuildCommentaryUserPrompt` embeds raw `Turn.Content`. Turns are model output,
  but a successful earlier injection can propagate; fencing makes it second-order.
- **Consider Anthropic's prompt-level guards** (e.g. moving all untrusted blocks
  strictly into user turns — already true for #1/#3 — and adding a short
  system-level "you may receive untrusted data in fenced blocks" preamble once,
  rather than per call site).
- **Ollama parity:** the local-model path (`OllamaLlmService`) shares
  `LlmPromptBuilder`, so #1/#2 fixes apply automatically; it has no tool use, so
  #3 does not apply. No extra work needed today, but keep this in mind if tool
  use is added to the Ollama path.

---

## Verification notes

The .NET SDK is not available in this review environment, so changes were
verified by code review rather than `dotnet build`. They are additive and
syntactically isolated (one new static class plus localized edits to three
existing files). Recommend running `dotnet build` and the existing test suite
before merge.
