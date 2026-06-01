# Campaign Manager — Implementation Reference

How the Campaign Manager game mode is actually built in **Civic Arena**
(`backend-civic` = `Civic.API`, `frontend-civic`). This documents the shipped
implementation; the design rationale lives in the sibling `_CIVIC_*` spec notes.

## Concept

A single-player game where the user acts as **campaign manager for an existing
`VirtualCandidate`** and tries to make them finish first in their race by
election day. The candidate, opponents, and races are the real seeded Civic
data; the game adds a per-campaign **support simulation** that is local to the
campaign and never mutates the global candidate catalog.

- **Race** = the seeded `VirtualCandidate`s sharing `(Office, State, District)`.
  The seed has one President race with 5 candidates.
- **Duration** is tied to the next upcoming national `Election` (the one the
  home-page countdown shows — currently 2026-11-03). Turns are **daily, 2
  actions/day**, ending on election day.
- **Win** = highest simulated support share in the race on election day.

## Backend

### Data model (`backend-civic/Models/CivicCampaign.cs`)

- `CivicCampaign` — owner `UserId` (string), `CandidateId`, `ElectionId` +
  `ElectionName`/`ElectionDate`, `RaceKey`/`RaceLabel`, `Difficulty`,
  `CurrentDay`/`TotalDays`, `Status`, `ActionsRemaining`, final
  `Won`/`FinalSupport`/`Outcome`.
- `CivicCampaignStanding` — one row per candidate in the race: `SupportShare`
  (0–100), `Momentum` (0–100, centered 50), `IsPlayer`.
- `CivicCampaignWeek` — a per-day snapshot (`DayNumber`, `PlayerSupportAfter`,
  salient issues, standings JSON, summary) powering the trend chart + recap.
- `CivicCampaignAction` — one action taken on a day (`ActionType`, `Target`,
  `RespondedBriefingSlug`, `Tone`, `SupportDelta`, `GeneratedPostId`).
- `CandidateNewsResponse` — cached per-`(CandidateId, BriefingSlug)` response
  options (the LLM/templated options JSON), reused across views/players.
- `CampaignPost` (existing) gained `OwnerUserId` + `CampaignId` and a widened
  `Body` (160 → 2000) so a manager's response posts are attributable and longer.

Enums are stored as strings (`HasConversion<string>()`), per Civic convention.
DbSets + relationships are in `backend-civic/Data/CivicDbContext.cs`.

### Support simulation (pure, `Services/Campaign/`)

- `CivicSupportModel` — DB-free, deterministic formulas: momentum amplifier
  (1 + (m−50)·k), momentum decay toward 50, `ActionPoints` (= base × fit ×
  salience × momentum × per-action multiplier), `OpponentDelta` (difficulty-scaled
  daily drift), `ApplyAndNormalize` (renormalize the field to 100), `WinnerIndex`.
- `CivicCampaignFit` — candidate↔issue fit from platform-plank / source tag
  overlap (owned issue → positive, off-brand → negative).
- `CivicSalience` — deterministic per-day salient issues drawn from the race's
  issue pool (the seam where a live polling/news salience feed would plug in).
- `CivicCampaignOptions` — all tunables (bound from config section
  `CivicCampaign`): `ActionsPerDay`, `NewsItemsToOffer`, `ResponseMaxChars`,
  drift/momentum/fit weights, difficulty curves, `MaxCampaignDays`.

All randomness (opponent variance) is injected so the model is unit-testable;
tests pass variance 0.

### Orchestration (`Services/Campaign/CivicCampaignService.cs`, scoped)

- `CreateAsync` — snaps to the next national `Election`, computes `TotalDays`,
  seeds even standings (+ small incumbency bump), persists.
- `GetDetailAsync` — standings, salient issues, the **news items** to respond to
  (with options), secondary action options, today's actions, day history.
- `TakeActionAsync` — `RespondToNews` (primary) computes a support delta and
  publishes a `CampaignPost` from the chosen option; `TargetIssue`/`ShoreUpAxis`
  are secondary "budgeting tools". Spends one action.
- `AdvanceDayAsync` — applies the day's player delta + opponent drift,
  renormalizes, snapshots the day, refills actions, finalizes on election day.
- `GetNewsResponsePageAsync` — the response-page payload: candidate profile +
  value axes + each option's full post body/tone.
- `GetResultsAsync` — final standings, rank, support trend, outcome.

### News-response generation (lazy + cached, LLM-guarded)

`GetOrCreateResponseOptionsAsync` returns cached `CandidateNewsResponse` options
or generates 2–3 via `ILlmClient.GenerateStructuredAsync<GeneratedNewsResponsesDto>`
(prompt in `NewsResponseGeneration.cs`, asking for longer, pointed, distinct
stances up to `ResponseMaxChars`). `ILlmClient` throws `LlmException` when no
Anthropic key is configured, so a deterministic **templated fallback** (3
multi-sentence stances) keeps the game fully playable offline. Options are then
cached for reuse.

### API (`Controllers/Api/CampaignManagerController.cs`, `/api/campaign-manager`)

`[Authorize]` on the controller — all campaign endpoints require a signed-in
user; `GET /races` is `[AllowAnonymous]` (the teaser). `RequireUserId()` asserts
a real user id (never the anonymous fallback) as defense-in-depth.

- `GET  /races` — current race(s) + candidates (public teaser).
- `POST /campaigns` — create (201). `GET /campaigns` — my campaigns.
- `GET  /campaigns/{id}` — detail.
- `GET  /campaigns/{id}/news/{briefingSlug}` — response-page data.
- `POST /campaigns/{id}/actions` — take an action (respond to news / budgeting tool).
- `POST /campaigns/{id}/advance` — advance one day.
- `GET  /campaigns/{id}/results` — final recap.

Auth maps to: anonymous → 401, not-found/cross-user → 404, invalid → 400,
state conflict → 409.

### Auth & per-user data

Civic validates JWTs minted by the **debate backend** (`Arena.API`) — shared
`Jwt:Secret`/`Issuer`/`Audience`. `ICurrentUserService.GetCurrentUserId()`
resolves the JWT `sub`. Every campaign row and every published `CampaignPost`
(`OwnerUserId`) is scoped to that user. Feeds are **tailored**: the global feed,
candidate feed, and `me/campaign-feed` return public/bot posts (null owner) plus
the caller's own responses — so each user sees their own campaign decisions woven
into the candidate feed and never another user's.

## Frontend (`frontend-civic`, magazine prototype)

- API client: `src/api/campaignManager.ts` (types + endpoint functions).
- Pages (`src/prototypes/magazine/pages/`):
  - `Campaigns.tsx` — list of my campaigns; signed-out shows the candidate roster
    (public `races` teaser) first, then a `SignInPrompt`.
  - `CampaignCreate.tsx` — race/candidate picker (with a lazy-loaded profile
    expander); signed-out can browse, the start button becomes a `SignInPrompt`.
  - `CampaignDashboard.tsx` — standings, an inline-SVG support sparkline, the
    "In the news" panel (each item links to the response page), secondary tools,
    advance-day, day log, results banner.
  - `CampaignNewsResponse.tsx` — the response page: candidate profile + value
    bars, the story, and each option's full post text/tone with a "Post this
    response" button.
- `components/SignInPrompt.tsx` — reusable teaser/CTA linking to `/login` and
  `/register` with a redirect back.
- Routes added in `src/App.tsx`; nav entry in `components/BottomTabs.tsx`.

## Migrations

EF-scaffolded (run `dotnet ef migrations add` from `backend-civic/`). Applied at
startup via `db.Database.MigrateAsync()`. Campaign migrations:
`AddCampaignManager` (core tables), `AddCampaignPostOwner` (owner + feed
tailoring), `WidenCampaignPostBody` (longer responses).

## Tests (`backend-civic-tests/`)

- `Civic.UnitTests/CivicSupportModelTests.cs` — formula invariants (momentum
  centering/decay, fit/salience scaling, difficulty ordering, normalization,
  winner).
- `Civic.ApiTests/CivicCampaignServiceTests.cs` — full campaign lifecycle, news
  responses + option bodies, tailored feed (owner sees own post, stranger
  doesn't), longer-than-tweet responses, and HTTP auth gating (anonymous → 401,
  races teaser public, authed create → 201). Uses the real `civic_test` DB +
  `StubLlmClient` (templated fallback, no network).

## Config & deploy notes

- Backend config section `CivicCampaign` overrides `CivicCampaignOptions`
  (optional; defaults are sensible). LLM uses the shared `Anthropic` section —
  empty key → templated fallbacks everywhere (no network).
- **Prod prerequisites for sign-in to work**: Civic must have `Jwt:Secret`
  matching the debate backend, the debate backend (token issuer) must be
  deployed, and the civic frontend build needs `VITE_ARENA_API_URL` pointing at
  the debate API. The races teaser and offline templated content work without
  these; only authenticated actions require them.
- Deploys: push to the `release` branch triggers `.github/workflows/deploy-civic.yml`
  (backend → Azure App Service, frontend → Azure Static Web Apps). Migrations
  auto-apply on backend startup.
