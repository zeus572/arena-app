# Civic Arena — Architecture (as built)

> **Status:** current. This describes the real, deployed implementation as of
> 2026-05-28. The other files in this folder (`DESIGN_BRIEF.md`,
> `FRONTEND_PROTOTYPE_SPEC.md`, `V0_PROMPTS.md`) are the original POC-era
> product/vision docs and predate the backend — read them for *intent*, this
> for *what exists*.

Civic Arena ("Public Lab" in the UI) is a youth-first civic-literacy app: it
turns current events into balanced briefings, teaches civic concepts, and helps
users build a nuanced, non-partisan **Civic Compass** by resolving value
tradeoffs. It is a sibling of the existing **Political Arena** debate app and
shares its login and news pipeline.

The POC was a frontend-only prototype running on mocked TypeScript data. It is
now a full stack: a .NET 8 API backed by PostgreSQL, a React frontend wired to
that API (no mocks), automated LLM content generation from live news, shared
authentication with Political Arena, and an Azure deployment.

---

## 1. Stack & repository layout

This is a monorepo containing **two** products plus a shared library.

```
arena-app/
├── backend/              Political Arena API (.NET 8, "Arena.API")   — debate app
├── frontend/             Political Arena web (React + Vite)           — debate app
├── backend-civic/        Civic Arena API (.NET 8, "Civic.API")        ← this doc
├── frontend-civic/       Civic Arena web (React 19 + Vite)            ← this doc
├── backend-civic-tests/  Civic.UnitTests + Civic.ApiTests
├── shared/Arena.Shared/  Shared netstandard library (RSS + Claude client)
├── shared/Arena.Shared.Tests/
├── infra/                Civic Azure deployment (Bicep + workflow + scripts)
└── docs/civic-app/       these docs
```

| Layer | Tech |
|---|---|
| Civic API | .NET 8 / ASP.NET Core Web API, EF Core 8, Npgsql |
| Database | PostgreSQL (Docker `arena-postgres`, port 5433 locally; shared Azure flex server in prod) |
| Civic web | React 19, TypeScript, Vite, Tailwind CSS 4, React Router 7, Axios |
| LLM | Claude — Sonnet for content generation, Haiku for cheap judge calls (via `Arena.Shared.Llm`) |
| Shared lib | `Arena.Shared` (net8.0): RSS news fetch + minimal Claude HTTP client |
| Tests | xUnit + FluentAssertions (backend), Playwright (e2e), Respawn (DB isolation) |

**Local ports:** civic API `:5050`, civic web `:5175`, debate API `:5000`,
debate web `:5173`, Postgres `:5433`.

---

## 2. Data model (`backend-civic/Models/`)

EF Core entities, one PostgreSQL database (`civic`). Owned/JSON columns noted.

**Read catalog (seeded + news-generated):**
- `Briefing` — full explainer (30s / 3-min / 10-min summaries, who acted, what
  changed, words to know `OwnsMany`, disagreement, strongest arguments, values
  in conflict, think-deeper question, related concepts). Unique `Slug`.
- `Concept` — evergreen civic concept (definition, why it matters, where you see
  it, common misunderstanding, try-it question). Unique `Slug`.
- `ThinkDeeper` — steelman-both-sides reflection companion to a briefing.
- `QuizQuestion` — MCQ with `CorrectAnswerIndex`, explanation, related concept.
- `BillTimelineStep` — the "how a bill becomes law" process diagram steps.
- `Election` — countdown targets; `ElectionScope` enum (National/State/Local),
  `ScheduledAt`, nullable `Region` for local elections.

**Provenance** (on Briefing/ThinkDeeper/Concept/QuizQuestion):
`GenerationSource` (`seed` | `news` | `manual`) + nullable `SourceNewsItemId`.
Hand-seeded rows are `seed`; news-pipeline rows are `news`.

**Values profile (per user):**
- `CivicQuestion` — onboarding question; `Type` enum (SimplePairing /
  PressureTest / ForcedTradeoff). `Choices` stored as JSON, each with
  `AxisDeltas` (which axis each answer moves and by how much).
- `CivicAnswer` — a user's answer with `Confidence` + `Intensity` enums (drive
  scoring weight) + optional reasoning. Unique per (UserId, QuestionId).
- `UserProfile` — `AxisScores` (one-to-many) + `ArchetypeBlend` (JSON).
- `ProfileAxisScore` — score per axis with supporting answer ids (traceability).
- `BudgetSession` + `BudgetAllocation` — the budget simulator.
- `ValuesReceipt` — a shareable snapshot (insights, changed axes, uncertain
  areas, `Tensions` as JSON).

**Pipeline:**
- `NewsItem` — ingested headline (ExternalId unique), `Status` enum
  (Ingested / Generating / Generated / Failed / Skipped), `LastError`,
  `AttemptCount`.

**Legacy scaffolding:** `Petition` (from the original civic backend stub).

Migrations live in `backend-civic/Migrations/` (9 as of writing, `Initial`
through `AddNewsItemsAndContentProvenance`). Seed JSON is embedded from
`backend-civic/Seed/*.json` and applied idempotently by `SeedService` on
startup.

---

## 3. API surface (`backend-civic/Controllers/Api/`)

Base URL `http://localhost:5050/api` (prod: `https://civic-api-fexzo2.azurewebsites.net/api`).
All read endpoints are `[AllowAnonymous]`; user-scoped endpoints resolve the
caller via `ICurrentUserService` (JWT `sub` → `X-User-Id` header → `anonymous`).

| Method & path | Purpose |
|---|---|
| `GET /health` | liveness + petition count |
| `GET /api/briefings` · `GET /api/briefings/{slug}` | briefing list / detail |
| `GET /api/concepts` · `GET /api/concepts/{slug}` | concept list / detail |
| `GET /api/think-deepers/{slug}` | think-deeper by slug |
| `GET /api/quiz/questions` | civics quiz |
| `GET /api/bill-timeline` | bill-becomes-law steps |
| `GET /api/elections` · `GET /api/elections/next` | upcoming elections (scope/region filters) |
| `GET /api/questions?type=&take=` | onboarding questions |
| `POST /api/answers` · `GET /api/answers/me` | submit answer / my answers |
| `GET /api/profile/me` · `POST /api/profile/me/recompute` | Civic Compass |
| `GET /api/budget/categories` · `POST /api/budget/sessions` · `GET /api/budget/sessions/me/current` · `GET /api/budget/sessions/{id}` · `PUT /api/budget/sessions/{id}/allocations` · `POST /api/budget/sessions/{id}/complete` | budget simulator |
| `POST /api/receipts` · `GET /api/receipts/me` · `GET /api/receipts/{id}` | Values Receipt |
| `GET /api/auth/me` · `POST /api/auth/link-anonymous` | identity echo / merge anon data |
| `POST /api/briefings/{slug}/debate` | **Premium only** — start a debate from a briefing |

---

## 4. Frontend (`frontend-civic/`)

The original POC shipped five visual prototypes (Bright, Dashboard, Classroom,
Diagram, Magazine). **Magazine won and is now the entire site** — the others
were removed. The app mounts at `/` (no `/magazine` prefix) and is mobile-first:
a bottom tab bar and a horizontal briefing carousel appear below the `md`
breakpoint, the editorial layout above it. Theme stays Magazine throughout.

**Routes (`src/App.tsx`):** `/`, `/briefings/:slug`, `/think-deeper/:slug`,
`/concepts/:slug`, `/onboarding`, `/profile`, `/budget`, `/receipt/:id`,
`/quizzes`, `/teachers`, `/timelines/bill`, `/login`, `/register`.

**Structure:**
- `src/api/` — Axios client + per-resource modules (`briefings`, `concepts`,
  `thinkDeepers`, `questions`, `answers`, `profile`, `budget`, `receipts`,
  `elections`, `quiz`, `billTimeline`, `debates`). `client.ts` attaches a
  `Bearer` token when logged in, else an `X-User-Id` header from localStorage.
- `src/auth/` — `AuthContext` (calls the **debate** backend's `/api/auth/*`) +
  `arenaAuthClient`.
- `src/prototypes/magazine/` — `Layout` (masthead + bottom tabs + auth strip),
  `pages/`, `components/` (CoverStory, CountdownTimer, BottomTabs, PullQuote,
  ValueChip, SharePreviewCard).

Env: `VITE_CIVIC_API_URL` (civic API) + `VITE_ARENA_API_URL` (debate API for auth).

---

## 5. Authentication & cross-app identity

**Political Arena owns identity.** It has the user store, password hashing, and
JWT issuance (`/api/auth/{register,login,refresh,logout}`). Civic Arena does
**not** duplicate any of that.

- **Shared JWT trust:** civic validates tokens with the same `Jwt:Issuer`
  (`arena-api`), `Jwt:Audience` (`arena-app`), and `Jwt:Secret` as debate. A
  token minted by debate is accepted by civic unchanged.
- **Anonymous users:** before login, the civic frontend stores a
  `crypto.randomUUID()` in `localStorage['civic-user-id']` and sends it as
  `X-User-Id`. All civic data keys on that id.
- **Linking on sign-in:** `POST /api/auth/link-anonymous` rekeys the anonymous
  user's CivicAnswers / UserProfile / BudgetSessions / ValuesReceipts onto the
  authenticated user id, so onboarding done while logged out isn't lost.
- **Civic frontend auth UI** (`/login`, `/register`) posts to the debate
  backend; the `plan` claim (`Free` / `Premium`) rides in the JWT and gates the
  premium features below.

---

## 6. News pipeline (shared ingestion → fan-out)

One RSS fetch feeds **both** apps via `Arena.Shared.News` (NPR + BBC by default,
deduped). Each app consumes it independently:

- **Debate:** `NewsTopicService` turns headlines into debate questions
  (unchanged behavior; just refactored onto the shared `RssNewsSource`).
- **Civic** (two `BackgroundService`s, both on a **2-hour** cadence):
  1. `NewsIngestionService` — polls the feed, upserts `NewsItem` rows by
     `ExternalId` (idempotent).
  2. `CivicContentGenerationService` — picks pending items (batch size + daily
     cap), then per item: Sonnet → `Briefing`, Sonnet → `ThinkDeeper`, then a
     cheap **Haiku judge** decides whether the story also warrants a `Concept`
     and/or `QuizQuestion`, generating those only when it says yes. Output is
     tagged `GenerationSource = news`. Failures retry once then mark `Failed`.

If no `Anthropic:ApiKey` is configured the services no-op gracefully.

**Premium-initiated debates:** a Premium civic user reading a briefing can hit
`POST /api/briefings/{slug}/debate`. Civic checks the `plan` claim, then proxies
to the debate backend's existing `[Authorize(Policy = "Premium")]`
`POST /api/debates` (forwarding the user's JWT) and returns the new debate URL.
Bot-driven debate generation continues unchanged. The longer-term direction is
that civic could become the primary news source.

---

## 7. Values profile scoring (`Services/`)

Rule-based per the product spec (`docs/civic_arena_values_profile_spec.md`); no
LLM in the scoring path.

- `ProfileScoringService` — weights each answer by `confidence × intensity`,
  aggregates per axis (10 axes), then computes cosine similarity against 9
  archetype vectors and a softmax blend. Axis scores carry the supporting
  answer ids for traceability.
- `ContradictionDetectionService` — flags high-confidence answers that push the
  same axis in opposite directions (surfaced as "tensions" on the receipt).
- `ReceiptService` — builds the shareable Values Receipt.
- `ExplanationService` — rule-based natural-language output today; the
  `IExplanationService` seam is reserved for an LLM implementation later.

Catalog data (axes, archetypes, budget categories, questions) is seeded from
`backend-civic/Seed/*.json`.

---

## 8. Deployment (`infra/`)

Azure, reusing the existing Political Arena resources so net-new cost is ~$0.

- **Civic API:** `civic-api-fexzo2.azurewebsites.net` — Linux App Service on the
  shared `plan-arena` (B1).
- **Civic web:** Azure Static Web App (Free), hostname
  `jolly-pebble-0e9d50810.7.azurestaticapps.net`.
- **Database:** a `civic` database on the shared `arena-pgserver` flex server.
- **Auth to Postgres:** passwordless via the App Service's managed identity
  (registered as a PG Entra admin); no DB password in config.
- **IaC:** `infra/civic.bicep` (+ `civic.parameters.json`), provisioned via
  `infra/deploy-civic.ps1`. `infra/README.md` is the runbook.
- **CI/CD:** `.github/workflows/deploy-civic.yml` builds + deploys backend
  (App Service) and frontend (SWA) on pushes to the `release` branch.
- **CORS:** owned **only** by the app's `UseCors()` middleware (reads
  `Cors:Origins`). Azure App Service *platform* CORS must stay off — running
  both makes the platform 400 the OPTIONS preflight. See `infra/README.md`.

---

## 9. Testing

| Suite | Count | What |
|---|---|---|
| `Arena.Shared.Tests` | 10 | RSS parse (fixture) + Claude client (mock HTTP) |
| `Civic.UnitTests` | 40 | scoring, mappers, prompt builders, seed JSON shape |
| `Civic.ApiTests` | 58 | controllers + generation services via `WebApplicationFactory` + Respawn against a real `civic_test` DB; fake `ILlmClient`/`INewsFeed` |
| `frontend-civic/e2e` | 30 | Playwright: read flow, onboarding, profile, budget, receipt, elections, quiz/concept/teachers/timeline, mobile layout, cross-app auth, premium debate |

E2E specs that need the debate backend (auth, premium debate) skip cleanly when
`:5000` isn't reachable.

---

## 10. Local development

```bash
# Database
docker start arena-postgres
docker exec arena-postgres psql -U postgres -c "CREATE DATABASE civic;"       # once
docker exec arena-postgres psql -U postgres -c "CREATE DATABASE civic_test;"  # once, for ApiTests

# Civic backend  (:5050)
dotnet ef database update --project backend-civic
dotnet run --project backend-civic --urls http://localhost:5050

# Civic frontend (:5175)
cd frontend-civic && npm install && npm run dev

# Debate backend (:5000) — needed for login + premium-debate flows
dotnet run --project backend --urls http://localhost:5000

# Tests
dotnet test backend-civic-tests/Civic.UnitTests
dotnet test backend-civic-tests/Civic.ApiTests
dotnet test shared/Arena.Shared.Tests
npm run test:e2e --prefix frontend-civic
```

Set `Anthropic:ApiKey` (user-secrets or env) to enable live news → content
generation; without it the app runs fine on seeded content only.

**`Anthropic:Enabled` (default `true`) — local LLM kill-switch.** Set it to `false`
in user-secrets to pause all live Claude calls (the shared `ClaudeLlmClient` then
throws `LlmException` and every Civic service falls back to its heuristic path)
*without* removing the API key — useful for stopping API spend on a dev box while
keeping the one-time-shown key intact:

```bash
dotnet user-secrets set "Anthropic:Enabled" "false" --project backend-civic   # off
dotnet user-secrets set "Anthropic:Enabled" "true"  --project backend-civic   # on
```

⚠️ Keep this flag out of committed `appsettings*.json` and prod config — it must
stay implicit-`true` everywhere except dev user-secrets, or prod could be turned
off by accident. See the root `CLAUDE.md` → Configuration for the full rationale.
