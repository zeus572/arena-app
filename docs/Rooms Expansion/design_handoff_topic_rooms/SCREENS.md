# Screens

Keyed by the id badge in `designs/Topic Rooms.dc.html`. Read `README.md` first — the
cross-cutting systems (density dial, evidence marks, meaningful change, money ladder,
conversation guardrails) are specified there and are assumed here.

Desktop reference width **1280px**. Mobile **390px**. Card surface is `--bg-elev` with a
`1px solid var(--border)` frame throughout.

---

## A · Theme Room front door

### `1a` Dispatch — default entry

**Purpose.** Orient someone in 60 seconds without a wall of information, and make the
next thing to watch unmissable.

**Layout, top to bottom** (1280px, content inset 64px):

1. **App bar**, 56px, bottom hairline. Left: wordmark (serif, 19px) + nav
   (Rooms / Bills / Sprint / Predictions, 13px/600; active item has a 2px `--accent`
   underbar). Right: search field, 28px avatar square.
2. **Room header**, 44px top padding. Two columns: title block (flex 1) and a 212px meta
   rail with a left hairline. Title block: uppercase `--accent` eyebrow
   ("Theme Room · Active · Reviewed daily"), `h1` 52px serif, 19px `--fg-soft` dek
   (max 640px), then an aliases line at 13px `--muted` with a dotted-underline
   "why the name matters" affordance. Rail: last-meaningful-update, revision id, and a
   full-width primary `Button` ("Follow this room").
3. **Status sentence.** Top rule `2px solid var(--fg)`, bottom hairline. Uppercase
   `--muted` label "Where this stands", then **31px serif** prose, max 960px. This is the
   room's most important element — it is edited in place, never appended to.
4. **Change ribbon**, 14px vertical, hairline below. 7px `--accent` square · count in
   14px/600 · change *types* in 13px `--muted` (never article counts) · right-aligned
   "Show me" with a 1px `--fg` underline. Opens `1d` in place.
5. **Three essential facts.** 3-column grid, hairline dividers between columns, hairline
   below. Each: serif `--accent` numeral (26px), 17px statement, 12px `--muted`
   status + source line. Facts are claim references, so their status line renders from data.
6. **Open question / Watch next.** 1.35fr / 1fr grid. Left: uppercase label, 25px serif
   question, 15px explanation, uppercase "Open the claim" link. Right: **inverse panel**
   (`--fg` bg) bleeding to the right edge — "Watch next", 25px serif, 15px detail at
   `oklch(100% 0 0 / .8)`, and "Predict the outcome →".
7. **Ambient path.** 8-column grid of 3px progress bars with a 12px/600 section name and
   an 11px `--muted` count. Right-aligned reassurance copy: "Nothing is required."
8. **Latest preview**, on `--bg-inset`, top hairline. 150px label column + rows. Each
   row: 74px date column, headline (16px/600) + "Why it matters" line (14px `--fg-soft`),
   126px right column with a "New" flag in `--accent` and category · status. Ends with
   an uppercase "All 8 developments" link.

**Copy discipline.** "Why it matters" is a required field on every development. The
status sentence describes a *state*, not an event.

### `1b` Situation Board — high-density alternative

**Purpose.** Same room, same objects, no prose. For the user who wants one surface.

- **Header** is inverse (`--fg`), 50px: room name (17px serif) + "Board view" label;
  right side carries the density dial with Board active (`--accent` fill, white text).
- **Left rail**, 186px, right hairline: eleven sections with right-aligned counts. Active
  item has `--federal-soft` background and a 3px `--federal` left border. Below a
  hairline: a filter chip cluster (active chip = 1px `--fg`; inactive = `--border`,
  `--fg-soft` text).
- **Body** is a 3-column grid of bordered tiles; tiles span 1–3 columns. In order:
  status + 4-metric strip (span 2) · "Since your last visit" on `--bg-inset` (span 1) ·
  claim-ledger distribution bars (span 2) · money summary with a 5-bar stage sparkline
  (span 1) · predictions with crowd bar + `--accent` user tick (span 1) · "Who can act
  next" (span 1) · sources by type (span 1) · Story Rooms 3×2 grid (span 3).
- Every tile header is a 10px uppercase `--muted` label. Numbers are serif; labels sans.
- Bar-chart convention: `--fg` for the primary series, `--border` for the remainder,
  and the disputed split-fill for the disputed row — the same vocabulary as `1m`.

### `1c` Density dial spec

Not a screen — the mechanic, shown on one module so the rule is legible.

- Header states the control's placement (top-right, sticky, remembered per user) and
  renders the three-segment control: 1px `--fg` frame, 9×18px segments, active segment
  filled `--fg` with `--bg` text.
- Three equal columns show the **same claim** ("damage to enrichment sites") rendered as
  Read (20px serif prose), Brief (labelled rows with status marks), and Board (a 4-column
  table with source counts and revision ids).
- Footer is an inverse panel stating the invariant and the default/persistence rules.
  Reproduce those rules in code as described in the README.

---

## B · What changed

### `1d` Delta ledger
620px card. Header: `--accent` eyebrow with the revision range ("r.141 → r.148"), then
26px serif in two lines — *"3 meaningful changes. / 11 edits we did not bother you with."*
Dismiss glyph top-right.

Rows are typed, with a 66px uppercase type column (`Added` in `--federal`, `Status` in
`--state`, `Resolved` in `--accent`): headline 16px/600, 14px `--fg-soft` explanation of
why it matters, then metadata. The status row renders the transition literally —
old mark, word, arrow, new mark, word. The resolved row shows the user's number against
the crowd's and a plain-language calibration note.

Footer on `--bg-inset` lists the withheld edits by count and links the full changelog.

### `1e` Diff mode
Full-width. `--accent` bar across the top: "Diff mode", the comparison being shown, and
"Exit". Below, a **revision scrubber** row: 96px label, a track with tall `--accent`
marks for meaningful changes, short `--muted` marks for edits, a hollow 11×18px marker at
the user's last visit, a solid `--fg` marker at current, and a 210px legend.

Body is the room itself, marked up: changed sentences highlighted, retired sentences
struck through in `--muted`, unchanged rows labelled `Same` in a 60px uppercase column,
changed rows tinted `oklch(96% .03 60)` and labelled `Changed` in `--accent` with a
"previously:" note. Right sidebar (340px, `--bg-inset`) is a change index: one row per
diff with a type swatch and relative time. Corrections carry their own swatch.

### `1f` Re-entry card
560px, **entirely inverse** (`--fg` bg). Names the gap ("last here 6 days ago"), then a
34px serif line that reports the payoff — *"The thing you were waiting on happened."* —
and ties it to why the user followed. Three numbered items with `--state` numerals on
hairlines at `oklch(100% 0 0 / .15)`. Actions: `--accent` primary ("Enter the room") and
an outlined secondary that jumps straight to the single change ("Just the vote"), plus a
right-aligned note that only meaningful changes trigger this. It is a **lid over the
room**, not a replacement — dismissing lands the user in the unchanged front door.

---

## C · Theme Room sections

### `1g` Latest
Header: eyebrow with section position ("Section 2 of 10"), 40px serif title, a dek that
states the bound honestly (*"Eight developments in 34 days. We logged 260 articles and
judged eight of them to have changed something."*), and a right-aligned category filter
chip set with counts.

Rows: 150px date column (right hairline; the newest row is tinted `oklch(97.5% .015 60)`
and carries a "New" flag) + content. Content per row: category eyebrow, serif headline
(24px for the lead, 21px for the rest), a **"Why it matters"** paragraph with the label
bolded inline, then a metadata line with the status mark + word, source summary, and a
Story Room link. Resolved predictions appear as rows at 72% opacity.

Right sidebar (290px, `--bg-inset`): **"What we left out"** — the count of excluded
articles and the seven-part inclusion rule as a plain list, closing with
*"New commentary about an old event is not a development."*

### `1h` Understand
Two blocks.

**Timeline.** Nine turning points on a horizontal 2px `--border` track, 118px columns.
Markers: hollow 16px squares (2px `--fg` border) for agreed events, solid `--federal` for
the two contested decisions, `--state` for the triggering event, `--accent` for "Now".
Year in 13px/700, description in 13px `--fg-soft`. A caption explains the marker
vocabulary and states that tapping a point shows **what was known at the time**, not what
is known now. A text alternative is required.

**Glossary + confusions.** 1.25fr / 1fr split on a 2px `--fg` top rule. Left: "Words you
will hit today" as a 2-column bordered grid, term (15px/600) + one-line gloss; already-read
terms are `--muted` with a check and the date opened. Right: "Easy to confuse" pairs
rendered as *A* vs *B* with a one-sentence discriminator, then a bordered geography card.

### `1i` People & Power
Header carries a 280px bordered control naming the decision the sort is relative to
("Sorting by leverage over: The Senate war powers vote ▾"). Changing it re-sorts.

Three **tiers**, each a row: a 132px left label cell (Decides is inverse `--fg`; Shapes
and Constrained are `--bg-inset`) with the tier name and its definition, plus a 3-column
grid of actor cards. Each card: a 9px type square + uppercase type label
(`--federal` government, `--state` foreign/international, `--muted` other), name
(15–16px/600), a one-line statement of leverage, and an appearance count.

Below the tiers, a plain line accounting for the remaining 19 actors and a "See all 31" link.

Right panel (346px, `--bg-inset`, 1px `--fg` left border) is the **actor card**, answering
five questions in fixed order on hairlines: Role here · Actual power · **Says it wants**
(always a quote or filing, with date — never inferred motive) · Constrained by ·
Appears in. Footer states that rule explicitly.

### `1j` Government & Law
620px. Rows sorted by **what can happen next**, not filing date. Each: type eyebrow +
identifier, 17px/600 title, then a **5-segment stage bar** (Introduced / Committee /
Floor / House / Desk) — completed segments `--fg` with `--bg` text, the current segment
`--accent` with white text, future segments `--muted` on `--bg-elev` — and a plain-language
consequence line with a Story Room link. Executive actions and appropriations use the same
row shape without the stage bar. **Empty categories are stated, not omitted**: the Courts
row reads "No live U.S. case" with the reason.

### `1l` Sources & Methodology
620px. Source-type distribution bars (primary documents first, public reaction last).
Then the **social sampling disclosure** in a `--state` / `--state-soft` panel: window,
named sources, raw and deduplicated counts, and the explicit statement that this is not a
measure of public opinion. Then the **correction log**, given equal billing: each entry
labelled `Corrected` or `Retracted` in `--accent` with the date **and the lag from the
source correction**, then what was wrong, what it is now, and how many pages propagated.

---

## D · Uncertainty

### `1m` Status system
620px. Eight rows, each a 16px mark + name + one-line definition — the table in the
README is the spec. Footer on `--bg-inset` demonstrates **inline use** in a 17px prose
paragraph at `line-height: 1.75` with 11px marks after the qualified clause, plus the
interaction and screen-reader rules.

### `1n` Claims & Evidence ledger
Full width. Header dek states the sort rationale: least settled first, *"because that is
where you are most likely to be misled."* Sort chips: Least settled · By date · By actor ·
Only unsettled.

Table header on `--bg-inset` over a 2px `--fg` rule: Status (180px) · Claim (flex) ·
Who says it (150px) · Evidence (120px) · Reviewed (96px, right). Rows: mark + word, claim
in 16px/600 with a 14px `--fg-soft` explanation of what the evidence actually does, then
the columns.

**Expanded row** (the disputed claim is shown open): a bordered panel on the tinted row
background, split into *Evidence that supports it* (`--federal` heading, items with a 2px
`--federal` left rule) and *Evidence against it* (`--state`), each item being a claim in
14px plus a 12px provenance line. A full-width footer band holds three cells:
**What would settle it** · **History of this label** (with dates) · **Appears in**.
"What would settle it" is a required field.

---

## E · Story Rooms

Both variants implement the same nine-part bundle: header/status → what happened →
why it matters → how we got here → who is affected → an interaction → what happens next →
sources → changelog. The difference is rhythm.

### `1o` Single column, gated — 900px
1. **Header.** Category eyebrow, room breadcrumb, right-aligned read-time chip. 44px
   serif headline, 19px dek. A hairline-separated meta strip: the disputed-claim warning
   with its mark, event/review dates + revision, and a **content note** in `--state`.
2. **Before You Know**, immediately after the header, on an inverse panel. 32px serif
   question; four options as 1px `oklch(100% 0 0 / .35)` outlined rows, the selected one
   filled `--accent`. Below: the no-penalty note and what share of readers pick each.
3. **What happened.** Rows on a 2px `--fg` rule, each with a 14px status mark and an 18px
   statement. The disputed row carries a sub-line pointing at the evidence trail.
4. **Why it matters.** 2×3 bordered grid across six named dimensions (Legal,
   Institutional, Financial, Human, Immediate, Longer term) — each an 11px uppercase label
   plus 16px prose. Filling all six is a content requirement; if a dimension is genuinely
   empty, say so rather than padding.
5. **Explore.** Bordered block on `--bg-inset`: the Fact/Interpretation/Prediction sort,
   with verbatim sentences as rows and `ValueChip`s as the label palette. A correctly
   sorted row shows its label filled `--fg`.
6. **What happens next.** Rows of: outcome (17px/600) + **"Confirmed if:"** criterion,
   a 130px actor + timeframe column, and a 140px right column with a "Predict this"
   outlined `--accent` chip.
7. **Footer** on `--bg-inset`: sources by type with what each was used for, and the
   changelog.

### `1p` Bill room, sidecar rhythm — 1180px
No gate; enter at any heading. Main column (flex 1, right hairline) runs: **What it
actually does** (four rows, two of which are explicit `Does not` statements) → **Vote
Before Reading** in a 1px `--fg` block on `--bg-inset` with three `Button` variants and
the privacy note → **the strongest version of each argument** as a 2-column bordered grid
(For in `--federal`, Against in `--state`, each a *composite of sourced statements*, with
a full-width **"What both sides accept"** band beneath) → **who is affected** as rows with
an explicit confidence column, including a low-confidence row that notes members of any
group hold different views.

Sidecar (326px, `--bg-inset`) never scrolls away: **State of play** (a plain sentence plus
a hairline key/value list — sponsor, cosponsors, threshold, publicly undecided, and
"Official cost estimate: **None published**" in `--state`), **Crowd prediction** with a bar
and the user's participation state, **Also in this room**, and **Read the bill** noting that
each summary claim links to the section it came from.

---

## F · Conversation

### `1q` Argument clusters
Order is load-bearing: title → **sampling disclosure** → clusters → questions.

Disclosure band spans full width on `--state-soft` with a `--state` bottom border: a
plain-language statement on the left, a 400px hairline key/value table on the right
(collected window, sources, items raw → kept, prevalence basis), and a link to the full
method, queries, and known biases.

Clusters are a 2-column grid, decreasing in card weight with prevalence: the top cluster
gets a 1px `--fg` frame and a 25px serif label, mid clusters `--border` frames, the two
smallest a compact 17px treatment. Each card: label + percentage, a 5px prevalence bar
(`--fg` on `--border`), a 16px description, an **underlying value** line, and — for the
top clusters — a hairline-separated **"Competing concern in the same sample"** in `--state`
with its own percentage. Cards also carry attached-claim counts using `1m` marks, and
where a cluster contains a recurring factual error it names the interaction it routes to.

Footer: **questions people are actually asking** as rows with a 170px status column
(`Answered → knowledge item` in `--accent`, or `No reliable public answer` in `--muted`),
beside a bordered `--bg-inset` panel listing **what this section will not do** (six items —
reproduce it verbatim; it is the guardrail made visible).

### `1r` Three layers
620px. Three bands, each a 120px colored label cell (`--federal` / `oklch(50% .01 50)` /
`--state`, white text) with the layer number, name, and its epistemic caveat, plus a
content cell of hairline-separated items with provenance. Layer 2 shows credentials **and
conflicts**. Layer 3 states the excerpting, deletion-recheck, and no-username rules.

Footer on `--bg-inset`: the **viral vs prevalent** interaction. Two bordered cards, each
with the same two labelled bars — **Reach** (`--state`) and **Share of sample** (`--fg`) —
which invert between the two examples. The high-reach one carries the `Unsupported` mark
and the line that reach and prevalence are different measurements.

---

## G · Money

### `1s` Pipeline ladder
Left: a 330px 1px-`--fg` **headline item** card — 52px serif amount, stage word, a
hairline key/value list (period, requested by, decides next, dollar basis), and an
**inverse "What this does not mean" panel** listing four things the number is not.

Right: the five-stage bar chart, 180px tall, all five stages always drawn — the funded
stage solid `--fg` with a serif amount above, empty stages a 10px dashed `--muted` bar
with a hatch fill and "$0". A labels row on a 2px `--fg` rule gives each stage a name, a
plain-language definition, and a `--accent` "You are here" flag. Caption states the
consequence: four of five stages are empty, so coverage saying the government "is
spending" this describes the first box as if it were the last.

Below, a 1.1fr/1fr split: **what is in the request** as labelled bars (`--federal` for
defense lines, `--state` for humanitarian, `--muted` for admin) plus a **known exclusion**
note; and **comparisons that hold up**, where the third comparison is shown
**struck through** with the reason it was rejected in `--state`.

Footer band on `--bg-inset` with a `--state` label separates **modelled economic effects**
from outlays, with a 400px table of ranges — and states they are never summed with the
appropriation.

### `1t` Descent ladder
600px, vertical. Header: 29px serif in two lines — *"$4.1B has been asked for. / $0 has
been spent."* Five rows, each with a 16px marker column (solid `--fg` square for the
reached stage, hollow `--muted` for the rest, connected by a 1px vertical rule), the stage
name, a right-aligned amount, and a plain-language note. Stages that may be skipped say so.

Footer on `--bg-inset`: **Guess the Stage**. The headline in a 1px `--fg` box at 20px
serif, five `ValueChip` options, and a **"Why this is tempting"** explanation.

---

## H · Interactions

### `1u` Timeline Builder — 620px
Placed slots are 1px `--fg` rows with a serif ordinal and a `--accent` "Placed ✓" flag;
the next slot is a dashed `--muted` drop zone on `--bg-inset`. Below a hairline,
**"Still to place"** holds draggable `--border` rows with a grip glyph. Footer is an
inverse panel describing the payoff pass explicitly.

### `1v` Calibrated prediction — 620px, two cards
**Card 1.** Question in 26px serif with the resolution source and cancellation condition
stated up front. The answer: a 56px `--accent` percentage, then a track — 4px `--border`
rail, `--accent` fill to the user's value, a 6×26px `--accent` handle, and a 2px `--fg`
tick at the crowd mean — with Certainly not / Coin flip / Certainly yes labels. Below a
hairline: a sentence comparing the user to the crowd, a small primary `Button`, and the
proper-scoring-rule explanation ("Confidence is not free").

**Card 2 (resolution).** 1px `--fg` frame on `--bg-inset`. Question with the outcome in
the heading, then a 3-cell bordered strip — You said / Crowd / Outcome, serif numerals,
the user's in `--accent`. Then a plain-language calibration read across all questions in
the room, and a **calibration-by-confidence-band** bar chart with overconfident bands in
`--state` and a caption stating it is private and never ranked.

### `1w` Budget Allocator — 620px
Header states the pot and the two hard constraints in prose. A "Remaining" row on a 2px
`--fg` rule with a 24px `--accent` serif figure. Each category: name + amount, a track
with a colored fill and handle, and — where a constraint applies — a 1px `--state`
vertical line on the track with a note naming the floor and what the real request
allocated. Footer on `--bg-inset` describes the unscored submit behavior and the
scope caveat.

### `1x` Civic Sprint completion — 620px
Segmented 5px progress bar (all filled) + elapsed time. Then: `--accent` eyebrow,
33px serif *"Three things you learned"*, three numbered rows on a 2px `--fg` rule; a
`--state` / `--state-soft` panel with **one thing still unresolved**; a bordered panel
restating the user's prediction and that its resolution is the only notification; then
`Button`s and the forgiving streak line.

---

## I · Editorial

### `1y` Story Bundle review
**Top bar** (54px, `--bg-inset`): "Story bundle" label, slug, an `In review` chip in
`--state`, provenance line (AI-drafted timestamp, editor name), gate progress, and a
**disabled** Publish button.

**Left rail** (172px): the ten bundle parts with counts; `--accent` counts mark parts
needing attention, and the Sensitivity row shows a `--state` "!".

**Center.** Field groups where each field shows its provenance. A verified field has a
plain `--border` frame and a `--federal` "Verified by … · date" flag. A model-proposed
field carries a **3px `--accent` left rule** and its proposed status labelled as such.
The blocked field shows an inline `--accent` panel on `oklch(97% .03 40)`:
*"Blocked · contradiction detected"*, the contradicting source, the rule
(*cannot be published above Disputed*), and three resolution actions —
**Set to disputed** (filled `--accent`), Rewrite as attribution, Override with note.
Below, a **headline check** panel with three pass criteria and the rejected drafts kept
on the record with their reasons.

**Right rail** (328px, `--bg-inset`, 1px `--fg` left border): the nine **publish gates**.
Cleared gates are solid `--fg` squares; outstanding gates are 2px `--accent` outlines in
600 weight; the trust-and-safety gate is `--state` outlined on `--state-soft` with its
trigger reason. Footer states gates are blocking and names are stored with the revision.

### `1z` Correction propagation — 600px
Header on `--state-soft`: the claim id, the transition in 25px serif, the trigger, and the
fan-out count. **What changes automatically** — three rows with solid `--fg` marks.
**What a human has to look at** on a 1px `--accent` rule — four rows with 2px `--accent`
outlined marks, each naming the object, why it is now wrong, and a right-aligned action
(Rewrite / Revalidate / Review / Logged). The share-card row notes that cards render live
but records how many people saw the old wording. Footer is an inverse **service level**
panel: 6-hour hide, and the metric definition.

---

## J · Mobile (390px)

### `1aa` Front door — first visit and returning visit
**First visit** compresses `1a`: 34px status bar with the density dial as "Read ▾";
32px serif title; status sentence at 22px serif on a 2px `--fg` rule; three essential
facts as numbered rows with `--accent` serif ordinals (status marks inline); the
**Watch next** inverse panel; a horizontally scrolling **section rail** of 88px progress
columns; and a two-button action row (`--accent` Follow + outlined Board view).

**Returning visit** leads with the `1f` re-entry card rendered full-bleed inverse at the
top of the room, followed by a note that the normal front door continues below unchanged,
and a bordered **"Not shown"** panel giving the withheld-edit counts and the real
notification frequency ("roughly twice a week on this room, not twice an hour"). Bottom
tab bar: Rooms / Bills / Sprint / You.

### `1bb` Story Room and Sprint
**Story Room.** Slim bar (back / read time / type-size control), 29px serif headline, a
hairline meta block with the disputed warning and dates, then **Before You Know**
full-bleed inverse with four 44px+ options and the no-penalty note. The gate is *more*
effective here than on desktop — there is nowhere to look away to. Body rows follow with
status marks.

**Sprint completion.** Segmented progress + elapsed time, 30px serif heading, three
numbered rows, the `--state` unresolved panel, the prediction panel, then two full-width
44px+ actions and the forgiving streak line centered at 12px `--muted`.
