# 04 — Place It

**Guess where a real bill sits on three compass axes.** The Wordle-shaped one.
~60 seconds. Read `00_OVERVIEW.md` first.

## The loop

1. A real bill: title, one-paragraph neutral summary, status.
2. Three axes are named. For each, place the bill on a 5-point scale between the axis's two
   pole labels.
3. Submit all three. Feedback per axis: ✅ exact, ⬆️ / ⬇️ if the true position is higher or
   lower. Up to **three rounds**.
4. Reveal: the true position per axis with its one-sentence rationale, then the payoff —
   *"Here's how this bill sits against **your** compass"* — deep-linking to `/bills/{id}`.

That last step is the point. Place It is the bridge from casual play into the compass
product; everything before it exists to earn the click.

## Content source — zero LLM, already computed

`BillAxisPosition` (`backend-civic/Models/Bill.cs`) already holds, per bill per axis:
`AxisKey`, `Score` (−1..+1), `Confidence` (0..1), `Rationale` (one sentence), and optional
`Evidence`. `BillSynthesisService` populates it in the background and
`BillAlignment.Classify` is a pure function with an exact client-side mirror in
`BillDetail.tsx`.

Selection, in `PlaceItGenerator`:

- Bill must have `SynthesisStatus == Synthesized`.
- Take the three axis positions with the **highest `Confidence`**, requiring `>= 0.6`. A bill
  with fewer than three such axes is skipped.
- Prefer bills with a recent `LatestActionDate` so the game tracks the news.
- Never reuse a `BillId`. Set `SourceBillId`.
- Bucket `Score` into the 5-point scale at −0.6 / −0.2 / +0.2 / +0.6 so guesses and truth
  live on the same grid.

## The honesty problem — and why it improves the game

The "right answer" here is an LLM's synthesis of the bill, not ground truth. Telling a player
they are *wrong* about a value judgment, on the authority of a model, would be both
overreaching and off-brand for an app whose entire thesis is that disagreement is handled
well rather than scored.

So the reveal is framed as a comparison, not a verdict:

> **Our synthesis put this at "Centralized" (+0.6).** *"The bill moves permitting authority
> from state agencies to a federal office."* — Disagree? Read the bill text and tell us.

Concretely:
- Copy never says "wrong" or "incorrect." It says "our synthesis put this at…".
- Every revealed axis shows its `Rationale`, and its `Evidence` when present.
- Every reveal links to `Bill.FullTextUrl` / `SourceUrl`.
- A "this synthesis looks off" control writes a flag for `/admin/daily` review. Repeated
  flags on one bill are a signal for `BillSynthesisService` quality, which makes the game a
  free QA channel for the synthesis pipeline.

Scoring stays generous for exactly this reason — see below.

## Payload contract (`PayloadVersion = 1`)

```jsonc
{
  "billId": "…", "billTitle": "…", "billSummary": "…", "billStatus": "InCommittee",
  "axes": [
    {
      "axisKey": "authority",
      "name": "Authority",
      "lowLabel": "Decentralized",
      "highLabel": "Centralized",
      "trueBucket": 4,            // SECRET — strip on GET
      "rationale": "…",           // SECRET — strip on GET
      "evidence": "…"             // SECRET — strip on GET
    }
  ],
  "maxRounds": 3
}
```

Axis names and pole labels come from `ICivicCatalog.AxisFor(axisKey)` at generation, never
hard-coded — the catalog is 15 axes and will keep growing.

`ResponseJson`: `{ "rounds": [[2,4,3],[3,4,4]] }` — bucket guesses per round.

**Known leak, accepted.** `GET /api/bills/{id}` is public and returns axis positions, so a
determined player can look up the answer. Not worth defending: there is no prize, and a
player curious enough to open the bill page has already done the thing the game exists to
cause. Do not add obfuscation for this.

## Scoring

Per axis, on the final round, distance in buckets:

```
axis_i = [100, 70, 40, 15, 0][min(4, |guess - true|)]
Score  = mean(axis_1..3) * (1 - 0.15 * (roundsUsed - 1))
```

Adjacent buckets score 70 — deliberately generous, because the truth is a synthesis and
"one notch off" is a legitimate reading rather than an error.

## Share

The Wordle grid, one row per round, one square per axis:

```
Place It #31
🟨🟩⬜
🟩🟩🟨
🟩🟩🟩
civersify.com/daily
```

🟩 exact, 🟨 one bucket off, ⬜ further. Axis names are not in the grid — that would leak
which axes are in play to anyone who hasn't played yet.

## Verification

- A bill with fewer than three axes at `Confidence >= 0.6` is skipped by the generator.
- `GET` responses contain no `trueBucket`, `rationale`, or `evidence`.
- Bucketing round-trips: a `Score` of +0.6 maps to bucket 4 and back.
- Axis labels match `ICivicCatalog` output for the same key; adding a 16th axis needs no
  code change here.
- The reveal renders rationale and evidence, links to the bill text, and never uses the word
  "wrong."
- The "synthesis looks off" flag persists and surfaces in admin.
- Playing through to a perfect third round produces a three-row grid.
