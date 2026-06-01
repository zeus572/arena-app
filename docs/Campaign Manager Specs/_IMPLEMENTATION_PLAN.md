# Campaign Manager — Implementation Plan (authoritative)

This is the single source of truth for implementing the Campaign Manager feature in the
**Political Arena** app (`backend/` = `Arena.API`, `frontend/`). It resolves all open spec
questions and specifies exact entities, formulas, endpoints, and UI. Follow it precisely and
match existing repo conventions.

## Resolved decisions (from Campaign_Manager_DECISIONS.md + sensible defaults)
- Single-player only. Default 4-week campaigns (configurable 4–24).
- 3 preset candidate personas + matching AI opponents. No custom personas in MVP.
- Anonymous users via `ICurrentUserService.GetOrCreateUserAsync()`; campaigns scoped to `user.Id`.
- Resources: **Budget** (money), **TimeUnits** (per-week effort, refilled each week), **StaffCount** (persistent), **Momentum** (0–100, centered at 50).
- Approval is a single 0–100 number. **Win if final approval ≥ 50.**
- Debate milestones on weeks divisible by 2 (so weeks 2 & 4 of a 4-week run). Reuse the existing Debate/Turn/Agent/Vote engine.
- Debate milestones are **skippable** when `DebatesMandatory=false` (default), at a polling penalty.
- No mid-campaign hard-fail in MVP. On the final week, finalize the campaign (Completed).
- Persisted in Postgres, resumable.

## Backend

### Enums (Arena.API.Models, in Campaign.cs)
- `CampaignStatus { Active, Completed, Abandoned }`
- `CampaignDifficulty { Easy, Normal, Hard }`
- `CampaignEventType { Opportunity, Crisis, Neutral }`
- `CampaignActivityType { Advertising, TownHall, Fundraising, OppResearch, DebatePrep, Polling }`

Store enums as strings in DB via `.HasConversion<string>()` in OnModelCreating (cleaner columns; OK to introduce for new entities).

### Entities (Arena.API.Models)
`Campaign`: Id(Guid PK), UserId(Guid), CandidateName(string), PersonaId(string), Persona(string),
OpponentName(string), OpponentPersona(string), Theme(string), PlatformJson(string="{}"),
CurrentWeek(int=1), TotalWeeks(int=4), Difficulty(enum=Normal), Status(enum=Active),
Approval(double=50), Won(bool?), FinalApproval(double?), Outcome(string?),
LastResolvedDebateWeek(int=0), CreatedAt, UpdatedAt, CompletedAt(DateTime?).
Navs: Resources (1:1), Weeks (1:many), Events (1:many).

`CampaignResources`: Id, CampaignId(unique FK), Budget(double=100000), TimeUnits(int=40),
StaffCount(int=5), Momentum(double=50), UpdatedAt.

`CampaignWeek`: Id, CampaignId(FK), WeekNumber(int), ApprovalRating(double), DecisionsJson(string="[]"),
ResourceChangesJson(string="{}"), DebateId(Guid?), Summary(string=""), CreatedAt.
Unique index (CampaignId, WeekNumber).

`CampaignEvent`: Id, CampaignId(FK), WeekNumber(int), Type(enum), EventKey(string), Title(string),
Description(string), OptionsJson(string="[]"), ResponseChosen(string?), OutcomeJson(string?),
Resolved(bool=false), ResolvedAt(DateTime?), CreatedAt. Index (CampaignId, WeekNumber).

Modify `Debate`: add `public Guid? CampaignId { get; set; }` and `public int? CampaignWeek { get; set; }`. Add `HasIndex(d => d.CampaignId)` in OnModelCreating. No navigation property needed.

DbContext: add `Campaigns`, `CampaignResources`, `CampaignWeeks`, `CampaignEvents` DbSets and
configure: Campaign 1:1 Resources (cascade), 1:many Weeks (cascade), 1:many Events (cascade),
enum string conversions, index on Campaign.UserId.

### Config: CampaignTuningOptions (Arena.API.Services, bound from "CampaignTuning")
Defaults: DefaultTotalWeeks=4, MinTotalWeeks=4, MaxTotalWeeks=24, StartingBudget=100000,
StartingTimeUnits=40, StartingStaff=5, StartingMomentum=50, StartingApproval=50, WinThreshold=50,
AdvertisingApprovalPer1k=0.2, TownHallApprovalEach=2.0, TownHallTimeCost=5, FundraisingPerStaff=8000,
FundraisingTimeCost=4, OppResearchMomentum=6, OppResearchStaffTimeCost=3, DebatePrepMomentum=6,
DebatePrepTimeCost=5, PollingBudgetCost=5000, MomentumAmplification=0.004, MomentumDecay=0.85,
DifficultyPressureEasy=0.5, DifficultyPressureNormal=1.5, DifficultyPressureHard=3.0,
DebateMilestoneEveryNWeeks=2, DebatesMandatory=false, DebateSkipPenalty=4.0,
DebatePerformanceWeight=0.15, TurnsPerDebate=4, EventChancePerWeek=0.6, BaseApprovalChange=0.0.
Register: `builder.Services.Configure<CampaignTuningOptions>(builder.Configuration.GetSection("CampaignTuning"));`

### CampaignMechanics (pure static, Arena.API.Services) — fully unit-testable, no DB
- `Clamp(v,min,max)`, `ClampApproval(v)=Clamp(v,0,100)`.
- `MomentumAmplifier(momentum,t) = 1 + (momentum-50)*MomentumAmplification` (so 50→1.0, 100→1.2, 0→0.8).
- `AdvertisingApproval(spend1kDollars, momentum,t)` = (spend/1000)*AdvertisingApprovalPer1k*amp.
- `TownHallApproval(count, momentum,t)` = count*TownHallApprovalEach*amp.
- `Fundraising(staff,t)` = staff*FundraisingPerStaff.
- `DifficultyPressure(difficulty, week, t)` = base(difficulty)*(1 + week*0.1).
- `UpdateMomentum(prev, gains, t)` = Clamp(50 + (prev-50)*MomentumDecay + gains, 0, 100).
- `DebatePerformance(momentum, difficulty, week, variance, t)` → returns struct {PlayerScore, OpponentScore, Signed = clamp(player-opponent,-40,40), Won, Margin}. PlayerScore = 50 + (momentum-50)*0.5 + variance. OpponentScore = 50 + difficultyBase(Easy0,Normal8,Hard16) + week*2. variance is passed in (caller supplies; tests pass 0).
- `ComputeOutcome(finalApproval, t)` → {Won = finalApproval>=WinThreshold, FinalApproval, Outcome string}.
- `ComputeWeek(WeekInput, t) -> WeekResult` aggregating: approvalChange = BaseApprovalChange + advertising + townhall + eventEffect + debateEffect + momentumBonus - difficultyPressure; newApproval=ClampApproval(prev+change). momentumBonus = (momentum-50)*0.02. Returns new approval, momentum, and the component breakdown for the week summary.

`WeekInput` carries: prevApproval, momentum, difficulty, week, advertisingSpend, townHallCount,
eventApprovalEffect, debateApprovalEffect, extraMomentumGain. `WeekResult`: newApproval, newMomentum, components.

### CampaignEventBank (static, Arena.API.Services)
5–7 templated events. Each: EventKey, Type, Title, Description, Options[] where each option =
{Id, Label, Approval, Budget, Momentum} effects. Provide `Pick(Random, recentKeys)` and `FindOption(eventKey, optionId)`.
Examples: scandal(Crisis), endorsement(Opportunity), viral(Opportunity/Neutral), budget-shortfall(Crisis),
town-hall-invite(Opportunity), opponent-stumble(Neutral). (No LLM needed — template-based.)

### PersonaBank (static, Arena.API.Services)
3 candidate personas (key, name, persona text, theme) + matching opponent (name, persona):
e.g. "reformer" (The Reformer), "pragmatist" (The Pragmatist), "populist" (The Populist).
Expose `All` and `Get(key)`.

### CampaignService (Arena.API.Services, scoped, registered in Program.cs)
Constructor deps: `ArenaDbContext`, `ILlmService`, `IConfiguration` (to read Anthropic:ApiKey),
`IOptions<CampaignTuningOptions>`, `ILogger<CampaignService>`. Uses a private `Random`.
Methods (all async): CreateAsync(user, req), ListAsync(userId), GetDetailAsync(id, userId),
AdvanceWeekAsync(id, userId, activities), PreviewAllocationAsync(id, userId, activities),
ResolveEventAsync(id, userId, eventId, optionId), RunDebateMilestoneAsync(id, userId, skip, topic),
GetResultsAsync(id, userId). All return DTOs (in Arena.API.Models.DTOs). Ownership enforced (throw a
KeyNotFound-style result the controller maps to 404; use a small `CampaignException` with a Kind for
NotFound/Validation/Conflict, or return null + out error). Prefer: throw `CampaignNotFoundException`
and `CampaignValidationException` (custom), controller catches → 404/400.

Behavior details:
- Create: validate persona key, clamp weeks to [Min,Max]. Insert Campaign+Resources (starting values),
  set Approval=StartingApproval. Generate week-1 events (0–2 based on EventChancePerWeek). Return detail.
- Advance: only if Status==Active. Block (Conflict) if current week is a debate milestone not yet
  resolved (LastResolvedDebateWeek < CurrentWeek). Validate activity costs affordable against current
  resources (Budget/TimeUnits/StaffCount); on failure throw validation. Deduct costs, apply gains
  (fundraising adds budget; oppo/prep add momentum). Sum resolved-event approval effects for the
  current week. Compute approval via ComputeWeek. Persist a CampaignWeek snapshot
  (WeekNumber=CurrentWeek, ApprovalRating=newApproval, DecisionsJson=activities, ResourceChangesJson,
  Summary). Set campaign.Approval=newApproval, momentum=newMomentum. Refill TimeUnits to StartingTimeUnits.
  If CurrentWeek==TotalWeeks → finalize (Status=Completed, CompletedAt, FinalApproval, Won, Outcome via
  ComputeOutcome, include debate W/L record). Else CurrentWeek++ and generate next-week events.
  Return AdvanceWeekResult (updated detail + week summary + completed flag + debateMilestoneDue flag).
- PreviewAllocation: compute line-item costs + affordability, no mutation.
- ResolveEvent: find pending event by id (must belong to campaign+current week, unresolved). Apply chosen
  option effects: Budget/Momentum immediately to resources (clamped, budget≥0, momentum 0–100), and
  Approval immediately to campaign.Approval (clamped) — record in OutcomeJson. Mark Resolved.
- RunDebateMilestone: require current week is a milestone & not resolved. If skip and !DebatesMandatory:
  apply DebateSkipPenalty to approval, mark LastResolvedDebateWeek=CurrentWeek, return skipped result.
  Else: find-or-create candidate Agent (Name=$"Candidate-{campaignId:N}") and opponent Agent
  (Name=$"Opponent-{campaignId:N}") with personas. Create Debate (CampaignId, CampaignWeek=CurrentWeek,
  Proponent=candidate, Opponent=opponent, Topic = req.Topic ?? generated from theme, Status=Active,
  Format="standard", Source="bot"). Generate TurnsPerDebate turns alternating
  (candidate,opponent,...). If `Anthropic:ApiKey` is non-empty, use ILlmService.GenerateTurnAsync
  (try/catch → fallback); otherwise use templated turn text. Compute DebatePerformance (variance from
  Random in [-10,10]). Apply Signed*DebatePerformanceWeight to approval (clamped). Persist simulated
  Votes: total ~20 split by logistic of (player-opponent) margin (candidate gets the larger share when
  Won). Set Debate.Status=Completed. Mark LastResolvedDebateWeek=CurrentWeek. Link DebateId onto the
  current/last CampaignWeek if present, else store for the next snapshot. Return DebateMilestoneResult
  (debateId, won, signedEffect, summary, updated detail).
- GetResults: only if Completed (else validation→400). Return results DTO: candidateName, won,
  finalApproval, totalWeeks, debatesPlayed, debatesWon (count Debates where CampaignId==id and
  candidate votes>opponent votes), approvalTrend (per-week), outcome string.

### DTOs (Arena.API.Models.DTOs, CampaignDtos.cs) — public classes, get;set;
CreateCampaignRequest {CandidateName, PersonaId, Difficulty(enum, default Normal), TotalWeeks(int?), Theme(string?), Platform(Dictionary<string,string>?)}.
ActivityAllocationDto {Type(enum), Budget(double?), TimeUnits(int?), StaffCount(int?), Count(int?)}.
AdvanceWeekRequest {List<ActivityAllocationDto> Activities}.
AllocationPreviewResult {bool Affordable, double ProjectedBudget, int ProjectedTimeUnits, int ProjectedStaff, List<string> Issues, List<object> LineItems}.
RespondEventRequest {string OptionId}.
RunDebateRequest {bool Skip, string? Topic}.
CampaignSummaryDto, CampaignResourcesDto, CampaignWeekDto, CampaignEventDto(+options as {Id,Label}),
CampaignDetailDto (campaign + resources + currentApproval + weeks + pendingEvents + debateMilestoneDue + activeDebateId),
AdvanceWeekResult, DebateMilestoneResult, CampaignResultsDto. Map enums to strings.

### Controller: CampaignsController (Arena.API.Controllers.Api) — [ApiController], [Route("api/[controller]")]
Inject `ICurrentUserService`, `CampaignService`. NO Premium auth (anonymous single-player allowed).
- `GET personas` → static list from PersonaBank (no auth, no user needed).
- `POST ` → create. 201 CreatedAtAction(nameof(GetById)).
- `GET ` → list current user's campaigns.
- `GET {id:guid}` (name GetById) → detail.
- `POST {id:guid}/advance` → advance.
- `POST {id:guid}/allocate` → preview.
- `POST {id:guid}/events/{eventId:guid}/respond` → resolve.
- `POST {id:guid}/debate` → run/skip milestone.
- `GET {id:guid}/results` → results.
Catch CampaignNotFoundException→NotFound(new{error}), CampaignValidationException→BadRequest(new{error}),
CampaignConflictException→Conflict(new{error}).

### Program.cs registrations (add near other AddScoped/Configure calls)
`builder.Services.Configure<CampaignTuningOptions>(builder.Configuration.GetSection("CampaignTuning"));`
`builder.Services.AddScoped<CampaignService>();`
appsettings.json + appsettings.Development.json: add a `"CampaignTuning": {}` section (empty is fine; defaults apply).

### Migration
After backend compiles: `dotnet ef migrations add CampaignManager -p backend/Arena.API.csproj -s backend/Arena.API.csproj` (or run from backend/). Verify it creates the 4 tables + adds Debates.CampaignId/CampaignWeek and updates the snapshot. Build again.

## Tests (in the existing test project that references Arena.API; discover it from arena.sln)
Add EFCore.InMemory package to that test csproj if missing. Use the project's existing test framework (xUnit expected). Add:
- `CampaignMechanicsTests`: amplifier centering (50→1.0), advertising scales with spend, town hall scales with count, momentum decay toward 50, difficulty pressure ordering Easy<Normal<Hard and grows with week, ComputeOutcome threshold at 50, DebatePerformance Won when player>opponent, approval clamps 0–100.
- `CampaignServiceTests` (InMemory ArenaDbContext, stub/real ILlmService with empty key → templated turns): create campaign seeds resources+events; advance deducts/affords correctly; over-allocation rejected; resolve event applies effects; full 4-week run reaches Completed with Won set; debate milestone produces a Debate with turns+votes and moves approval; results endpoint returns debatesPlayed/Won. Provide a tiny fake ILlmService returning fixed text to avoid network.
Run `dotnet test` for that project → green.

## Frontend (frontend/, Tailwind tokens, only ui/button.tsx exists)
- `src/api/types.ts`: add Campaign* interfaces mirroring DTOs.
- `src/api/client.ts`: add async functions: listCampaigns, getCampaign, createCampaign, advanceWeek,
  previewAllocation, respondEvent, runDebate, getResults, getPersonas (use the shared `api` axios instance).
- Pages (src/pages/): `Campaigns.tsx` (list + New button), `CampaignCreate.tsx` (wizard: name, persona
  cards from getPersonas, difficulty, weeks), `CampaignDashboard.tsx` (resource panel, approval number +
  inline-SVG sparkline of week approvals, pending events with respond buttons, activity allocation form →
  advance, debate-milestone panel run/skip, week timeline, results banner when Completed).
- `src/App.tsx`: add routes `/campaigns`, `/campaigns/new`, `/campaigns/:id`.
- `src/components/navbar.tsx`: add a "Campaign" entry to NAV_LINKS (lucide icon e.g. `Megaphone`/`Flag`).
- Match existing pages' Tailwind token classes and `cn()` usage. Respect strict tsconfig: use
  `import type` for type-only imports, no unused vars. Build: `npm run build` (runs tsc -b) → green.

## Acceptance
- `dotnet build` green; `dotnet ef migrations add` clean; `dotnet test` green; `npm run build` green.
- All endpoints functional; full campaign playable to a Win/Loss result.
