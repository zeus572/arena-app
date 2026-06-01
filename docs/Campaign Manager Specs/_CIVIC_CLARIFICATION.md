# Campaign Manager — Civic Arena Clarification (authoritative)

This note supersedes any ambiguity in the other spec files about **where** Campaign Manager
lives and **what** the player does. It reflects decisions confirmed with the product owner on
2026-06-01 and is grounded in the actual Civic Arena codebase (`backend-civic` = `Civic.API`,
`frontend-civic`).

## Core concept (corrected)

- Campaign Manager is a game mode **inside Civic Arena**, NOT the Political Arena debate app.
- The player is a **campaign manager for an EXISTING `VirtualCandidate`** (the seeded fictional
  candidates already in Civic Arena). The player does **not** create a candidate.
- The goal is to **get that candidate to win their race** — i.e. finish ahead of the **real other
  `VirtualCandidate`s in the same race** (same `Office` + `State` + `District`) by election day.
- Outcomes are fictional/simulated; real-world data only informs the *environment* (issue salience,
  news), never who is "right." Nonpartisanship is non-negotiable (see Civic_Arena_Integration doc).

## Confirmed decisions (2026-06-01)

1. **Win condition** = *Beat opponents in the race.* On election day the candidate with the highest
   simulated **support share** in the race wins. The player wins if their managed candidate is #1.
2. **Levers** = *Civic-native actions.* Each turn the player acts through real Civic mechanics:
   publish a campaign post (choose tone + issue/plank), shore up a weak value-axis, target a
   high-salience issue, respond to a news briefing. Reuses existing `CampaignPost` + LLM generation.
3. **Opponents** = *Real candidates in the race.* The other seeded `VirtualCandidate`s in the same
   race each have their own simulated support that moves over the campaign (simple AI cadence).
4. **Old work** = the Political Arena build (PR #2) is closed, branch deleted, dev DB rolled back.
   Reusable pure logic (formula engine, event bank, tuning) is salvaged for reuse.

## The key gap to design: a Support/Standing model

Civic Arena today has **no polling, vote tallies, approval, or winner** — only user↔candidate
*affinity matching*. Campaign Manager must introduce a per-campaign **support simulation** that is
local to the game (does NOT alter Civic's global candidate data):

- A **race** = the set of `VirtualCandidate`s sharing (`Office`, `State`, `District`) within the
  current `ElectionCycle`. (Use `ElectionCyclesController.Races()` grouping as the source.)
- Each campaign tracks a **support share per candidate in the race** (the managed one + opponents),
  normalized to 100%. Start from an even split (or seeded by incumbency/archetype).
- A campaign runs for **N weeks** (default tied to the cycle's `ElectionDate`; configurable).
- Each week the player takes Civic-native **actions** that produce a **support delta** for the
  managed candidate, driven by:
  - **Issue salience** (which issues matter this week — seeded from a salience table / news;
    deterministic fallback when no live data).
  - **Candidate fit**: how well the action's issue/axis matches the candidate's `PlatformPlanks` /
    `CandidateAxisScore`s (strong fit → bigger gain; off-brand → smaller or negative).
  - **Message discipline & momentum**: repetition/consistency and a momentum multiplier (salvaged
    from the existing `CampaignMechanics` momentum model).
  - **Action quality**: e.g. a published post's tone/intensity match to the issue.
- **Opponents** gain support via a simple difficulty-scaled weekly cadence (Easy/Normal/Hard),
  optionally weighted by their own fit to the week's salient issues. The managed candidate's gains
  come out of the shared 100% pool (zero-sum-ish normalization).
- **Election day**: normalize final shares; the candidate with the highest share wins. Persist the
  result and a per-week support trend for the recap.

This support model is the heart of the feature and lives **only** in the Campaign Manager tables.

## Civic-native action set (turn levers)

Map directly onto existing Civic content so the mode teaches real mechanics:

- **Publish Post** — pick a `PlatformPlank`/issue + `CampaignTone` + intensity. Generates a
  `CampaignPost` for the managed candidate via the existing `CampaignPostGenerationService`
  (LLM-guarded: templated fallback when no `Anthropic` key). Support delta from fit × salience × tone match.
- **Rapid Response** — when a news `Briefing`/`NewsItem` is "hot" this week, choose to respond
  (offense / stay disciplined / pivot). Delta depends on resonance with the candidate's strengths.
- **Shore Up Axis** — invest a turn in a weak `CandidateAxisScore` area to reduce vulnerability
  (defensive; lowers opponent gains on that axis).
- **Target Issue** — concentrate the week on a high-salience issue the candidate is strong on
  (focus bonus, but opportunity cost on other issues).

Each action costs a limited weekly **action budget** (small integer, e.g. 2–3 actions/week) rather
than the abstract money/staff resources from the old build (those are out per decision #2; a light
"campaign energy" budget is fine if needed for pacing).

## Data model (Civic.API, new tables only)

All names provisional; finalize during planning. Enums stored as strings (Civic convention,
`HasConversion<string>()`). EF-scaffolded migration with snapshot (Civic convention).

- `CivicCampaign` — Id, UserId (string; Civic uses string user ids), CandidateId (FK
  `VirtualCandidate`), ElectionCycleId (FK), RaceKey (office/state/district), Difficulty,
  TotalWeeks, CurrentWeek, Status (Active/Completed/Abandoned), Won (bool?), CreatedAt/UpdatedAt.
- `CivicCampaignStanding` — Id, CampaignId, CandidateId, IsPlayer (bool), SupportShare (double),
  updated each week. One row per candidate in the race.
- `CivicCampaignWeek` — Id, CampaignId, WeekNumber, PlayerSupportAfter (double),
  SalientIssuesJson, ActionsJson, DeltaBreakdownJson, Summary, CreatedAt.
- `CivicCampaignAction` (optional, or embedded in week) — Id, CampaignId, WeekNumber, ActionType,
  Issue/Axis, Tone, ResultJson, GeneratedPostId (FK `CampaignPost`, nullable).
- (Reuse) link generated posts back via `CampaignPost` (the candidate already owns posts).

No changes to existing Civic tables beyond additive FKs; do **not** alter global candidate data.

## API (Civic.API, `/api/...`, anonymous-friendly per Civic conventions)

- `GET  /api/campaign-manager/races` — current cycle's races (office/state/district) with candidates.
- `GET  /api/campaign-manager/races/{raceKey}/candidates` — selectable candidates for a race.
- `POST /api/campaign-manager/campaigns` — start: { candidateId, difficulty, totalWeeks? }.
- `GET  /api/campaign-manager/campaigns` — list my campaigns.
- `GET  /api/campaign-manager/campaigns/{id}` — detail: standings, current week, salient issues,
  available actions, history.
- `POST /api/campaign-manager/campaigns/{id}/actions` — take a weekly action (may generate a post).
- `POST /api/campaign-manager/campaigns/{id}/advance` — end the week: resolve opponent moves,
  recompute standings, advance/Finalize.
- `GET  /api/campaign-manager/campaigns/{id}/results` — final standings, trend, win/loss recap.

Current user via Civic's `ICurrentUserService.GetCurrentUserId()` (JWT sub / X-User-Id / anonymous).
Ownership enforced (cross-user → 404).

## Frontend (frontend-civic, magazine prototype conventions)

New pages under the magazine layout + a nav entry:
- **Race/Candidate picker** — browse current-cycle races, pick a candidate to manage (reuse
  `CandidateAvatar`, value chips).
- **Campaign dashboard** — race standings (player vs. real opponents) with a support-trend chart,
  this week's salient issues, the Civic-native action panel, generated-post preview, advance-week.
- **Results recap** — final standings, support trend, decision log, win/loss.

Match Civic styling (Tailwind + CSS custom props `--fg-soft/--border/--accent/--muted`, lucide,
Radix tabs). API base `VITE_CIVIC_API_URL` (default `http://localhost:5050/api`).

## Tests (backend-civic-tests)

- `Civic.UnitTests` — support-model formulas (fit × salience, normalization to 100%, opponent
  cadence by difficulty, win determination = max share), pure and deterministic.
- `Civic.ApiTests` — WebApplicationFactory + Respawn: start campaign → take actions → advance to
  election day → managed candidate can win/lose; ownership 404; LLM stubbed (no network).

## Reuse from the salvaged Political Arena build

Salvaged to `C:\Users\samee\campaign-salvage\` (outside the repo):
- `CampaignMechanics.cs` — momentum/clamp/decay/outcome helpers (adapt: replace approval-vs-threshold
  with support-share-vs-field; keep momentum + clamping utilities).
- `CampaignEventBank.cs` — templated event/news-response options (adapt to Civic briefings).
- `CampaignTuningOptions.cs` — tunable knobs pattern (re-theme to support model).

## Out of scope (MVP)

- Multiplayer; create-your-own candidate; real money/staff resource economy; cross-posting results
  to Political Arena; live real-world calendar sync (use seeded salience + existing briefings).
