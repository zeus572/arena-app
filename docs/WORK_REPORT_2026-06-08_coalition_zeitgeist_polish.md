# Work Report — Civersify polish: coalitions, quizzes, Zeitgeist & About

**Date:** 2026-06-08
**Branch:** `feature/coalition-zeitgeist-polish` (not pushed; not merged to `master`)
**App:** Civersify (`backend-civic` = Civic.API, `frontend-civic` = magazine prototype)

All six requested items are implemented, type-checked, unit/integration-tested, and
visually verified against a locally-run instance. The local backend was launched with an
**empty Anthropic API key** (so no LLM tokens were spent) and **shut down** after
verification (port 5050 confirmed free, no `Civic.API` process running).

---

## 1. Cover-story text no longer overflows the box

**File:** `frontend-civic/src/prototypes/magazine/components/CoverStory.tsx`

- Added `min-w-0` + `[overflow-wrap:anywhere]` + `hyphens-auto` so long headline words wrap
  instead of spilling past the right edge.
- Changed the desktop summary clamp from `md:line-clamp-none` to `md:line-clamp-2` so the
  summary can never grow taller than the fixed-height (`h-[360px]`/`h-[460px]`) gradient box
  and bleed out the top.

**Verified:** A long real headline ("Ultra-Orthodox Jews block roads and trains…") now sits
cleanly inside the box with the summary truncated to two lines.

## 2. Removed "Reflection / Think Deeper" from the home page

**File:** `frontend-civic/src/prototypes/magazine/pages/Home.tsx`

- Deleted the entire `Reflection` → "Think deeper →" section (it is superseded by Coalitions).
- The `/think-deeper/:slug` route and page were left intact (only the home-page promo was
  removed), so no other surface breaks.

## 3. Dynamic quizzes + global poll (60-day moving average)

The quiz was a fixed, always-identical list with no aggregate stats. It is now a shuffled,
dynamic set backed by a real global poll.

**Backend**
- New entity `QuizResponse` (`Models/QuizResponse.cs`) + `DbSet` + EF config
  (`Data/CivicDbContext.cs`), indexed on `(QuestionId, CreatedAt)` for the rolling window.
- New EF migration `20260608085308_AddQuizResponses` (creates the `QuizResponses` table).
- `QuizController` rewritten:
  - `GET /api/quiz/questions?count=N` now returns a **freshly shuffled subset** (default 6)
    so the quiz isn't the same every time, each question carrying `correctRate` +
    `responseCount` computed over a **trailing 60-day window**.
  - `POST /api/quiz/questions/{id}/responses` records one answer (server validates the index,
    decides correctness against `CorrectAnswerIndex`) and returns the updated poll
    (`QuizPollResultDto`: responseCount, correctCount, correctRate, windowDays = 60).
- Quiz seed bank enlarged from 4 → **12** questions (`Seed/quiz-questions.json`) so the
  shuffle has real variety.

**Frontend** (`api/quiz.ts`, `pages/Quizzes.tsx`)
- After answering, the user sees a **Global Poll** card: "{X}% got this right · {N} answers ·
  60-day avg" with a progress bar. Falls back to the load-time snapshot if the write fails.
- "Question i of N · fresh set every time"; "New questions" re-pulls a reshuffled set.

**Verified:** Answering a question recorded a response and showed "100% got this right · 1
answer · 60-day avg".

## 4. Coalition positions come from the Civic Compass; prevailing position is shown

The coalition join/position flow previously asked users to self-label as **left / center /
right** (partisan framing). That is replaced with the position **discovered through their
Civic Compass**, and the page now surfaces the coalition's current prevailing wording.

**Frontend only** — the backend `SpectrumBucket` is a free-form string, so no engine,
migration, or coalition-test changes were needed (the coalition geometry engine is untouched).

- New helper `frontend-civic/src/lib/compass.ts` — `deriveCompassPosition(profile)` maps a
  person's Civic Compass profile to the position they speak for (leads with their strongest
  **archetype**, e.g. "The Public Builder"; falls back to their most strongly-held value
  axis). `prettyBucket()` renders raw bucket strings for display.
- `CoalitionProvisionDetail.tsx`:
  - The left/center/right `<select>` is gone. The join control now shows **"Your Civic
    Compass position"** with the discovered label, or prompts users without a compass to
    "Build your Compass →" (`/onboarding`).
  - New **"Prevailing coalition position"** card: shows the leading version's wording (by
    coalition reach / net co-signs), with co-sign counts, or the neutral starting text plus
    "No agreed wording yet" when nothing has emerged.
  - Participants render `prettyBucket(p.bucket)` instead of raw "left/center/right".
- `CoalitionProvisionParticipate.tsx`: the "Speaking for" dropdown is replaced by the
  read-only Civic Compass position; `takePosition` now sends `compass.bucket`.

**Verified:** Detail page shows the prevailing-position card and the Civic-Compass join
control (with the build-your-compass fallback for anonymous users).

## 5. New "About" / explanation page

**Files:** `frontend-civic/src/prototypes/magazine/pages/About.tsx`, route in `App.tsx`, nav
links in `Layout.tsx` (top nav + footer).

- Explains the mission: building coalitions against real-world events **without partisan
  bickering**, discovering **how you'd govern**, and **making your voice heard**. Three steps
  (Compass → Coalitions → Zeitgeist), a "Why it matters" callout, and CTAs.
- Reachable at `/about` and linked from the top nav and footer.

## 6. New "Zeitgeist" page — discoveries from how people vote

A read-out of what the public is discovering, framed as signals worth sending to leaders.

**Backend**
- `ZeitgeistService` (+ `IZeitgeistService`, registered in `Program.cs`) and
  `ZeitgeistController` (`GET /api/zeitgeist`). Pure DB aggregation — **no LLM**:
  - **Where the public leans:** mean Civic Compass score per axis across all profiles, with a
    plain-language lean label ("Active public builder", "Split — no clear consensus", …).
  - **Where coalitions are forming:** each provision with its prevailing position (highest
    net-co-signed version, else neutral text), participant count, and a one-line signal.
  - **What trips people up:** the lowest-scoring quiz questions over the trailing 60 days.
  - Totals: compasses built, live coalitions, quiz answers (60d).
- DTOs in `Models/DTOs/ZeitgeistDto.cs`.

**Frontend:** `api/zeitgeist.ts` + `pages/Zeitgeist.tsx`, route `/zeitgeist`, nav + footer
links. Each coalition links back to its provision.

**Verified:** Page renders coalition discoveries, per-axis public leans with markers, and the
quiz blind-spots section.

---

## Testing

**Backend integration tests** (`backend-civic-tests/Civic.ApiTests`, Postgres on :5433):
- Rewrote `QuizControllerTests` for the new dynamic + poll behavior (subset bounds, `count`
  param, whole-bank fetch, **60-day moving-average math** across two answers, invalid-index →
  400, unknown-question → 404).
- New `ZeitgeistControllerTests` (all compass axes present with labels; quiz signals reflect
  recent answers).
- **Result:** all 8 new/updated tests pass.
- Full suite: **189 passed, 1 skipped, 8 failed.** The 8 failures are all pre-existing
  `CoalitionApiTests` / `CoalitionLifecycleServiceTests` integration tests that require an
  external Debate API / LLM ("Debate API returned 503: upstream down"). **Confirmed
  pre-existing** by stashing all of my changes and re-running on clean `master` — the same 8
  failed (7 passed), identical to this branch. My changes touch none of that code.

**Frontend:** `npm run build` (`tsc -b && vite build`) passes clean (per project convention
that CI uses the full build, not `tsc --noEmit`).

**Manual verification:** Ran backend (empty LLM key) + the dev frontend and drove the app with
a browser to confirm items 1–6 render and behave correctly. Backend then shut down.

## Notes / follow-ups (not done — out of scope)
- The Civic Compass → coalition `bucket` is computed client-side and kept compatible with the
  existing engine; the seeded demo agents still carry "left/center/right" buckets, so a
  provision's spectrum can mix archetype labels with the seeded ones. If desired, a future
  pass could migrate the seed agents to compass/archetype buckets for full consistency.
- Quiz questions are still authored (shuffled), not LLM-generated — intentional to avoid token
  spend; the "dynamic" requirement is met via the enlarged bank + shuffling.
