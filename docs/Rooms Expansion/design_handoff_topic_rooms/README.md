# Handoff: Civersify Topic Rooms

## Overview

Civersify is expanding from a bills-and-mini-games product into **Topic Rooms**: a
persistent, structured home for an ongoing news topic. This package documents 27
designed surfaces across seven PRDs (Theme Rooms, Story Rooms, Conversation Map,
Knowledge Graph, Money Trail, Interactive News Engagement, and the editorial tooling
that feeds them).

The product thesis the designs encode:

- A **Theme Room** is the durable container for a topic. It does not expire when the
  news cycle moves. It has ten inner sections and a status that is edited, not appended.
- A **Story Room** is the atomic unit — one development, always with the same nine-part
  structure, always carrying an evidence status.
- Everything reusable (claims, actors, sources, budget items, knowledge items) is a
  **first-class object** referenced by rooms, not text duplicated into them. A status
  change on one claim must propagate everywhere it appears.
- Nothing is presented as more certain than the evidence supports. Uncertainty is a
  visible, typed property of every factual statement.

All designs are populated with one pilot theme — **U.S.–Iran escalation** — so the same
content can be compared across densities, reading rhythms, and platforms. **The copy is
illustrative sample content written for design purposes. It is not reporting and must
not ship as fact.** Replace every string with real editorially-reviewed content.

---

## About the design files

`designs/Topic Rooms.dc.html` is a **design reference created in HTML** — a prototype
that shows intended look, structure, and behavior. It is **not production code to copy**.

It is a single pan-and-zoom canvas holding all 27 options side by side, each with a
visible id badge (`1a`, `1b`, `1c` …). Open it in a browser and scroll/zoom. Ids are the
shared vocabulary for this handoff: every reference below (`1a`, `1n`, `1s`) points at a
labelled block in that file.

Your task is to **recreate these designs in Civersify's existing environment**, using its
established patterns, routing, data layer, and component library. If no frontend
environment exists yet, pick the framework that fits the rest of the stack and implement
there. Concretely:

- The canvas is a flat presentation surface. Do **not** reproduce the canvas, the id
  badges, the option headers, or the "Turn 1" chrome — those are review scaffolding.
- Every option is a *screen or module*, not a page in a flow. Several options are
  alternative treatments of the same surface (see "Choices still open").
- Styling in the prototype is inline for streaming reasons. In the real codebase, use the
  design system's components and Tailwind + token classes as described below.

---

## Fidelity

**High fidelity.** Colors, type scale, spacing rhythm, borders, and copy tone are final
intent. Layout proportions, column widths, and hierarchy should be matched closely.

Two caveats:

1. **Interactions are depicted, not implemented.** The prototype is static: sliders show
   a fixed value, quiz options show a fixed selection, diff mode shows one comparison.
   The behavior specs below are the source of truth for what these do.
2. **No real imagery.** The designs deliberately use no photography or illustration —
   type, rule, and mark only. Where the real product wants imagery (Story Room heroes,
   map explainers), treat that as an open design task, not an omission to fill with stock.

---

## The design system

Civic Arena, bound at
`designs/_ds/civic-arena-design-system-a107f0bd-aa0e-45f8-aafe-23a4b3661074/`.
It is the published `frontend-civic` React library bundled as a browser global. In the
real codebase, consume `frontend-civic` as a dependency rather than the bundle.

Two hard requirements from the DS guide:

1. Every tree must be inside `className="theme-magazine"`. All tokens are declared on
   that class, **not** on `:root`. Without it, accent fills render invisible and the
   serif display font falls back.
2. Many components use react-router (`Link`, `useNavigate`) and throw outside a Router.
   `DesignProvider` supplies both wrappers.

### Tokens (verbatim, from `_ds_bundle.css`)

```css
.theme-magazine {
  --bg:            oklch(98.5% .008 60);   /* page background */
  --bg-elev:       oklch(100% 0 0);        /* elevated surface / cards */
  --fg:            oklch(15% .01 50);      /* primary text; also inverse panel bg */
  --fg-soft:       oklch(32% .01 50);      /* secondary text */
  --muted:         oklch(50% .01 50);      /* tertiary text, labels */
  --border:        oklch(85% .01 50);      /* hairlines */
  --accent:        oklch(60% .22 35);      /* warm red-orange; actions, "new" */
  --federal:       #1d4e89;                /* civic accent: federal / "for" side */
  --federal-soft:  #1d4e891f;
  --state:         #b5552d;                /* civic accent: state / "against", warnings */
  --state-soft:    #b5552d1f;
  --radius:        0px;                    /* NOTE: zero. Nothing is rounded. */
  --font-display:  "Iowan Old Style", "Charter", "Source Serif Pro", "Georgia", serif;
  --font-body:     "Inter", system-ui, sans-serif;
  --text-base:     18px;
  line-height: 1.65;
}
.theme-magazine h1, h2, h3, .display {
  font-family: var(--font-display); letter-spacing: -.02em; font-weight: 700; line-height: 1.05;
}
```

Two extra surface values are used in the designs and should be added as tokens rather
than left as literals:

| Purpose | Value | Suggested token |
|---|---|---|
| Canvas / recessed panel behind cards | `oklch(93.5% .012 60)` | `--bg-sunken` |
| Inset panel inside a card (sidecars, footers) | `oklch(97% .01 60)` | `--bg-inset` |
| Diff highlight (changed sentence) | `oklch(88% .04 60)` | `--highlight-changed` |

### Style rules the designs depend on

- **Zero border radius, everywhere.** `--radius` is `0px`. No rounded cards, no pill
  buttons, no rounded avatars. This is the single most identity-defining rule.
- **Hairline borders instead of shadows.** `1px solid var(--border)` separates things.
  `2px solid var(--fg)` opens a major section. There are no drop shadows in the designs.
- **Serif for display, sans for everything else.** Serif (`.display`) is used for
  headlines, status sentences, big numbers, and pull-quote-scale statements. It is never
  used for labels, table cells, or UI chrome.
- **Uppercase micro-labels.** `font-size: 10–11px; font-weight: 600–700;
  letter-spacing: .2–.28em; text-transform: uppercase; color: var(--muted)` — or
  `var(--accent)` when the label marks something new or interactive.
- **Inverse panels for emphasis, not color.** When something must dominate (Watch Next,
  Before You Know, re-entry card), it becomes `background: var(--fg); color: var(--bg)`
  with `oklch(100% 0 0 / .55–.9)` for secondary text on it. Accent is used sparingly
  inside those panels.
- **Type scale in use:** 58 / 52 / 44 / 40 / 34 / 32 / 30 / 27 / 25 / 24 / 21 / 19 / 17 /
  16 / 15 / 14 / 13 / 12 / 11 / 10 px. Body copy is 15–19px; never below 12px except
  micro-labels at 10–11px uppercase.
- **Spacing rhythm:** section padding 40–64px horizontal on desktop, 20–28px on mobile.
  Row padding 11–18px vertical. Gaps 8 / 10 / 14 / 20 / 24 / 32 / 40 / 56px.
- **Layout with flex/grid + `gap`.** No margin-based spacing between siblings.
- `text-wrap: pretty` on every multi-line prose block.

### Components used from the DS

`Button` (`variant="primary" | "secondary" | "ghost"`, `size="sm"`, `fullWidth`),
`ValueChip` (`label`, `selected`) for taggable answer chips. `DisclaimerBadge`,
`Term`, `PullQuote`, `CaveatGrid`, `SplitBar`, and `BudgetFactCard` are all
appropriate for these surfaces and should be preferred over new components — see
`components/<group>/<Name>/<Name>.prompt.md` in the DS source for each contract. Note
that `BudgetFactCard` and `CountdownTimer` fetch on mount and expect a live API.

---

## Screens

Full per-screen specs are in **`SCREENS.md`** — layout, columns, exact copy, and the
behavior of each module, screen by screen. What follows is the map and the
cross-cutting systems. Read this file first, then `SCREENS.md` for the screen you are
building.

| Id | Screen | PRD | Notes |
|---|---|---|---|
| `1a` | Theme Room front door — "Dispatch" | 01 | Default entry. Editorial, low density. |
| `1b` | Theme Room — "Situation Board" | 01 | High-density alternative view of the same room. |
| `1c` | Density dial spec | 01 | The Read / Brief / Board mechanic connecting 1a and 1b. |
| `1d` | Delta ledger | 01 | "What changed" as an in-place unfurl. |
| `1e` | Diff mode + revision scrubber | 01 | "What changed" as room-level markup. Most ambitious. |
| `1f` | Re-entry card | 01 | "What changed" arriving from a notification. |
| `1g` | Theme Room § Latest | 01 | Bounded development list with a stated inclusion rule. |
| `1h` | Theme Room § Understand | 01/04 | Timeline spine, glossary, confusion pairs. |
| `1i` | Theme Room § People & Power | 01 | Actors tiered by leverage over a named decision. |
| `1j` | Theme Room § Government & Law | 01 | Where the existing /bills experience plugs in. |
| `1l` | Theme Room § Sources & Methodology | 01/03 | Sampling disclosure + correction log. |
| `1m` | Evidence status system | 01/04 | The eight-status mark vocabulary. |
| `1n` | Claims & Evidence ledger | 04 | Sortable claim table with an expanded evidence trail. |
| `1o` | Story Room — single column, gated | 02 | First-contact rhythm. Pre-exposure prompt up top. |
| `1p` | Story Room — bill, sidecar rhythm | 02/06 | Persistent state-of-play. Vote Before Reading. |
| `1q` | Conversation § argument clusters | 03 | Clusters + competing concerns, no feed. |
| `1r` | Conversation § three layers | 03 | Official / informed / public, plus viral-vs-prevalent. |
| `1s` | Money Trail — pipeline ladder | 05 | Horizontal five-stage funding ladder. |
| `1t` | Money Trail — descent ladder | 05 | Vertical form + "Guess the Stage". |
| `1u` | Timeline Builder | 06 | Ordering interaction; payoff is the knowability pass. |
| `1v` | Calibrated prediction + resolution | 06 | Probability slider, then calibration feedback. |
| `1w` | Budget Allocator | 05/06 | Constrained allocation, deliberately unscored. |
| `1x` | Civic Sprint completion | 06 | Ends on learning + open question, never a score. |
| `1y` | Editorial: Story Bundle review | 02 | Structured form with blocking publish gates. |
| `1z` | Editorial: correction propagation | 02/04 | Fan-out of a single status change. |
| `1aa` | Mobile front door (first + returning) | 01 | 390px. |
| `1bb` | Mobile Story Room + Sprint | 02/06 | 390px. |

---

## Cross-cutting system 1: the density dial (`1c`)

One sticky, always-visible control in the room header with three states:

| Mode | Rendering | Default for |
|---|---|---|
| **Read** | Prose carries meaning. One idea per screen height. No counts, filters, or tables. | First visit |
| **Brief** | Prose collapses to labelled rows. Status marks appear. Still no filters. | — |
| **Board** | Full object tables. Sortable, filterable, source and revision columns. | Users who chose it twice consecutively |

**The invariant:** density changes the amount of scaffolding around facts, never the facts
themselves. A claim that is `disputed` in Read is `disputed` in Board. Nothing is hidden
in Read that is shown in Board — it is *expressed differently*. If a module cannot render
at all three densities, it is not a module yet; that is a design gate, and it should be a
code gate too (a module component should accept `density` and have a real branch for each).

Behavior: persisted per user (not per room). Never auto-switches mid-session. Never
hidden. Mobile offers Read and Board only.

## Cross-cutting system 2: evidence status (`1m`)

Eight statuses, applied to every factual statement. The mark is a **14–16px square** whose
fill pattern encodes the status; it is always accompanied by the status **word** in any
non-inline context.

| Status | Mark | Meaning |
|---|---|---|
| Confirmed | solid `--fg` | Multiple independent sources, or a primary document |
| Strongly supported | `--fg` filled to 75% height, `--border` above | Good evidence, no contradiction, not independently confirmed |
| Plausible but unresolved | `oklch(60% .01 50)` to 45%, 1px `--muted` border | Could be true; settling evidence does not exist yet |
| Disputed | split vertically: `--federal` left, `--state` right | Credible sources directly contradict each other |
| Unsupported | 45° hatch of `--border`, 1px `--muted` border | Circulating with no evidence behind it |
| False | 1px `--fg` border with a single 45° slash | Evidence shows it is not true |
| Outdated | 1px dashed `--muted`, `oklch(96% .005 50)` fill | Was accurate; something changed. Stale date shown |
| Prediction | 1px dotted `--accent`, `oklch(95% .03 40)` fill | A statement about the future, not a fact |

Requirements:

- **Never colour alone.** Every mark is distinguishable by fill pattern in greyscale.
- **Inline marks** sit immediately after the clause they qualify, are focusable, and
  announce as e.g. *"disputed claim, four assessments — open evidence"*.
- Hovering a mark dims everything except statements at that status.
- `False` and `Unsupported` claims are **retained**, never deleted — the ledger records
  that the claim exists and what the evidence does.
- Status is a property of the **claim object**, so the mark renders from data. A status
  change must fan out (see `1z`).

## Cross-cutting system 3: what "changed" means (`1d` / `1e` / `1f`)

A **meaningful change** — the only kind that notifies, and the only kind counted in the
"since your last visit" ribbon — is one of:

1. An official body acts (vote, ruling, order, filing)
2. A verified fact changes
3. A claim's evidence status moves
4. Money advances a funding stage
5. A negotiation status changes
6. A prediction resolves
7. A correction is issued

Explicitly **not** meaningful: new commentary about an old event, copy edits, added
sources on an existing fact, typo fixes. These appear in the full changelog only. The
"not shown" count is displayed honestly (`1d`: *"11 edits we did not bother you with"*).

Corrections get their own visual treatment and are **never folded into "updated."**

## Cross-cutting system 4: the ambient path (`1a`)

No stepped wizard. Each of the room's sections carries a thin progress bar (3px,
`--accent` on `--border`) plus a plain-language count ("5 of 8", "Not opened"). Free
browsing; the bars only remember. Copy states this explicitly: *"Nothing is required.
The bars just remember where you have been."*

## Cross-cutting system 5: conversation guardrails (`1q` / `1r`)

The Conversation surfaces must remain useful with usernames and exact quotes hidden. The
primary object is the **argument**, never the post.

Non-negotiable, and worth encoding as tests:

- Sampling disclosure renders **above** the clusters, not in a footer. It states window,
  named sources, item count before and after dedup, and that prevalence is
  conversation-based.
- Every prevalence bar is labelled with what it measures, every time.
- Each cluster shows a **competing concern from the same sample**.
- Ranking uses prevalence, distinctiveness, and stakeholder coverage — **never engagement
  counts**. Virality can never upgrade a claim's status.
- No scrolling feed. No sentiment score. No naming of private individuals. No gamifying
  firsthand accounts of harm.
- Three layers (official / informed / public) are physically separate bands and are never
  merged into one list.

## Cross-cutting system 6: the money ladder (`1s` / `1t`)

Every budget item is rendered across all five stages, **including the empty ones**:

`Requested → Authorized → Appropriated → Obligated → Spent`

- Empty stages render as visible empty (dashed border + hatch), never omitted. A stage
  that does not apply says so.
- `whatThisDoesNotMean` is a **required field** on every money item, rendered in an
  inverse panel — not a tooltip.
- Government outlays and modelled economic effects live in **separate halves of the page**
  and are never summed.
- Comparisons must be same-year, same-jurisdiction, same-source. Per-capita framing on
  one-time military requests is rejected — `1s` shows the rejected comparison and why,
  which is a pattern worth keeping in the editorial tool.

---

## Interactions & behavior

### Before You Know (`1o`, `1bb`)
Pre-exposure prompt on an inverse panel, above the article body. One question, 3–4
options. No timer, no streak, no penalty. Answer is required to continue (that is the
point — commitment before exposure). After answering: reveal the answer, the explanation,
and what share of readers picked each option.

### Fact / Opinion / Interpretation / Prediction (`1o`)
3–5 sentences pulled **verbatim** from real coverage. User drags a `ValueChip` onto each.
Every answer gets an explanation, right or wrong. Correct answers can depend on a claim's
current status — so items must be revalidated when a status changes (`1z`).

### Vote Before Reading (`1p`)
Yes / No / Not sure, before the arguments. First answer is hidden from the user until
they have read both sides, then both answers shown with whether they moved. **Private by
default; never aggregated publicly per user.**

### Timeline Builder (`1u`)
Drag 6 events into order. Drop zones show placement state. The payoff is the second pass:
the same timeline annotated with **what was knowable on each date**, showing that most
confident takes predate the evidence that contradicted them.

### Calibrated prediction (`1v`)
Probability slider 0–100 with the crowd mean marked as a tick on the same track. Resolution
criteria and cancellation conditions stated before answering. Scored with a proper scoring
rule. Resolution screen shows your number, the crowd's, the outcome, and a
calibration-by-confidence-band chart with overconfident bands in `--state`.
**Private by default. Never a leaderboard.**

### Budget Allocator (`1w`)
Fixed pot, sliders per category, live remaining figure. At least two **hard constraints**
sourced from the real request (e.g. a readiness floor drawn as a `--state` line on the
track; a statutory oversight cap). On submit: show the user's split against the actual
request, name the tradeoff they made and who it affects, and say nothing about whether
they were right. Always show the scope caveat ("one supplemental request, not a federal
budget").

### Guess the Funding Stage (`1t`)
Real headline + five stage chips. Feedback explains **why the wrong answer is tempting**
(spending verbs over a request document) and notes that the headline is not technically
false.

### Civic Sprint (`1x`, `1bb`)
Finite session, 3 segments, ~4 minutes, progress bar at top. Completion screen shows
three things learned, **one thing still unresolved**, and any prediction made. Never a
score. Streak language is explicitly forgiving: *"Miss tomorrow and nothing is lost."*
The only notification it schedules is prediction resolution.

### Diff mode (`1e`)
Full-room markup comparing the user's last-seen revision to current. Changed sentences
highlighted (`--highlight-changed` with a 3px box-shadow bleed), retired sentences struck
through in `--muted`, changed rows tinted. A revision scrubber shows tall marks for
meaningful changes, short marks for edits, and a hollow marker at the user's last visit;
dragging it walks the room backwards. A change index sidebar lists every diff with a
type swatch. **This is the most expensive item in the set — see "Choices still open."**

### Responsive
Desktop reference width **1280px** (content max ~1180px). Mobile reference **390px**.
Transformations at mobile: density dial → Read/Board only; section rail → horizontal
scroller with progress bars; three-column fact grids → stacked numbered rows; money
pipeline (`1s`) → descent ladder (`1t`); actor tiers → single column with the tier as a
sticky band; claim table → stacked cards with the status mark and word first.
**Hit targets never below 44px.**

### Accessibility
- Status marks are pattern-differentiated, focusable, and labelled in text.
- The timeline (`1h`) and every chart have a full text alternative.
- Interactions need a keyboard path — drag-and-drop must have a select-then-place fallback.
- `prefers-reduced-motion` is respected by the DS (`.tax-animate`); honor it in new
  transitions too.
- Contrast: `--muted` on `--bg-elev` is the floor. Do not go lighter for body text.

---

## State management

### Per user, per room
`lastSeenRevision` (drives 1d/1e/1f and the ribbon), `density` (global, not per room),
`sectionProgress` (per section: opened, items seen / total), `following` (bool),
`predictions` (question id → probability, timestamp, resolution, score),
`interactionAnswers` (id → answer + whether pre/post exposure), `calibrationSummary`
(derived, by confidence band). Prediction and vote answers are **private by default**.

### Per room (content)
`revision` (integer, incremented on any edit), `statusSentence`, `essentialFacts[]`,
`lastMeaningfulUpdate`, `changeLog[]` (each entry typed as meaningful or not),
plus references into the shared object graph.

### Shared objects (the graph)
`Claim` (text, status, statusHistory[], evidenceFor[], evidenceAgainst[],
whatWouldSettleIt, appearsIn[]) · `Actor` (name, type, roleHere, actualPower, statedWants
+ source, constrainedBy, appearsIn[], leverage per decision) · `Source` (type, primary?,
url, usedFor[]) · `MoneyItem` (amount, stage, period, requestedBy, decidesNext,
dollarBasis, whatThisDoesNotMean, breakdown[], comparisons[] with accepted/rejected) ·
`KnowledgeItem` (term, definition, confusionPair?) · `Development` (date, category,
whyItMatters, inclusionReason, status) · `ConversationCluster` (label, prevalence,
underlyingValue, competingCluster, attachedClaims[], sampleMeta).

**Fan-out is a requirement, not a nicety.** A claim status change must automatically update
the mark and label everywhere, the room changelog, every follower's delta view, and the
claim's own history — and must **flag for human review**: any room status sentence
referencing it, any interaction whose correct answer depends on it, any conversation
cluster whose framing note references it, and any share cards in circulation (`1z`).
Flagged objects are hidden from new sessions after **6 hours unreviewed**. The published
metric is time-from-source-correction, not time-from-our-noticing.

### Editorial state (`1y`)
Bundle status (`draft` / `in review` / `published`), per-field provenance
(`proposedBy: model | human`, `verifiedBy`, `verifiedAt`), and **nine publish gates**.
Gates are blocking: Publish stays disabled until each is cleared by a named person, and
the names are stored with the revision. Model-proposed, unverified fields render with a
3px `--accent` left rule. The tool actively blocks contradictions: a claim with
contradicting evidence of comparable quality cannot publish above `Disputed`.

---

## Assets

None. No images, icons, illustrations, or fonts are shipped in this package. Iconography
in the prototype is CSS-drawn squares and rules only. Fonts come from the DS token stack
(`Iowan Old Style` / `Charter` / `Source Serif Pro` / `Georgia` for display; `Inter` for
body) — the prototype loads Inter and Source Serif 4 from Google Fonts as a stand-in for
the system serif stack, which is a prototype convenience, not a requirement.

---

## Choices still open

Three decisions were deliberately left to the product team; they change scope materially.

1. **Is Board a mode or a destination?** `1c` makes density a dial across all surfaces,
   which is elegant but means every module needs three real renderings. The cheaper path
   is a separate `/board` destination per room. Decide before building modules.
2. **Is Diff mode (`1e`) in scope for MVP?** It requires per-sentence revision tracking
   and a diff renderer. The delta ledger (`1d`) delivers most of the retention value from
   the typed changelog alone and needs no sentence-level diffing.
3. **Does leverage-based actor sorting (`1i`) survive at scale?** Sorting actors by
   leverage over one named decision is defensible and sourceable, but it is a per-decision
   editorial judgment that must be re-made whenever the room's next decision changes.

## Not designed yet

Classroom mode, the map/geography explainer, standalone actor pages, the values-and-tradeoff
profile, notification settings, and room discovery / search.

---

## Files

```
design_handoff_topic_rooms/
├── README.md          ← this file
├── SCREENS.md         ← per-screen specs, keyed by option id
├── designs/
│   ├── Topic Rooms.dc.html     ← the design reference (open in a browser)
│   ├── support.js              ← prototype runtime; not for production
│   └── _ds/…                   ← Civic Arena bundle (css, js, README)
└── source_prds/       ← the seven original PRDs, verbatim
```

The PRDs in `source_prds/` are the requirements of record. Where this README and a PRD
disagree on intent, the PRD wins; where they disagree on visual treatment, the design wins.
