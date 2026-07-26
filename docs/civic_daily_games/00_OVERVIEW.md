# Daily Games — Overview & Shared Platform

**Status:** spec — not yet implemented
**Date:** 2026-07-25

## Why

Civersify's existing surfaces are good but heavy. The coalition loop is the flagship
and it asks for a 5–15 minute reading-and-writing session; Campaign Manager asks for
20–30 minutes a week. Both have high activation cost, which makes them poor
top-of-funnel. The gamification docs already name this problem — with a mixed-age
public audience and no classroom layer, "gamification must carry return motivation
alone" (`civic_arena_gamification/00_OVERVIEW.md`).

Daily Games are the shallow end: six ~60-second, civics-themed puzzles that an
anonymous first-time visitor can play without an account, that refresh with the news,
and that produce a shareable result. They exist to widen the top of the funnel and to
give `Cohort.tsx` — currently a leaderboard with no verb attached to it — something to
score.

**Deferred from the original idea set:** a Connections-style grouping game (too
recognizably a clone) and a process-ordering game (static content, doesn't refresh
with the news).

## The six games

| # | Game | One-liner | Content source | LLM at generation? |
|---|---|---|---|---|
| 01 | **Fork** | Daily "would you rather" — two costly options, one tap | `SubQuestion` on live provisions | Rarely (fallback only) |
| 02 | **Crowd Call** | Guess what share of people got a question right | `QuizResponse` tallies + authored poll bank | No |
| 03 | **Priced In** | Guess the size of a real federal figure, 3 guesses | New `magnitudes.json` + `TaxEngine` | No |
| 04 | **Place It** | Guess where a real bill sits on 3 compass axes | `BillAxisPosition` | No |
| 05 | **Time Machine** | Sort real headlines by era / spot this week's | `NewsItem` + archival bank | No |
| 06 | **Whose Value** | Name the value an argument appeals to | `BillAxisPosition.Rationale` | No |

Each has its own spec file in this directory. **Read this document first** — it defines
the shared table, controller, XP hook, streak, and share format that all six use.

## Design constraints these inherit

From `civic_arena_gamification/01_PHILOSOPHY_AND_WIN_CONDITION.md` and
`llm-cost-mapping.md`, non-negotiable:

- **~60 seconds.** One puzzle is one sitting. No multi-session state.
- **Zero LLM at play time.** Generation is a once-daily background job; play is pure
  computation against precomputed rows. This is the existing rule — "structured scoring
  (no LLM) for continuous, every-provision, every-day signals; LLM only for irreducible
  discrete judgments."
- **Anonymous play is first-class.** No sign-in wall on any game. Play state keys on the
  `X-User-Id` device id and rekeys on sign-in via the existing
  `POST /api/auth/link-anonymous`.
- **No party labels as identity.** Answers map to the 15 compass axes
  (`backend-civic/Seed/axes.json`), never to parties.
- **Volume must not dominate.** Per the philosophy doc's third named trap, "a thoughtful
  adult checking in 3×/week with two real bridges should out-rank a teen grinding 20
  mediocre comments/day." Daily-game XP is deliberately small and capped — see below.
- **Soft cadence, not hard streaks.** The docs argue against daily streaks because they
  "punish the reflective, occasional, busy user" and risk compulsive engagement for
  minors. We ship a weekly "3 of 7" ring, not a breakable counter.

## Shared data model

**One migration covers all six games.** Both tables are generic; per-game shape lives
in a JSON payload, so adding game #7 is an enum member and a payload contract — no
schema change.

```csharp
// backend-civic/Models/Daily/DailyPuzzle.cs
public enum DailyGameKind { Fork, CrowdCall, PricedIn, PlaceIt, TimeMachine, WhoseValue }

public enum DailyPuzzleStatus { Draft, Approved, Live, Retired }

public class DailyPuzzle
{
    public Guid Id { get; set; }
    public DailyGameKind Kind { get; set; }

    /// <summary>The day this is the daily. Unique with (Kind, Locality).</summary>
    public DateOnly PuzzleDate { get; set; }

    /// <summary>Human-facing edition number, e.g. "Fork #142". Monotonic per Kind.</summary>
    public int Edition { get; set; }

    /// <summary>Game-specific content. Shape is defined per game spec, versioned below.</summary>
    public string PayloadJson { get; set; } = "";
    public int PayloadVersion { get; set; } = 1;

    /// <summary>2-letter state code for locality-scoped variants; null = national.</summary>
    [MaxLength(2)] public string? Locality { get; set; }

    // Provenance — which real content this puzzle was cut from.
    public Guid? SourceBillId { get; set; }
    public Guid? SourceProvisionId { get; set; }
    public Guid? SourceNewsItemId { get; set; }

    public DailyPuzzleStatus Status { get; set; } = DailyPuzzleStatus.Draft;

    /// <summary>seed | derived | manual — reuses <see cref="CivicGenerationSource"/>.</summary>
    [MaxLength(20)] public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// backend-civic/Models/Daily/DailyPuzzlePlay.cs
public class DailyPuzzlePlay
{
    public Guid Id { get; set; }
    public Guid PuzzleId { get; set; }
    public DailyPuzzle? Puzzle { get; set; }

    [Required, MaxLength(120)] public string UserId { get; set; } = "";

    /// <summary>What they answered. Shape per game spec.</summary>
    public string ResponseJson { get; set; } = "";

    /// <summary>Normalized 0..100. Games with no right answer (Fork) store 0.</summary>
    public int Score { get; set; }
    public int AttemptsUsed { get; set; }
    public bool Completed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Indexes:**
- `DailyPuzzles`: unique `(Kind, PuzzleDate, Locality)`; index `(Kind, Status, PuzzleDate)`.
- `DailyPuzzlePlays`: unique `(PuzzleId, UserId)` — one play per person per puzzle,
  which is also the idempotency guard; index `(UserId, CreatedAt)` for the archive view.

Migration name: `AddDailyPuzzles`. Register both `DbSet`s on `CivicDbContext`.

## Shared API

One controller, `backend-civic/Controllers/Api/DailyController.cs`, `[AllowAnonymous]`,
route `api/daily` (verified free — no collision with existing routes).

| Endpoint | Purpose |
|---|---|
| `GET /api/daily` | Today's slate: every live puzzle, the caller's play state for each, and the weekly ring. One round-trip powers the hub. |
| `GET /api/daily/{kind}?date=` | One puzzle. `date` omitted = today; a past date serves the archive. Never returns the answer key. |
| `POST /api/daily/{kind}/plays` | Submit. Returns result, crowd stats, XP awarded, and the share grid. |
| `GET /api/daily/{kind}/archive?take=` | Recent editions with the caller's scores, for binge play. |

**Answer-key hygiene.** The `GET` responses must strip the solution from
`PayloadJson` before serialization — every game's payload contract below marks which
fields are secret. Scoring happens server-side in `POST`. (Place It is the one case
where a determined player can read the answer from the public `/api/bills/{id}`
endpoint; see that spec for why we accept it.)

**Replay.** `POST` is rejected with `409` when a completed play already exists for
`(PuzzleId, UserId)`. The client then renders the result from the archive endpoint.

## Shared services

New, in `backend-civic/Services/Daily/`:

- **`DailyPuzzleService`** — read/serve/score. Holds the per-kind scorer dispatch.
- **`IDailyPuzzleGenerator`** — one implementation per game kind, each responsible for
  producing tomorrow's payload from its content source.
- **`DailyPuzzleGenerationService : BackgroundService`** — runs once daily, generates
  **two days ahead** for every kind so there is always a buffer if a generator fails or
  a puzzle is rejected in review. Follows the existing hosted-service pattern
  (`BillIngestionService`, `CoalitionLifecycleHostedService`), registered in `Program.cs`.

**One refactor to do first:** `QuizController.PollStatsAsync` is a private method but
Crowd Call needs the same 60-day tally. Extract it to
`backend-civic/Services/QuizPollStats.cs` and have both call it. Do not duplicate the
window logic — `QuizController.PollWindowDays` must stay the single source of truth.

## XP: one enum member, no new economy

Per the seam that already exists in `ReasoningLedger`, all six games award XP through a
single new act type:

```csharp
// CoalitionActType (Models/Coalition/CoalitionAct.cs)
DailyPuzzle,   // completed a daily game; Payload = "{kind}:{yyyy-MM-dd}"

// CoalitionPoints.BasePoints
DailyPuzzle => 3,   // reasoning currency, same tier as ReactionWithReason/ClaimTag
```

That is the entire economy change. Everything else is inherited:

- `CoalitionPoints.ApplyDiminishing` caps the day at 150 and decays each further act by
  0.8×, so playing all six games cannot out-earn real coalition work. This is what
  enforces the anti-volume-dominance rule — **do not add a per-game bonus large enough
  to defeat it.** A small accuracy bonus (`bonus: score >= 80 ? 2 : 0`) is the ceiling.
- `ReasoningLedger.LogActivityAsync` writes `CoalitionActivityDay` automatically, so
  games feed the cadence ring for free.
- `CohortService` ranks the ledger, not coalition acts specifically, so daily-game
  players appear on the cohort board with zero extra work.

**Award once per puzzle**, on first completion, gated by the unique
`(PuzzleId, UserId)` index — the same marker-row idempotency trick `GetQuestsAsync`
already uses for quest payouts.

**Critical guard:** `CurrentUserService.GetCurrentUserId()` falls through to the literal
string `"anonymous"` when neither a `sub` claim nor an `X-User-Id` header is present.
Skip the ledger write entirely when the id is `"anonymous"`, or every such visitor
shares one XP bucket and pollutes both the diminishing-returns math and the cohort
board. Anonymous players still *play* and still see results — they just don't accrue
XP until they have a device id or an account. `CohortService` already filters the
literal out on the read side; this closes the write side.

## Streak / cadence

No new table and no breakable counter. Read `CoalitionActivityDay` and render a weekly
ring — "3 of 7 days this week" — reusing the existing `CadenceDto(Score, bool[] Last7Days)`
from `CoalitionLoopService.CadenceAsync`. This honors the docs' explicit objection to
hard streaks while still giving a return signal.

## Share

Two mechanisms, both on existing plumbing.

**1. Copyable emoji grid** (the Wordle growth engine). The `POST` response includes a
`shareGrid` string built server-side so it stays identical across web and Android. Per-game
formats are in each spec. Every grid ends with `civersify.com/daily` and the edition number,
and **must never leak the answer** — Wordle's grid conveys progress, not solution.

**2. Auto-post to Bluesky.** Add `CivicDailyPuzzle = 13` to `SocialContentType`
(`shared/Arena.Shared/Social/Model/SocialPost.cs`) and a selection branch to
`CivicHighlightSelector`. Card rendering (`SkiaCardRenderer`), dedup, retry, and circuit
breaking are already built.

This directly resolves open user feedback: the current Bluesky posts are redundant
because the tweet text and the card image say the same thing, and the ask was for the
card to carry "a *would you rather* choice drawn from the coalition" as the hook. **Fork
is exactly that card.** Post the Fork question as the image, keep the existing tweet
text and link.

## Frontend

Pages under `frontend-civic/src/prototypes/magazine/pages/daily/`:

- `DailyHub.tsx` → `/daily` — today's six cards, play state, weekly ring.
- `DailyGame.tsx` → `/daily/:kind` — the player shell; per-game bodies dispatch off `kind`.

Routes register inside `MagazineLayout` in `App.tsx`, alongside the existing magazine
routes. Add `{ to: "/daily", label: "Daily" }` to **`NAV_PRIMARY`** in `nav.ts` — not to
the `Explore` dropdown. This is the funnel entrance and it needs to be one tap from
anywhere; burying it behind a dropdown defeats the purpose.

New API client: `frontend-civic/src/api/daily.ts`, matching the existing per-feature
module convention (`quiz.ts`, `bills.ts`, `cohort.ts`).

Shared components in `components/daily/`:

- `DailyCardShell.tsx` — title, edition number, timer-free framing, result state.
- `ShareGrid.tsx` — renders + copies the grid, with a "Copied." confirmation. Note the
  standing user feedback that a copy button without a confirmation reads as broken.
- `CrowdBar.tsx` — the "what everyone else did" reveal bar, reused by all six.
- `ResultReveal.tsx` — score, explanation, and the one deep link into the real product.

Two existing surfaces get a daily-game entry point:

- `components/featureCards/DailyPuzzleFeatureCard.tsx` — follow `QuizFeatureCard.tsx`
  exactly; it slots into the existing `FeatureRotator` on the home page.
- `components/shorts/DailyPuzzleShortCard.tsx` — a playable card inside `/shorts`,
  following `BudgetFactShortCard.tsx`. This also serves the open feedback that Shorts
  should drop candidate tweets in favor of "interesting facts" — a playable puzzle is
  a stronger card than a candidate post.

## Admin review

New `/admin/daily` page under the existing `AdminShell`, gated by the current `Admin`
email-allowlist policy. Lists the generated buffer with Approve / Reject / Edit.

- **Auto-approve** (pure selection from already-reviewed rows): Crowd Call, Priced In,
  Place It, Whose Value.
- **Require review**: Fork and Time Machine — both can produce a puzzle that reads as
  partisan or as a trick, and both are the most publicly shareable.

The `Draft → Approved → Live → Retired` states exist for this queue. A kind with no
approved puzzle for today simply doesn't appear on the hub; the hub must degrade
gracefully to however many games are live, never error.

## Neutrality auditing

The gamification docs commit to monthly bias audits flagging any ideology advantage
over 5%. Daily games need the same treatment, because a puzzle bank is an editorial
position whether or not anyone intends it to be. Add to the admin page a per-kind
balance report:

- Fork: distribution of which axis pole the "popular" option sits on.
- Priced In: distribution of items whose true answer is "smaller than you think" vs
  "bigger than you think" — a bank that is all *smaller* reads as advocacy.
- Time Machine: source and era balance.

## Build order

1. **Shared platform** — tables, migration, `DailyController`, `DailyPuzzleService`,
   generation host, XP enum member, hub page, share grid, admin queue.
2. **Crowd Call** (spec 02) — ships first. It needs no new content pipeline and no
   authoring: every existing quiz question is already game content. Fastest read on
   whether daily-game traffic converts at all.
3. **Fork** (spec 01) — the shareable hook and the Bluesky card fix.
4. **Place It** (spec 04) — the bridge from casual play into the compass product.
5. **Priced In** (spec 03) — highest content cost (~150 authored magnitudes).
6. **Time Machine** (spec 05) and **Whose Value** (spec 06).

Steps 2–6 are each additive: one generator, one scorer, one payload contract, one
frontend body. No further schema changes.

## Verification

Per game, and for the platform:

```bash
# backend
cd backend-civic && dotnet build
dotnet ef migrations add AddDailyPuzzles
dotnet ef database update

# run the civic API (port 5050) and the frontend
dotnet run --urls "http://localhost:5050"
cd frontend-civic && npm run dev

# frontend type-check — use build, not tsc --noEmit; CI is stricter
cd frontend-civic && npm run build
```

Test projects run individually, never via `dotnet test arena.sln` — the parallel DB
suites crash the shared Postgres container:

```bash
dotnet test backend-civic-tests
```

Platform acceptance:

- `GET /api/daily` with **no** `X-User-Id` header and no token returns a full slate and
  writes no ledger row on play.
- `GET /api/daily` with an `X-User-Id` header awards XP once; a second `POST` for the
  same puzzle returns `409` and does not double-award.
- Playing all six games in one day cannot exceed `DailyReasoningCap`; assert the sixth
  award is smaller than the first (diminishing returns applied).
- `CoalitionActivityDay` gains exactly one row for the day.
- The caller appears on `/api/cohort` having played only daily games.
- No `GET` response body contains a solution field.
- With `Anthropic:Enabled=false`, generation still produces a full slate (every game
  except Fork's rare fallback path is LLM-free by construction).
