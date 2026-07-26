# 03 — Priced In

**Guess the size of a real civic figure. Three guesses, higher/lower.**
~45 seconds. Read `00_OVERVIEW.md` first.

## The loop

1. One item: *"How much did the federal government spend on food assistance (SNAP) in 2024?"*
2. Guess on a **log-scale** slider — the range spans millions to trillions.
3. Higher / lower feedback. Three guesses total.
4. Reveal: the true figure, the source, and an anchor that makes the magnitude legible —
   *"about 1.4% of federal spending"* or *"about $340 per person per year."*

Scale is the single most misunderstood thing in budget politics, and it is misunderstood in
both directions by everyone. Getting it wrong is not embarrassing, which is what makes this
format comfortable for a first-time visitor. The discovery lands as a discovery rather than
as a correction.

## Content source — the one game that needs authoring

There is no federal outlay table in the codebase. `TaxConstants.cs` holds brackets, rates,
and the FICA wage base; `TaxEngine` computes an individual's liability and the federal /
state apportionment split. Neither has program-level spending. So this game needs a new
seeded bank, and that is its main cost.

**Static bank — `backend-civic/Seed/magnitudes.json`, ~150 items.**

```jsonc
{
  "key": "snap-outlays-2024",
  "prompt": "Federal spending on SNAP (food assistance), FY2024",
  "trueValue": 112400000000,
  "unit": "usd",
  "minBound": 1000000000,
  "maxBound": 2000000000000,
  "anchor": "About 1.6% of federal outlays — roughly $335 per American per year.",
  "source": "USDA Food and Nutrition Service, FY2024 outlays",
  "sourceUrl": "https://...",
  "asOf": "2024-09-30",
  "direction": "smaller"          // audit tag: is the truth smaller or bigger than typical guesses?
}
```

**Derived items — self-refreshing, no authoring.** `TaxEngine` can generate items live, and
these should be roughly a third of the bank so it does not go stale between annual refreshes:

- "What does a single filer earning $60,000 in Ohio pay in federal income tax?"
- "What share of a $90,000 earner's total tax bill is state and local, in Texas vs. California?"

These carry `GenerationSource = "derived"` and recompute against the current
`TaxConstants.TaxYear`, so the annual bump that the tax model already documents as "a
contained edit" refreshes them for free.

**Refresh discipline.** Every static item carries `asOf`. The admin page flags any item whose
`asOf` is more than 24 months old. A stale figure presented as current is the failure mode
that costs the most credibility.

## Payload contract (`PayloadVersion = 1`)

```jsonc
{
  "prompt": "Federal spending on SNAP (food assistance), FY2024",
  "unit": "usd",
  "minBound": 1000000000,
  "maxBound": 2000000000000,
  "maxGuesses": 3,
  "trueValue": 112400000000,   // SECRET — strip on GET
  "anchor": "...",             // SECRET — strip on GET
  "source": "...",             // revealed with the answer
  "sourceUrl": "..."
}
```

Higher/lower feedback is computed **server-side**, one `POST` per guess with
`{ "guess": 90000000000, "final": false }`, so the true value never reaches the client before
the reveal. The final guess sets `final: true` and completes the play.

`ResponseJson`: `{ "guesses": [90000000000, 130000000000, 115000000000] }`.

## Scoring

Ratio error, not absolute — being off by $10B means something very different on a $12B item
than on a $900B one:

```
ratioErr = |log10(finalGuess / trueValue)|
raw      = max(0, 100 - 40 * ratioErr)     // 60 at 1.5×, 20 at 6×, 0 at 100×
Score    = round(raw * (1 - 0.1 * (guessesUsed - 1)))   // 10% haircut per extra guess
```

Getting within 1.25× on the first guess scores ~96; within 2× on the third scores ~58.
Guard `finalGuess <= 0` before the log.

## Neutrality — this bank is an editorial position

A magnitude bank stacked with items whose answer is "much smaller than you think" argues a
thesis, whichever thesis it happens to be. Foreign aid, welfare fraud, congressional
salaries, NASA, and public broadcasting are all classic "smaller than you think" items; a
bank built only from those reads as advocacy no matter how accurate each entry is.

Hence the `direction` tag on every item, and a hard requirement: **the shipped bank must be
within 55/45 on `smaller` vs `bigger`**, and selection must not drift from that. The admin
balance report renders the trailing-30-edition split, consistent with the monthly bias audit
the gamification docs already commit to.

## Share

```
Priced In #12
🎯 Got it in 2 — within 1.3×
civersify.com/daily
```

Guess count and closeness band only; the figure itself is never in the grid.

## Verification

- Log-slider input maps correctly at both bounds and the midpoint.
- The `GET` payload contains no `trueValue` or `anchor`.
- Higher/lower is computed server-side; the client cannot derive the answer from three
  intermediate responses beyond the bracketing the game intends.
- A derived item recomputes when `TaxConstants.TaxYear` changes.
- The bank passes the 55/45 `direction` balance check; the check fails loudly in a unit test
  if a future edit skews it.
- Items older than 24 months are flagged on `/admin/daily`.
- A guess of 0 or a negative number is rejected without throwing on the log.
