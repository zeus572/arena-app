# 07 — Which Is True

**Status:** implemented
**Date:** 2026-07-29
**Read `00_OVERVIEW.md` first** — the table, controller, XP hook, cadence ring and share
format are shared and are not repeated here.

## One-liner

A question and two figures. One of them answers it. Five rounds, one tap each.

> **What is the average combined state and local sales tax rate in Tennessee?**
> `5.50%` **·** `9.55%`

## Why this game

Priced In (03) already asks "how big is that number?" and it works, but a free-entry
ladder is a lot of interaction for a feed and it rules out anything that isn't a dollar
amount. Which Is True is the two-tap version: it takes the same content and reduces the
interaction to a single choice, which makes it the first daily game that fits *inside* a
Shorts card comfortably and the first that can ask about non-numeric facts (a sponsor, a
chamber, a year).

It also does something Priced In can't: it teaches **two** facts per round. See below.

## The invariant: both numbers are real

**The decoy is never a fabricated figure.** It is always another true value from the same
family — a different state's rate, a different bracket's threshold, a different bill's
sponsor. The generator carries a `DecoyTruth` string saying what the losing option
actually is, and the reveal always shows it:

> **9.55%** — Tennessee averages 9.55% once local add-ons are included.
> *The other one? 5.50% is Maine's.*

Three reasons this is worth more work than `trueValue * 2.4`:

1. **Credibility.** A made-up number is a made-up number even when it's the wrong
   answer. Priced In's spec already refuses to ship an unverified figure; showing one as
   a decoy is the same failure with a thinner excuse.
2. **It stays hard.** Invented decoys are guessable within a week — players learn that
   the rounder or the more extreme number is the fake, and the game stops measuring
   anything. When both figures come from the same real distribution there is no tell.
3. **It teaches twice.** "That's Tennessee's, and the other is Maine's" is a better beat
   than "the other one was nothing," and it's the reason a wrong guess is still worth
   something.

Enforced by `WhichIsTrueTests.EveryCandidate_PairsTheTruthWithADifferentRealFigure`.

## Content sources

All three are already in the repo. Zero LLM, at generation or at play.

| Topic | Source | Rounds available |
|---|---|---|
| **State & local tax** | `StateProfiles` — 50 states, verified Tax Foundation 2025 sales + property rates | ~90 |
| **Federal budget** | `Seed/magnitudes.json` (verified rows) + `TaxConstants` bracket table | ~15 |
| **Congress** | Ingested `Bill` rows — sponsor, chamber of origin, year introduced | 3 × synthesized bills |

The state pool is deep enough that the game never runs dry, and it re-prices itself for
free on the annual tax-constants bump the tax model already documents.

**Pairing rules**, so a round is a question about the world rather than about rounding:

- Rates must differ by **1.5–4.0 points** (sales) or **0.6–1.4 points** (property).
- Dollar magnitudes must differ by ≥ 1.8×.
- A state's rival is picked deterministically from *every* state inside that band, not the
  one furthest away.

The ceiling on the band is the less obvious half of that rule, and it came out of looking
at real generated output. Pairing each state against the national extreme produced
"Nevada: 0.00% or 8.24%" — technically two real rates, but *never pick the 0.00%* is a
winning strategy that has nothing to do with knowing anything, and the same two outliers
turned up as the decoy all week. Banding the gap and picking inside it fixes both.

**One round per family.** The per-topic cap (2) isn't enough on its own: two bracket
questions are both "Federal budget" and read as the same question asked twice. Rounds are
also capped at one per key prefix — `state-sales`, `state-property`, `magnitude`,
`bracket`, `bill-sponsor`, `bill-chamber`, `bill-year`.

## Payload

```csharp
public record WhichIsTrueRound(
    string Key,          // SECRET — dedup bookkeeping ("state-sales:TN")
    string Topic,        // "Federal budget" | "State & local tax" | "Congress"
    string Prompt,
    string OptionA,
    string OptionB,
    string Correct,      // SECRET — "A" | "B"
    string Explanation,  // SECRET
    string DecoyTruth,   // SECRET — what the loser actually is
    string Source,       // SECRET
    string? SourceUrl,   // SECRET
    string? AsOf,        // SECRET
    Guid? BillId);       // SECRET

public record WhichIsTruePayload(List<WhichIsTrueRound> Rounds);
public record WhichIsTrueResponse(List<string> Picks);   // "A" | "B" per round
```

### Redaction is stricter here than in any other game

Every other daily game shows its provenance up front — Crowd Call's attribution, Priced
In's source URL — and hides only the answer. **Which Is True hides the provenance too**,
because with two options on the card a citation *is* an answer key:
`ssa.gov/oact/cola/cbb.html` or `H.R. 1234 · 118th Congress` hands over the very figure
the question is asking about.

The player sees the topic, the prompt and the two options. Everything else arrives in the
reveal, where it belongs. Asserted in
`DailyContentTests.Redaction_WhichIsTrue_StripsTheAnswerAndAllProvenance`.

## Scoring

Straight accuracy: `100 × correct / rounds`, one `hit`/`miss` band per round.

Deliberately **not** curved for the 50% floor a two-option question hands you. The
end-card reports the raw count, and "3 of 5, on coin flips" is exactly the humbling read
the game exists for. Chance-correcting the score would hide it.

## Side balance

If the answer sits on one side more often than the other, a player learns the pattern
rather than the facts. Which side is correct is derived from a **stable FNV-1a hash** of
`{dayNumber}:{roundKey}` — not `Random`, not `HashCode.Combine` (both would move the
answer when a day is regenerated after a process restart), and not a shared RNG sequence
(which would correlate side with round position).

Two tests hold the line: the A-share across the whole bank on a given day must sit inside
40–60%, and a given question must land on both sides across a 40-day window.

## Share grid

```
Which Is True #12 — 4/5
🟩🟩🟥🟩🟩
civersify.com/daily
```

No topic list. "3 budget, 2 Congress" would tell someone who hasn't played which half of
the card to think hard about.

## Frontend

- `components/daily/games/WhichIsTrueGame.tsx` — one round at a time, two big buttons,
  then a per-round reveal carrying the explanation, `DecoyTruth`, the citation, and a
  deep link into `/bills/{id}` for Congress rounds.
- Teaser in `components/shorts/DailyShortCard.tsx` leads with the real prompt.

## Review

Auto-approves (`RequiresReview => false`). Unlike Fork and Time Machine there is nothing
here that can read as an editorial position — every round is pure selection over verified
rows, and both options are figures the app already publishes elsewhere.

## Verification

```bash
cd backend-civic && dotnet build
dotnet test backend-civic-tests/Civic.UnitTests/Civic.UnitTests.csproj
dotnet test backend-civic-tests/Civic.ApiTests/Civic.ApiTests.csproj   # needs arena-postgres
cd frontend-civic && npm run build && npx vitest run
```

Acceptance:

- `GET /api/daily/which-is-true` contains no `correct`, `explanation`, `decoyTruth`,
  `source`, `sourceUrl`, `asOf`, `billId` or `key`.
- A play returns a per-round reveal whose `decoyTruth` names what the other option is.
- A pick that is neither `"A"` nor `"B"` is a `400`, not a silent zero.
- The share grid carries neither figure.
- With no bills ingested, the generator still produces a full five rounds from the tax
  pools alone.
