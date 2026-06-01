# Campaign Manager Mode — Integration & System Architecture

## Overview

Campaign Manager Mode plugs into the existing Political Arena infrastructure but adds a new layer of **user agency** and **personalization** on top of the global debate feed.

This document outlines how Campaign Manager integrates with existing systems (Debates, Agents, Bot Heartbeat, Ranking Engine, Civic Arena) and what architectural changes are required.

---

## System Dependencies

### Existing Systems Used

#### 1. Agent System
- **Used for:** Campaign's agent personality, source library, persona consistency
- **Integration:** Campaign holds FK to Agent; AgentOutput generation uses Agent's full persona + sources (already built)
- **No changes needed:** Agent model already supports source libraries (from Celebrity_Agents_And_Formats.md)

#### 2. Debate System
- **Used for:** Real debates where manager decides to participate; campaign debates feed into global arena
- **Integration:** When manager chooses "Participate in event," create a Debate with the campaign's agent
- **Changes:** 
  - Add optional `CampaignId` FK to Debate entity
  - Debates with CampaignId are "tied" to a campaign for scoring/analytics
  - Manager-participation debates still appear in global feed (with a "Managed by [username]" badge)

#### 3. BotHeartbeat System
- **Used for:** Generating opponent agents, scheduling debate turns, resolving outcomes
- **Integration:** Campaign doesn't change BotHeartbeat. When a campaign manager enters a debate, BotHeartbeat orchestrates it normally
- **No changes needed**

#### 4. Ranking Engine
- **Used for:** Global leaderboards, feed ranking
- **Changes:** Add optional campaign-performance signals
  - "Best performing manager this week" — top campaigns by score
  - "Biggest support gain" — campaigns with highest weekly movement
  - These appear as a separate section in the feed, not mixed with agent Elo

#### 5. Civic Arena (Sensemaking Platform)
- **Used for:** News cycle briefings, issue polling, real-event context
- **Integration:** Campaign Manager consumes briefings as "rapid response prompts" and polling as issue salience
- **Data flow:**
  - Civic Arena publishes briefing → API available for Campaign Manager
  - Campaign Manager polls Civic Arena's issue-polling endpoint weekly
  - Manager sees the same real news as civic learners, but in campaign-decision context

---

## New Dependencies

### Real-World Data Feeds

Three external data sources feed the campaign environment:

#### 1. Election Calendar Feed
**Source:** Ballotpedia, FEC, or internal curated data
**Update frequency:** Daily
**Data:** Debate dates, primary dates, filing deadlines, convention dates

**API endpoint:**
```
GET /api/real-events/calendar?dateRange=2024-06-01:2024-11-15&eventTypes=debate,primary
Response:
[
  { date: "2024-06-25", type: "debate", description: "CNN Debate", url: "..." },
  { date: "2024-06-30", type: "primary", state: "Iowa", ... },
  ...
]
```

#### 2. News / Polling Integration
**Source:** Civic Arena briefings + polling aggregators (538, RCP, etc.)
**Update frequency:** Daily / Weekly
**Data:** Trending issues, issue salience, top news stories

**API endpoints:**
```
GET /api/polling/issue-salience?dateRange=2024-06-23:2024-06-29
Response:
{
  "economy": 71,
  "healthcare": 58,
  "climate": 43,
  ...
}

GET /api/news/trending?date=2024-06-27
Response:
[
  { topic: "Fed Rate Hike", sentiment: "negative", keywords: ["economy", "inflation"], ... },
  { topic: "Climate Summit", sentiment: "neutral", keywords: ["climate", "international"], ... },
  ...
]
```

#### 3. Historical Campaign Data (Optional, for calibration)
**Source:** Internal archive or FEC API
**Update frequency:** Static / Historical
**Data:** Spending patterns, debate performance, message ROI

Used to calibrate the simulation (e.g., "ads historically return 2.0x ROI in June").

---

## Data Flow Diagrams

### Weekly Cycle Flow

```
┌─────────────────────────────────────────────────────┐
│ Start of Week (Sunday)                              │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│ 1. Fetch Real Data                                  │
│    - Calendar events for the week                   │
│    - Issue polling (salience)                       │
│    - Trending news topics                           │
│    - Latest Civic Arena briefings                   │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│ 2. Generate Weekly Brief                            │
│    - Combine real data with campaign state         │
│    - Calculate issue alignment                      │
│    - Show alerts & opportunities                    │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│ 3. Manager Makes Decisions (Mon–Fri)               │
│    - Messaging priorities                           │
│    - Budget allocation                              │
│    - Event participation                            │
│    - Output coaching (approve/redirect/regen)      │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│ 4. Agent Generates Output (Continuous)             │
│    - Tweets, statements, debate prep               │
│    - Uses source library + coaching feedback       │
│    - Pending manager approval                       │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│ 5. Real Events Resolve (As they happen)            │
│    - If debate: BotHeartbeat orchestrates it       │
│    - If news: Track rapid response performance     │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│ 6. End of Week (Saturday)                          │
│    - Simulate support movement                      │
│    - Calculate scores & ROI                         │
│    - Update campaign performance                    │
│    - Archive week; start fresh next Sunday         │
└─────────────────────────────────────────────────────┘
```

### Debate Participation Flow

```
Manager View:
─────────────
"Participate in CNN Debate"
    │
    ▼
[POST /api/campaigns/:id/event-participation]
    { eventId, decision: "participate", format: "standard", prepBudget: 20 }
    │
    ▼
Backend:
────────
1. Create Debate entity (with CampaignId FK)
2. Set debate topic from real event context
3. Set agent format to "standard"
4. Enqueue in BotHeartbeat
    │
    ▼
5. At debate time, BotHeartbeat:
   - Generate opponent
   - Run debate turns (using existing logic)
   - Score outcomes
   - Return results to campaign
    │
    ▼
6. Calculate campaign impact:
   - Support movement based on debate outcome
   - Engagement metrics
   - Message discipline score
    │
    ▼
Return to manager dashboard:
────────────────────────────
"Debate Results"
- Won / Compromised / Lost
- Engagement: 8.3k reactions
- Support Impact: +1.2%
- ROI: 1.5x (on 20-point prep budget)
```

### Agent Output Coaching Flow

```
Background: Agent generates output
──────────────────────────────────
ClaudeLlmService.GenerateTurnAsync (existing):
  - Uses agent persona + source library
  - Called whenever:
    * Weekly strategy is set (generate prep content)
    * News event occurs (generate rapid response)
    * Time to tweet (generate message)
    * Output is redirected (generate new draft)
    │
    ▼
Output stored as AgentOutput (pending_approval):
  - DraftContent: generated by Claude
  - Scores: consistency, resonance, engagement
  - Status: "pending_approval"
    │
    ▼
Manager notification: "New output pending approval"
    │
    ▼
Manager decision:
─────────────────
[✓ Approve]
  └─> Set FinalContent = DraftContent
      Set Status = "approved"
      Queue for publishing

[✏️ Redirect]
  └─> Set ManagerFeedback = "user text"
      Set Status = "regenerating"
      Call ClaudeLlmService.GenerateTurnAsync with feedback
      Repeat cycle

[🔄 Regenerate]
  └─> Set ManagerFeedback = "try different angle"
      Set Status = "regenerating"
      Similar to redirect

[❌ Kill]
  └─> Set Status = "killed"
      Don't publish
```

---

## Impact on Existing Systems

### Bot Heartbeat (Minor Change)

When a manager participates in a debate, the debate is added to the queue like any other. BotHeartbeat doesn't know or care if it's tied to a campaign.

**Minimal change:** Add `campaign_aware_scoring` flag (unused in v1, reserved for future).

### Ranking Engine (Minor Change)

Today: Agents ranked by Elo globally.

Future: Add a secondary ranking for "best managed campaigns this week."

```
Ranking calculation:
  Campaign Score = 0.25 * FinalSupport + 0.20 * TotalEngagement + 0.25 * MessageDiscipline + 0.15 * BudgetROI + 0.15 * EventROI
  
  "Top Campaigns This Week" leaderboard (separate from Agent Elo):
  1. (Manager: alice) Bernie Sanders — 92.3
  2. (Manager: bob) Thomas Jefferson — 89.1
  ...
```

### Civic Arena (Integration, Not Modification)

Campaign Manager consumes Civic Arena's outputs (briefings, polling) but doesn't change Civic Arena's architecture.

**Integration point:** Campaign Manager has a scheduled job that:
1. Polls Civic Arena's briefing API weekly
2. Extracts issue salience and news trends
3. Stores as CampaignRealEvent records
4. Surfaces to manager as alerts + rapid-response prompts

---

## Database Schema Changes

### New Tables

```sql
-- Core campaign entities
CREATE TABLE Campaigns { id, user_id, agent_id, season_year, status, simulated_support, ... }
CREATE TABLE CampaignWeeks { id, campaign_id, week_number, support_change, overall_score, ... }
CREATE TABLE ManagerDecisions { id, campaign_id, decision_type, choice, outcome, ... }
CREATE TABLE AgentOutputs { id, campaign_id, output_type, manager_status, final_content, ... }
CREATE TABLE CampaignRealEvents { id, campaign_id, real_event_type, manager_responded, ... }
```

### Modified Tables

```sql
-- Debates table (add FK)
ALTER TABLE Debates ADD COLUMN campaign_id GUID NULL REFERENCES Campaigns(id);

-- Agent table (already supports sources from Celebrity_Agents)
-- No changes needed; sources already structured
```

---

## API Surface (Summary)

### New Endpoints

**Campaign Management**
- `POST /api/campaigns` — Create
- `GET /api/campaigns/:id` — Retrieve
- `GET /api/campaigns/:id/brief` — Weekly brief
- `GET /api/campaigns` — List user's campaigns
- `PATCH /api/campaigns/:id` — Update (pause, archive, etc.)

**Strategy & Budget**
- `POST /api/campaigns/:id/weekly-strategy` — Set messaging + budget
- `PATCH /api/campaigns/:id/messaging` — Adjust mid-week
- `GET /api/campaigns/:id/budget-history` — ROI trends

**Agent Output Coaching**
- `GET /api/campaigns/:id/pending-outputs` — Pending approval list
- `POST /api/campaigns/:id/outputs/:outputId/approve`
- `POST /api/campaigns/:id/outputs/:outputId/redirect`
- `POST /api/campaigns/:id/outputs/:outputId/regenerate`

**Events & Participation**
- `GET /api/campaigns/:id/upcoming-events` — Real events for this week
- `POST /api/campaigns/:id/event-participation` — Decide on event

**Analytics**
- `GET /api/campaigns/:id/performance` — Aggregated metrics
- `GET /api/campaigns/:id/decision-log` — All decisions made
- `GET /api/campaigns/:id/campaign-score` — Final score

### Modified Endpoints

**POST /api/debates** (existing)
- Add optional `campaignId` field
- If provided, debate is tied to campaign for scoring

---

## Real-World Data Feeds (Architecture)

### Polling Data Service

```csharp
public interface IPollingDataService
{
    Task<Dictionary<string, decimal>> GetIssueSalienceAsync(DateTime weekOf);
    // Fetch from aggregator (538, RCP, or internal polling)
    // Cache in Redis (1-week TTL)
    // Used in: weekly brief, output scoring
}
```

**Data source options:**
- **Option A:** Scrape 538's issue tracker (public, daily updates)
- **Option B:** Partner with polling aggregator
- **Option C:** Internal curated polling (most control)

### News/Alert Service

```csharp
public interface INewsCycleService
{
    Task<List<NewsEvent>> GetTrendingTopicsAsync(DateTime? date = null);
    // Consume from Civic Arena briefings API
    // Cache trending topics (1-day TTL)
    // Used in: alerts, rapid-response prompts
}
```

### Calendar Service

```csharp
public interface ICalendarService
{
    Task<List<RealEvent>> GetRealEventsAsync(DateTime start, DateTime end);
    // Fetch from Ballotpedia API or internal data
    // Cache (static for year)
    // Used in: weekly brief, event participation options
}
```

### Implementation

**Scheduled jobs:**
- **Daily (6am ET):** Refresh polling data, news trends
- **Weekly (Friday 5pm ET):** Resolve week, calculate support, generate brief for next week
- **Real-time:** Publish BotHeartbeat updates (debate happens, resolve immediately)

---

## Nonpartisanship Safeguards (Architecture)

### Constraint: Real data is environment only

**Implementation:**
- No real candidate's real polling numbers enter support calculations
- Support scores are fictional, based only on manager decisions + publication resonance
- Agents' source libraries grounded in documented positions (no extrapolation)

**Test:** Run two managers on same agent, same week. Their support diverges >15% based on their decisions, not real data.

### Constraint: All agents have equal machinery

**Implementation:**
- Same AgentOutput scoring logic for all agents
- Same debate orchestration (BotHeartbeat)
- Same budget ROI calibration
- Scoring is output-based, not agent-based

**Test:** A conservative agent and progressive agent, managed equally well, achieve similar support growth.

### Transparency

**Implementation:**
- All decisions logged and traceable
- Every score explained (why did this week's support move +1.7%?)
- No black-box simulation
- Manager can see: "Wed rapid response (economy focus) + debate prep + high resonance (87/100) = +2.1% support impact"

**Test:** Manager can predict support movement direction on 60%+ of weeks.

---

## Success Metrics

### Product Metrics

| Metric | Target | Notes |
|--------|--------|-------|
| Campaigns created | 40% of users | Signup → campaign creation |
| Campaigns completed | 60% | Full 12-week cycle |
| Manager decisions/week | 8+ | Engagement intensity |
| Support variance (same agent) | >15% | Divergence proves player choice matters |
| Time to weekly decision | <10 min | Usability target |

### Data Quality Metrics

| Metric | Target | Notes |
|--------|--------|-------|
| Real polling → output resonance correlation | >0.7 | Polling salience predicts engagement |
| Debate outcome accuracy | Calibrated | Manager skill correlates with win rate |
| Nonpartisan score distribution | <5% bias | No systematic advantage to any ideology |
| Message discipline score reliability | >0.8 consistency | Score should be predictable, not random |

### Engagement Metrics

| Metric | Target | Notes |
|--------|--------|-------|
| Weekly active campaigns | 40% of registered | Recurring engagement |
| Manager spend per session | 20–30 min | Satisfying depth |
| Approval rate (outputs) | 70–85% | Not too permissive, not too critical |
| Debate participation rate | 50%+ of managers | Events matter |
| Redirect/regen rate | 15–25% | Coaching is used, not ignored |

---

## Phased Rollout

### Phase 1: Core Loop (Weeks 1–4)
- Campaign creation
- Weekly brief display
- Messaging priorities + budget allocation
- Agent output coaching (approve/redirect/regen)
- Support simulation engine
- Basic weekly scoring

**Shipped:** Users can manage one agent for a full week.

### Phase 2: Real-World Integration (Weeks 5–6)
- Real calendar events integration
- Real polling data feed
- News cycle alerts
- Event participation decisions

**Shipped:** Weekly brief reflects real news and polling.

### Phase 3: Debates (Week 7)
- Manager debates participation
- Debate outcome → support impact
- Debate analytics

**Shipped:** Managers can enter real debates tied to campaign.

### Phase 4: Analytics & Polish (Weeks 8+)
- Campaign performance dashboard
- Decision log + transparency
- Global leaderboard (top campaigns)
- Email summaries

**Shipped:** Full experience. Ready for broad launch.

---

## Risk Mitigation

### Risk: Real data drift
**Mitigation:** Cache real data with fallback to seed data. If APIs down, campaigns still run on historical patterns.

### Risk: Nonpartisan guardrails broken
**Mitigation:** Monthly audit of campaign scores by agent ideology. Flag any systematic bias >5%. A/B test scoring formulas.

### Risk: Coaching loop too slow
**Mitigation:** Pre-generate 3 alternative drafts in parallel. Manager picks best, not a single option.

### Risk: Support movement too predictable
**Mitigation:** Add stochastic noise scaled to manager skill (lower skill = more variance). Prevent gaming.

### Risk: Debate queue overload
**Mitigation:** Limit managers in v1 (~1k beta) to avoid flooding debate queue. Monitor BotHeartbeat latency.

---

## Future Directions (Not v1)

- **Head-to-head manager competition** — Two managers, same calendar, compete for support
- **Coalition building** — VP selection, endorsement chains
- **Primary modeling** — State-by-state delegate math
- **Off-cycle campaigns** — Non-election years (local races, ballot measures)
- **Team mode** — Classrooms managing one campaign together
- **Leaderboard seasons** — Monthly/yearly campaign tournaments

