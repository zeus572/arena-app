# 01 — Fork

**The daily "would you rather," drawn from a live coalition provision.**
One tap, ~10 seconds. Read `00_OVERVIEW.md` first for the shared table, controller, and XP hook.

## The loop

1. Two options, A and B. Each is one short label plus one line naming what it costs you.
2. Tap one.
3. Immediate reveal: the national split, your locality's split, your age band's split,
   and one line naming the value axis the choice loads on.
4. Optional second tap — "why does this matter?" — expands the tradeoff line and deep-links
   to the provision it was cut from.

There is no right answer, so there is no score. The payoff is the reveal.

## Why this one matters most for growth

It is the cheapest possible entry point — no reading, no account, one tap — and it is the
only game whose output is naturally a *social object*. It also closes a standing piece of
user feedback: the current Bluesky posts are redundant because the tweet text and the card
image both say "Help bridge this one before it closes," and the ask was for the card to
carry "a *would you rather* choice drawn from the coalition." Fork is that card.

## Content source — mostly zero-LLM

`SubQuestion` (`backend-civic/Models/Coalition/SubQuestion.cs`) is already the right shape:

- `Prompt` — the question in plain language.
- `TradeoffDescription` — "one line naming the real tradeoff this crux turns on."
- `PositionOptions` — "known/expected discrete position labels," a `text[]`.

When a sub-question on a live provision has **two or more `PositionOptions`**, Fork is pure
selection: pick the provision, pick the sub-question, take two options. No LLM.

Selection rules, in `ForkGenerator`:

- Provision must be in an open lifecycle state (not resolved/dead).
- Prefer sub-questions with `Origin == Birth` and the lowest `OrderIndex` — those are the
  central cruxes, not the long-tail details.
- Never reuse a `(ProvisionId, SubQuestion.Key)` pair that has already shipped as a Fork.
  Track via `DailyPuzzle.SourceProvisionId` plus the payload's `subQuestionKey`.
- Set `SourceProvisionId` for provenance and the deep link.

**Fallback (the only LLM path in the six games).** If no eligible sub-question has two
usable options, call Haiku once to write A/B from `Prompt` + `TradeoffDescription`. Per the
existing failure semantics, a `BadResponse` fails *this puzzle only* and the generator moves
to the next candidate — it must not fail the day's batch. If `Anthropic:Enabled` is false,
skip the fallback entirely and fall through to the hand-authored bank
(`Seed/fork-fallback.json`, ~30 evergreen tradeoffs) so the slate is never empty.

## Payload contract (`PayloadVersion = 1`)

```jsonc
{
  "question": "Who should pay for the grid upgrades a new data center needs?",
  "tradeoff": "Charging the facility slows buildout; spreading it raises everyone's bill.",
  "optionA": { "label": "The facility pays",  "cost": "Fewer data centers get built here." },
  "optionB": { "label": "All ratepayers share", "cost": "Your utility bill goes up." },
  "axisKey": "economic-fairness",
  "subQuestionKey": "cost-allocation",
  "provisionSlug": "data-center-grid-fee"
}
```

Nothing here is secret — there is no answer key — so `GET` serves the payload whole.

`ResponseJson`: `{ "choice": "A" | "B" }`. `Score` is stored as `0`.

## The reveal

Three bars from `DailyPuzzlePlay` aggregates over this puzzle:

- **National** — all plays.
- **Your locality** — plays by users whose `Locality` matches the caller's. Suppress the bar
  below a minimum of 20 plays and say so ("not enough plays in OH yet") rather than showing a
  noisy 2-person split.
- **Your age band** — same rule, using the existing age-range profiling.

Then one line: *"This one turns on **Economic fairness** — market outcome vs. redistributive
correction."* Axis name and pole labels come from `ICivicCatalog.AxisFor(axisKey)`, never
hard-coded.

## Compass integration — deliberately opt-in

A Fork tap is weak signal. `CivicAnswer` carries **Confidence** and **Intensity**; a one-tap
binary carries neither. Writing Fork taps straight into `ProfileAxisScore` would quietly
degrade the quality of the compass, which is the asset the entire product rests on.

So: **do not auto-write profile axes from Fork.** Instead, after the reveal, offer an explicit
one-tap upgrade — *"Add this to your compass?"* — which opens the normal answer flow with
confidence and intensity. That converts casual players into profiled users on purpose rather
than by accident, and it is the intended conversion path out of this game.

## Neutrality rule — both options must be costly

The failure mode for "would you rather" is that one option is an applause line and the other
is a strawman. The gamification docs require provisions be "neutral-surface, real-tradeoff-
underneath"; Fork inherits that.

Generation validator, enforced before a puzzle reaches `Draft`:

1. Both options must have a non-empty `cost` string. An option with no stated cost is rejected.
2. Reject options containing party names, politician names, or the words "obviously,"
   "common sense," "extremist."
3. Fork is on the **review-required** list in the admin queue. A human approves every one.

Post-hoc audit, on `/admin/daily`: the distribution of which axis pole the *winning* option
sits on, over the trailing 30 editions. A bank where the high pole always wins is an editorial
position and should be corrected in selection.

## Share

Grid — deliberately no spoiler, since there is no answer to spoil, only a split to tease:

```
Fork #142
◧ I went A — 61% of the country went B.
civersify.com/daily
```

Bluesky card: the question as the image via `SkiaCardRenderer`, existing tweet text and link
unchanged. `SocialContentType.CivicDailyPuzzle`, `ContentId = DailyPuzzle.Id`.

## Verification

- A Fork puzzle generates with `Anthropic:Enabled=false` (falls through to the seed bank).
- A sub-question with fewer than two `PositionOptions` and no LLM available is skipped, and
  the generator still produces a puzzle from the next candidate.
- An option missing its `cost` line is rejected by the validator and never reaches `Draft`.
- The locality bar is suppressed under 20 plays.
- Tapping does **not** change `ProfileAxisScore`; taking the explicit upgrade does.
- The same `(provision, subQuestionKey)` never ships twice.
