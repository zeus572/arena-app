# Coalition Game — Implementation Reference

The coalition / "find the governance layer" game for Civic Arena, implemented per
`07_IMPLEMENTATION_PLAN.md` (Layers 0→3) plus the spec docs 00–06. This is the
engineering reference for the merged feature; `BUILD_LOG.md` (repo root) is the
phase-by-phase build audit with pasted test output.

## What it is
Players take **positions** on neutral, real-tradeoff **provisions**, propose **amendments**
(carve-outs), and **co-sign** the version that assembles a **cross-spectrum coalition** before
a deadline. The win condition is breadth · cost · specificity · movement — not winning a debate.
Constructed/Values-grounded **agents** seed thin rooms; the same loop serves humans and agents.

## Where it lives
- Backend: `backend-civic/Services/Coalition/**` (+ `Models/Coalition`, `Controllers/Api/CoalitionProvisionsController.cs`)
- Frontend: `frontend-civic/src/api/coalition.ts`, `src/prototypes/magazine/pages/CoalitionProvisions*.tsx`
- Tests: `backend-civic-tests/Civic.ApiTests/Coalition/**` + `Coalition*Tests.cs`

## Architecture (by layer)
| Layer | Namespace | Summary |
|---|---|---|
| 0 — data & extraction | `Models/Coalition`, `Services/Coalition` | Provision/SubQuestion/Version/Position/Amendment/AcceptanceRecord; **emergent** sub-questions (jsonb vector, no migration to add — A4); provision birth from briefings; the extraction function (text → sub-question positions, cached by text hash). |
| 1 — geometry (pure) | `Services/Coalition/Geometry` | Acceptance regions + overlap; distance-to-coalition; breadth (vs composed spectrum); movement; fork detection; multi-axis per-axis breadth. **No LLM.** |
| 2 — the loop | `Services/Coalition/Loop`, `/Agents` | Part B state machine (OPEN→CONTESTED→NEAR→{PASSED,FORKED,DIED}) driven by acts + geometry; agent `wouldSign` + act policy; synthesis + integrity gates; self-play harness. **No LLM in the loop.** |
| 2H — human | `Services/Coalition/Human` | Human acts translate to the SAME `LoopAct`s (A6); spectrum-bar surfacing with a directional CTA; broadcast-only safety invariant (no private channels). |
| 3 — curriculum | `Services/Coalition/Curriculum` | Gap-width estimation; difficulty laddering; league composition (balanced spectrum, age-banded) + breadth-favoring scoring; campaign milestones; promotion/relegation; soft cadence. |
| product wiring | `Services/Coalition/Product` | EF persistence of the loop; REST API; `/me` campaign record; leagues/standings; acts/points ledger; lifecycle scheduler. |
| LLM tier | `Services/Coalition/Judges`, `TwoFramingsService`, `AgentProfileMapper`, `ExtractionService`, `ProvisionBirthService` | Judges (governance+reasoning, Common Ground, substantive, teeth, steelman), two-framings, agent-region-from-Values, extraction, birth — all over `ILlmClient` with **heuristic fallbacks**. |

## The LLM model (A5) and the access gate
Every LLM-capable seam calls `ILlmClient` and **degrades to a heuristic fallback** when the
model is unavailable, so the product is fully functional with no key (dev) and upgrades in prod.

**Access gate (`Services/Coalition/LlmAccessPolicy.cs`) — the single chokepoint:** only an
authenticated **Premium** in-request user (JWT `plan == "Premium"`) may trigger a coalition LLM
call; anonymous/Free users get heuristics (no cost/abuse). The no-HTTP-context background
scheduler is a trusted caller (allowed). Consulted by judge / extraction / birth / agent-mapper
/ two-framings.

## Points economy (two clocks)
- **Daily reasoning XP**: low ceiling, diminishing returns within a day, capped.
- **Scarce coalition currency**: uncapped premium for bridging (co-sign a passed plank, macro acts).
- Agree-vs-amend asymmetry (bare co-sign ≈ nothing; amend = real points). Payouts on PASS
  (scarce to signers) and DIED (reasoning to participants). See `CoalitionPoints.cs`.

## API endpoints (`/api/coalition`)
| Endpoint | Access |
|---|---|
| `GET /provisions`, `GET /provisions/{id}`, `GET /me`, `GET /leagues`, `GET /provisions/{id}/framings` | public (read; framings LLM is premium-gated) |
| `POST /provisions/{id}/join \| positions \| amendments \| amendments/freeform \| acceptances \| acts`, `POST /acts` | public gameplay (LLM within them gated to premium) |
| `POST /provisions/{id}/agent-step`, `POST /coalition/seed`, `POST /coalition/leagues/compose`, `POST /coalition/birth` | **Development-only** (404 in prod; the scheduler does these automatically) |

## Data model / migrations
`AddCoalitionProvisions`, `AddExtractionCache`, `AddCoalitionParticipants`,
`AddCoalitionLeaguesAndActivity`, `AddCoalitionActs`, `AddParticipantAgeBand`. The app
auto-migrates on startup; for prod run `dotnet ef database update` against the prod DB.

## Config knobs
- `Anthropic:ApiKey` — enables the live LLM tier (else heuristics everywhere).
- Premium = JWT `plan` claim == `Premium` (minted by the debate backend).
- `CoalitionLifecycle:TickMinutes` (default 30) — scheduler cadence (resolve deadlines, top up
  from briefings, run agent ballast, promotion).
- In-code defaults (tunable): `LoopConfig`, `ForkOptions`, `CoalitionPoints` (cap/decay/weights),
  `CoalitionLifecycleService.TargetActiveProvisions`, agent ballast rounds-per-tick.
- `ASPNETCORE_ENVIRONMENT` must be `Production` in prod (the dev-only endpoint gate keys off it).

## Dev vs prod
- **Dev** (no key, anonymous): heuristic LLM fallbacks; the "Run agents" and "Seed / recompose"
  buttons appear and their endpoints work.
- **Prod**: dev-only endpoints return 404; the lifecycle scheduler auto-births provisions and
  runs agent ballast; Premium users get real LLM judgments; anon/Free stay on heuristics.

## Run locally
```
docker start arena-postgres
cd backend-civic && dotnet run --urls http://localhost:5050     # seeds demo provisions
cd frontend-civic && npm run dev                                 # http://localhost:5175 → "Coalition"
# auth (optional) is served by the debate backend on :5000
```

## Test
```
dotnet test backend-civic-tests/Civic.ApiTests/Civic.ApiTests.csproj
cd frontend-civic && npm run build   # tsc -b + vite build (type-check)
```
Coverage: pure geometry/loop/curriculum unit tests; LLM seams tested on both the live (stub)
and fallback (keyless / deny-policy) paths; HTTP integration tests for gameplay, points,
leagues, lifecycle, and the **dev-only-endpoint gate in a non-Development environment**. One
test is `Skip`-gated on a live `Anthropic:ApiKey` (extraction fidelity) — un-skip with a key.

## Deferred / follow-ups
- Multi-axis breadth is delivered as computed+tested geometry; wiring it into the live
  single-bucket loop is a data-model migration.
- Persist coalition outcomes as rows (currently re-derived on read).
- Pre-existing, unrelated: `CivicCampaignServiceTests` is excluded from the test build (outdated
  constructor) — see `BUILD_LOG.md`.
