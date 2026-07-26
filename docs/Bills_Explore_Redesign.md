# Bills Explore redesign — design vs. data notes

Rebuilds the Civersify **Explore → Bills** page (`frontend-civic/src/prototypes/magazine/pages/Bills.tsx`)
from the "Bills Explore v2" design handoff: one page with three switchable views
(**Front Page / The Floor / Compass Field**) over the same bill corpus, each scoring every
bill on the six compass axes the user scored themselves on.

The design was produced from screenshots only, without the data model. This documents where
the design assumed data that **does not exist** in the backend and how each gap was resolved.
Nothing is faked — every unbacked element either maps to a real field or degrades to an
honest empty/derived state.

## What was built

- `pages/Bills.tsx` — orchestrator: page header, URL-synced view switcher (`?view=floor|field`),
  search, stage segmented control, sort, axis chips, alignment threshold. Wide-canvas breakout
  (the design is 1440px; the magazine shell is 1024px — see #1).
- `pages/bills/model.ts` — the six canonical axes, stage mapping, client-side alignment mirror,
  radar geometry, per-bill view-model builder, sort.
- `pages/bills/svg.tsx` — `MiniCompass` (small hexagon, bill + optional user overlay) and
  `AlignmentRing`.
- `pages/bills/FrontPageView.tsx`, `FloorView.tsx`, `FieldView.tsx` — the three views.
- Reuses the real library (`Button`/`ButtonLink`, `ValueChip`), tokens, and `usePrefersReducedMotion`
  idioms. `CompassRadial` was **not** reused (it bakes in labels/legend and is unusable at 34px);
  `MiniCompass` is the stripped thumbnail variant.

## Data wiring

The list endpoint (`GET /api/bills`) returned no per-bill axis scores and no alignment — only an
`AxisCount`. The three views need per-bill axes + alignment for the **whole** corpus on one page.
Rather than fire N detail requests, one small **additive** backend change was made:

> **`BillSummaryDto.Axes: List<BillAxisScoreDto>`** (`{axisKey, score, confidence}`), populated in
> `BillsController.List`. User-independent (no compass in the query), so the list stays anonymous /
> cache-friendly. No DB migration — the data already lives on `Bill.AxisPositions`.

Alignment is then computed **client-side** against the user's compass (`GET /api/profile/me`, fetched
once), reusing the exact `Classify` / `OverallPercent` logic the bill-detail page already mirrors.
Cold start (no compass) is a first-class state — see #11.

---

## Discrepancies (design assumed → data has → resolution)

1. **Page width.** Design is a 1440px broadsheet; the magazine shell (`Layout.tsx`) caps content at
   `max-w-5xl` (1024px). → `MagazineLayout` now widens the **whole shell** — nav, content, and footer
   — to `max-w-[1440px]` for the `/bills` route only (a one-line `wide` flag on `useLocation`), so the
   chrome stays aligned with the body. All other routes (including `/bills/{id}`) are unchanged.

2. **Fixed six axes vs. a 15-axis catalog.** The design hard-codes a 6-vertex hexagon
   (Government role, Change speed, Economic fairness, Authority, Risk, Time horizon). The backend
   catalog has **15** axes, and each bill is scored only on the subset it implicates. → All six design
   axes exist as catalog keys, so the hexagon is built on those six; a bill missing one renders that
   axis at centre (score 0). Bills routinely have fewer than six positions (the UI shows "N of 6
   values mapped"). The other 9 catalog axes are not visualised here.

3. **Cosponsors — absent.** `Bill` has a single `Sponsor` string, no cosponsor list/count. → Every
   "N cosponsors" is dropped; sponsor + party is shown instead. The Floor card's `cosponsors/96` bar
   became an axis-**coverage** bar (`positioned / 6`).

4. **Read / view / "compass-reading" counts — absent.** No engagement tracking for bills. → All read
   counts, "+N read" deltas, "read 889 times", and "You've read 7 of 41" are removed. The hero's
   "Where you've been" block became a compass-status block. Momentum-by-reads is gone (see #6).

5. **Next scheduled action / days-out countdown — absent.** Only `LatestActionDate` (past) exists;
   no forward-looking schedule. → "Floor vote in 1 day" / "days out" countdowns are replaced with the
   real latest-action date. "On the calendar this week" became **"Latest to move"** (by
   `LatestActionDate`).

6. **Default sort "Momentum (new readings)" — unbacked.** → Default is **Recent action** (the list's
   native order). Sort options: Recent action, Best aligned*, Furthest from you*, Most values mapped.
   "Most cosponsors" is dropped. (*needs a compass.)

7. **Topic / policy category — absent.** No subject/policy-area field. → The card "topic" slot shows
   the bill's mapped-axis count ("5 values") instead. (`Jurisdiction` exists but is "Federal" for all,
   so it doesn't distinguish.)

8. **Stage taxonomy mismatch.** Design stages: Introduced · In Committee · **Floor Vote** · Passed
   Chamber · Enacted. Real `BillStatus`: Introduced, InCommittee, PassedOneChamber, PassedBothChambers,
   Enacted, Failed, Unknown. There is no "Floor Vote" status. → The pipeline uses the real statuses:
   Introduced · In Committee · **Passed Chamber** (PassedOneChamber) · **Passed Congress**
   (PassedBothChambers) · Enacted. `Failed` is treated as off-pipeline (not shown in a column);
   `Unknown` folds into Introduced. Stage colours preserved from the design.

9. **Activity feed "What moved today" — absent.** Status changes are not journaled; there is no
   movement/event table. → The Floor's feed became **"Latest activity"**, built from bills ordered by
   `LatestActionDate` (real date + status), not fabricated event kinds (Scheduled/Surge/Amended…).

10. **Stage-movement 8-week sparkline / "34 stage changes this week" — absent** (no history). → Replaced
    with a real **"Where the corpus sits · by stage"** distribution bar (live per-stage counts).

11. **Alignment on the list & cold start.** Alignment was detail-only. → Added raw bill axes to the
    list DTO and compute alignment client-side (above). When the user has **no** compass, every
    alignment affordance degrades per the design's own cold-start rule: rings/percentages show "—",
    cards drop the alignment fill for a coverage fill, the Compass Field hides the "You" marker and
    neighborhood radius, and the axis-chip row is replaced by a "Take the values quiz" prompt.

12. **Curated clusters ("Build-it-now bloc" etc.) — no clustering backend.** → The Compass Field keeps
    the crosshair, gridlines, user marker/neighborhood, and pole edge-captions, but the three named,
    hand-placed bloc ellipses are omitted (they were editorial). The X-axis **histogram** and
    **"Nearest in the field"** are computed for real.

13. **"What's in it" provision planks with curated tags** ("Hard deadline", "Most contested section")
    — no provision breakdown exists. → The detail panel lazily fetches `GET /api/bills/{id}` and shows
    the real **per-axis `Rationale`** entries, tagged with the axis name + which pole the bill leans.
    Bills with no positions show an honest "No per-axis rationale recorded" (visible on the 0-axis
    seed bill in testing).

14. **Follow / watchlist ("Bills I follow", "Bills you're following vote this week") — no such feature.**
    → Removed. The Floor "Saved views" became real computed presets (In committee / Passed a chamber /
    Enacted this Congress / Furthest from your compass) that drive the filters.

15. **Header stat trio (`41` / `3 Floor votes` / `12 Act within 7d`).** The latter two need scheduling
    data. → All three are real, derived from status: **Bills tracked / In committee / Enacted**.

16. **"Updated 9:41a from congress.gov".** No ingest timestamp surfaced. → Shows the newest
    `LatestActionDate` across the corpus ("Updated {date} from congress.gov").

17. **Search across committees / full bill text — partially unbacked.** No committee field or
    full-text is on the list payload. → Search matches identifier, title, short title, sponsor, and
    the teaser/summary. (Detail has `FullTextUrl` but not indexed text.)

## Follow-ups worth considering

- If the "Best aligned" ranking should filter server-side over the whole corpus (not just the page),
  move alignment into the list query behind the authenticated user (the design's own recommendation).
- A lightweight bill **read/engagement** counter would unlock the design's momentum sort, read deltas,
  and "what surged overnight" feed as real features.
- Persisting **stage-change events** would enable the real "what moved today" feed and the movement
  sparkline (currently a static distribution).

## Local dev DB note (for whoever runs this branch)

The local `civic` DB had 8 bills, all `Ingested` with **zero** axis positions (LLM synthesis hasn't
run locally), so the page would render empty. To smoke-test the three views, the 8 bills were flipped
to `Synthesized`, spread across statuses, and given **placeholder** axis positions/scores directly in
the **local** DB (not committed, not prod). Those scores are synthetic. To reset:
`UPDATE "Bills" SET "SynthesisStatus"='Ingested'; DELETE FROM "BillAxisPositions";` (the original
`Status` values were overwritten and are not recoverable without a re-ingest).
