# Progress — Civic "new stories" notification (active-tab polling)

**Branch:** `feat/civic-new-stories-notification` (off `master`)
**PR:** #86 — https://github.com/zeus572/arena-app/pull/86
**Commit:** `6708fa4` (11 files, +464/−1)
**Status:** Implementation complete. Builds clean, 49 unit tests pass. Not merged.

## Goal
Notify Civersify readers when new news stories (**briefings**) land, polling a cheap
signal **only while the tab is active** — never when backgrounded or the user is idle —
so there's no constant polling.

## What was built

### Backend
- `GET /api/briefings/latest` → `{ latestCreatedAt, total }`. Lightweight "is there
  anything new?" signal (no page payload), locality-walled exactly like the feed list.
  - `backend-civic/Controllers/Api/BriefingsController.cs` — `Latest()` action. Uses
    `MaxAsync(b => (DateTime?)b.CreatedAt)` so an empty set yields null, not a throw.
    Route literal `latest` takes precedence over `{slug}` in attribute routing.
  - `backend-civic/Models/DTOs/BriefingDto.cs` — `BriefingLatestDto`.

### Frontend (`frontend-civic/`)
- `src/lib/useVisibilityPolling.ts` — **reusable** hook. Runs a callback on an interval
  only while the tab is **visible** (Page Visibility API) AND the user isn't **idle**
  (optional `idleTimeoutMs`; resumes on interaction). Genuinely clears the interval when
  gated off. Pure helpers `isIdle` + `pollingActive` exported for tests.
- `src/lib/newStories.tsx` — `NewStoriesProvider` context. Polls the signal, tracks a
  baseline, computes `newCount`. Exposes `newCount`, `feedVersion`, `refresh()` (catch up
  + reload feed), `dismiss()` (mark seen, no reload). Pure `computeNewCount` exported.
  - Tunables at top: `POLL_INTERVAL_MS = 120_000` (2 min), `IDLE_TIMEOUT_MS = 10*60_000`.
- `src/prototypes/magazine/components/NewStoriesBanner.tsx` — floating "N new stories ·
  Show" pill with a dismiss ✕. Hidden when `newCount <= 0`.
- Wiring:
  - `src/App.tsx` — `NewStoriesProvider` wraps `<Routes>`.
  - `src/prototypes/magazine/Layout.tsx` — `<NewStoriesBanner />` at top of layout, so it
    shows app-wide (anonymous + signed-in, locality-aware). NOT on `/shorts` or `/welcome`
    (those render outside `MagazineLayout` by design).
  - `src/prototypes/magazine/pages/Home.tsx` — consumes `feedVersion`; on refresh reloads
    the feed and jumps to page 1 (new briefings get `IssueOrder = 0`, so they sort to the
    top of page 1).
- Tests: `src/lib/useVisibilityPolling.test.ts`, `src/lib/newStories.test.ts` (pure logic
  only, matching the repo's `useCountUp` test pattern — no @testing-library/react in deps).

## Key design facts / rationale
- **Signal:** `MAX(CreatedAt)` = "something newer" trigger; total delta = count. If a story
  is *replaced* (newest timestamp advances, count flat), still surfaces "1 new story".
- **News briefings sort to top of page 1:** `CivicContentGenerationService.cs:347` sets
  `IssueOrder = 0`; feed orders by `IssueOrder` asc then `CreatedAt` desc.
- **Active-tab only, by request:** reaching a backgrounded tab / Capacitor Android shell
  would be a separate Web Push / system-notification path (not in scope here).

## Verification done
- `backend-civic`: `dotnet build` → 0 warnings, 0 errors.
- `frontend-civic`: `npm run build` (tsc -b + vite) clean; `npm run test` → 49 pass.
- NOTE: `npm run lint` fails **repo-wide** — there is no `eslint.config.js` in
  `frontend-civic` (pre-existing gap, unrelated to this change). `tsc` is the real gate.

## How to resume
```bash
git checkout feat/civic-new-stories-notification
```

## Open follow-ups (not started)
- Eyeball the banner in the running app; tune placement (`top-16` / `md:top-24` floating
  pill) — could be an inline strip under the header or a corner toast instead.
- Tune interval / idle timeout if desired.
- Optional: Web Push / native (Android) notifications for backgrounded tabs.
- This progress file is a handoff aid; remove it before merge if you don't want it in `master`.
