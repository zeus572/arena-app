# Work Report — Weekly Cohorts & Coalition Participate redesign

**Date:** 2026-06-10
**Branch:** `feature/weekly-cohorts-participate` (committed locally; **not** pushed, **not** merged to `master`)
**App:** Civersify (`backend-civic` = Civic.API, `frontend-civic` = magazine prototype)

Both requested items are implemented, type-checked, tested, and visually verified against a
locally-run instance (backend launched with an **empty Anthropic key** → no LLM tokens spent,
then shut down).

---

## 1. Weekly cohort of 50

Every week a user now works the bills alongside a fixed group of up to 50 people, seeded from
their league (friends) and topped up with others — instead of everyone's experience being open
to all. Includes a weekly leaderboard against that cohort.

**Backend**
- New entities `Cohort` + `CohortMember` (`Models/Cohort.cs`), DbSets + EF config
  (`Data/CivicDbContext.cs`), and migration `20260610065906_AddCohorts`.
  - Unique `(AnchorLeagueId, WeekKey)` → one cohort per league per week (null anchors are
    distinct, so solo users get their own). Unique `(UserId, WeekKey)` → one cohort per user
    per week.
- `CohortService` (`Services/CohortService.cs`, registered in `Program.cs`):
  - `WeekOf(utcNow)` → Monday-anchored week key (`yyyy-MM-dd`) + window start.
  - `GetOrCreateForUserAsync`: returns the user's existing weekly cohort, or assembles one —
    **(1)** seed from the user's primary league (owned first, else earliest-joined) so friends
    share a cohort, **(2)** random-fill from the real-user pool (league members ∪ profiles ∪
    coalition participants), then agents, up to 50. Respects "one cohort per user per week"
    and tolerates races on the unique indexes. The matching is intentionally simple — a
    placeholder we can improve later.
  - Builds a **weekly leaderboard**: each member's coalition points (`CoalitionActs`) and active
    days within the week window, ranked, with the caller's rank/points.
- `GET /api/cohort/me` (`CohortController`) → `CohortDto` (member count / target, league name,
  friends count, your rank/points, leaderboard).

**Frontend**
- `api/cohort.ts` + `pages/Cohort.tsx` (route `/cohort`, nav link): a fill meter
  (`N of 50`), friends-from-league count, and the weekly leaderboard with the caller
  highlighted, friend (★) and agent badges, and active-day streaks.

**Verified:** the page filled to 50 from the dev user pool, listed agents + members, and
highlighted "You" at the correct rank.

## 2. Coalition participate redesign

The participate flow was split: read-only sub-questions, then a separate "Propose a carve-out"
flyout. It's now one clean flow, and the system's default arguments no longer headline the bill.

**Frontend-only** (`pages/CoalitionProvisionParticipate.tsx`) — uses existing endpoints; the
coalition geometry engine and its tests are untouched (those tests assert the engine still
seeds base versions, so the engine was intentionally not changed):
- **Inline answers:** each sub-question card now has selectable option chips. You choose your
  position directly in the cards (no separate flyout).
- **Save → compare:** "Save my answers & see where I land" reveals **"How your answers
  compare"** — existing versions ranked by closeness (`matches / answered`, computed client-side
  from each version's `positions`), each with co-sign / decline.
- **Present / carve-out:** one CTA puts your answers on the table via the existing
  `proposeAmendment` endpoint — labelled **"Present this as the bill"** when no cohort member
  has presented yet, or **"Propose this as a carve-out"** once versions exist.
- **Cohort presents first (framing for item 2a):** versions authored by a real member are
  treated as the bill's positions; the system's neutral seed versions are demoted to a
  collapsed **"Starting drafts"** section. When no member has presented, a **"No one in your
  cohort has presented this bill yet — be the first"** banner leads the page.
- The Civic Compass position (from the prior feature) is shown when presenting, and the user
  joins with their compass bucket on first action.
- A secondary "describe in your own words" collapsible keeps the free-form extraction path.

**Verified:** picking `large-only` + `exempt` and saving ranked a carve-out (2/2 match) above a
draft (1/2 match) and offered "Propose this as a carve-out".

---

## Testing
- **New backend integration tests** (`CohortControllerTests`, Postgres on :5433):
  `WeekOf` Monday math; `GET /api/cohort/me` returns the caller and is idempotent across calls;
  league friends seed the cohort (friends count + league name); weekly points reflect
  `CoalitionActs` in the window. **All 4 pass.**
- **Full suite:** 193 passed, 1 skipped, 8 failed. The 8 failures are the **same pre-existing**
  `CoalitionApiTests` / `CoalitionLifecycleServiceTests` integration tests that need an external
  Debate API / LLM ("Debate API returned 503: upstream down") — unrelated to this work
  (confirmed earlier by running clean `master`). My changes added 4 passing tests and 0 new
  failures.
- **Frontend:** `npm run build` (`tsc -b && vite build`) passes clean.
- **Manual:** ran backend (empty LLM key) + dev frontend, drove both new UIs in a browser, then
  shut the backend down.

## Notes / follow-ups
- The cohort currently **groups** people and scores them weekly; it does not yet *gate* which
  provisions/participants you can see by cohort. Scoping coalition participation to the cohort
  is a larger engine change and is a natural next step.
- Matching is league + random fill (v1). Future: balance by Civic Compass spread, activity, or
  timezone; recycle inactive slots; persist cohort history for week-over-week stats.
- Item 2a is realized as UX framing (demote system drafts, "be first" banner) rather than
  removing backend base-version generation, because the coalition engine and its test contract
  depend on those versions existing.
