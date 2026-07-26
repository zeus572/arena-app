# 06 — Whose Value

**Read an argument, name the value it appeals to.** Five rounds, ~60 seconds.
Read `00_OVERVIEW.md` first.

## The loop

1. One short argument: *"Moving permitting authority to a federal office means one standard
   instead of fifty, so a project doesn't die in the gaps between them."*
2. Four axis choices. Which value is this argument actually appealing to?
3. Reveal: the correct axis, plus how everyone else answered.
4. After five rounds: your score, and the axis you read best — *"You're sharpest on
   Authority, weakest on Time horizon."*

Non-partisan by construction: the answer space is the 15 compass axes, never two parties.
And it trains the exact skill the coalition loop depends on — reading an argument for what it
values rather than for whose side it's on. Of the six games this is the most direct on-ramp
to the flagship product.

## Content source — the best reuse in the set

`BillAxisPosition.Rationale` is already a one-sentence explanation of why a bill lands on a
given axis, and the row already carries the `AxisKey`. **The rationale is the argument and the
axis is the answer.** No authoring, no LLM, no new pipeline — the content is sitting in the
table that `BillSynthesisService` has been filling since the compass shipped.

Selection, in `WhoseValueGenerator`:

- `Bill.SynthesisStatus == Synthesized`, `BillAxisPosition.Confidence >= 0.7` — higher than
  Place It's bar, because here a wrong label is unambiguously a wrong answer rather than a
  debatable one.
- Five rationales per puzzle, each from a **different bill** and a **different axis**, so a
  player can't infer answers from a shared topic.
- Distractors: three other axes, drawn preferentially from axes the *same bill* also touches.
  Those are genuinely tempting rather than absurd, which is what makes the round interesting.
- Never reuse a `(BillId, AxisKey)` pair.

## The leak filter — required

Rationales are written *about* an axis, so many name it outright: "this **centralizes**
authority," "a **precautionary** requirement." Those make the round trivial.

Reject a rationale at generation when its text contains, case-insensitively:

1. The axis `Name` (e.g. "Authority"),
2. Either pole label (e.g. "Decentralized", "Centralized"), or
3. A stem of either — match on the first 6 characters, so "centralizes" and "centralization"
   are caught along with "centralized".

All three come from `ICivicCatalog.AxisFor(axisKey)`, so the filter stays correct as the
catalog grows. Reject rather than rewrite: there are thousands of candidate rows and no
reason to spend an LLM call salvaging one.

Expect the filter to reject a substantial share of candidates. That is fine — the generator
needs five per day. If eligible rows ever run short, the fix is a higher `Confidence` bar on
synthesis, not a weaker filter.

## Payload contract (`PayloadVersion = 1`)

```jsonc
{
  "rounds": [
    {
      "argument": "One standard instead of fifty means a project doesn't die in the gaps.",
      "billTitle": "…",              // shown only in the reveal
      "billId": "…",
      "choices": [
        { "axisKey": "authority",   "name": "Authority",   "lowLabel": "Decentralized", "highLabel": "Centralized" },
        { "axisKey": "expertise",   "name": "Expertise",   "…": "…" },
        { "axisKey": "govt-role",   "name": "Government role", "…": "…" },
        { "axisKey": "change-speed","name": "Change speed", "…": "…" }
      ],
      "correctAxisKey": "authority"  // SECRET — strip on GET
    }
  ]
}
```

Shuffle `choices` at generation, not at render, so the client can't infer the answer from
ordering.

`ResponseJson`: `{ "picks": ["authority", "risk", ...] }`.

## Scoring

`Score = round(100 * correct / 5)`. No partial credit — the choice is discrete.

The end-card names the player's strongest and weakest axis across their **last 10 puzzles**,
not just today's five, so the read is stable rather than noise. Query
`DailyPuzzlePlays` filtered to `Kind == WhoseValue`.

## Compass integration

Reading an axis well is not the same as scoring high on it, and the copy must not conflate
them. "You're sharpest on Authority" is a comprehension result; it says nothing about where
the player sits on that axis. Keep the framing strictly about reading skill, and link to
`/profile` for the actual compass rather than implying the game measured it.

## Share

```
Whose Value #19
4/5 — sharpest on Authority
🟩🟩🟥🟩🟩
civersify.com/daily
```

## Verification

- The leak filter rejects a rationale containing the axis name, either pole label, or a
  6-character stem of either; a unit test covers "centralizes" against the `authority` axis.
- Five rounds always come from five distinct bills and five distinct axes.
- Distractors prefer other axes of the same bill when available.
- `GET` responses contain no `correctAxisKey`.
- Choice order is fixed at generation and identical across two `GET`s of the same puzzle.
- The strongest/weakest readout uses the last 10 plays and degrades gracefully when the
  player has fewer.
- No `(BillId, AxisKey)` pair ships twice.
