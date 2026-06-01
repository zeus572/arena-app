# Campaign Manager Mode — Product Requirements Document

## Vision

Transform Political Arena from a spectator platform into an active strategy experience. Users become campaign managers for a single AI agent, making real decisions about messaging, budget, and strategy that unfold against the real election calendar and issue landscape. The real election is the *environment and clock*, not the win condition.

## Core Promise

**Synchronize with reality, control the fiction.**

Users experience the 2024/2028 campaign in real time — real debate dates, real news, real issue polling. But their agent's success is determined entirely by management skill, not by who wins in the real world.

## Product Goals

### Primary

1. Give users agency and narrative ownership over one agent's campaign journey.
2. Make the product feel *live and consequential* by syncing to the real election cycle.
3. Teach campaign strategy, message discipline, and tradeoff-making through active decision-making.
4. Create a personalized experience unique to each user (vs. the global debate feed everyone watches).
5. Maintain strict nonpartisanship: real data informs the environment; outcomes stay fictional.

### Secondary

1. Deepen engagement with agents through sustained, intimate interaction.
2. Reuse and extend existing systems (debates, budgets, agents, rankings).
3. Create natural integration points with civic learning content (real news → briefings → manager moments).
4. Build a foundation for future modes (non-election years, primary scheduling, convention play, etc.).

## Not In Scope (v1)

- Head-to-head manager vs. manager competition
- Real fundraising or money mechanics
- Non-election-year campaign modes
- Primary scheduling (delegate math, state strategies)
- Coalition building or VP selection
- Ballot access or eligibility simulation

These are rich areas for later iterations.

---

## Core Systems

### 1. The Weekly Campaign Cycle

Each week (Sunday–Saturday) the manager completes this loop:

1. **Brief** — Review the week's real calendar, trending issues, current support.
2. **Strategy** — Set messaging priorities (1–2 issues to emphasize).
3. **Budget** — Allocate limited resources across channels.
4. **Coach** — Review agent output; approve, redirect, or regenerate.
5. **Moments** — Choose whether to enter real events (debates, Town Halls, Tweet Battles).
6. **Resolve** — See results and updated standings.

**Duration:** ~15–30 minutes per cycle, designed for repeated weekly engagement.

### 2. Real-World Inputs (The Environment)

Three data streams drive the campaign environment:

#### 2.1 Calendar Events

Real, scheduled political events become live manager moments:

- **Debate nights** (real scheduled debates)
- **Primary results** (real primary dates)
- **Polling releases** (real polls, aggregated)
- **Convention dates**
- **Election night**

Each event appears as a countdown with context: *"Real debate: June 27. Topics expected: economy, immigration, climate. Your agent has 48h to prep."*

The manager decides:
- Does your candidate participate? (Yes/No)
- If yes, which debate format (Town Hall, standard Argument, Roast)?
- What prep priority (high/medium/low budget)?

#### 2.2 News Cycle / Rapid Response

Real trending political stories become rapid-response prompts:

Example: *"Breaking: Fed holds rates. Market down 2%. The economy is dominating headlines. Does your candidate respond?"*

Options:
- **Go on offense** — Pick a related issue (e.g., job creation), draft a strong statement
- **Stay disciplined** — Stick to pre-planned message, acknowledge but don't pivot
- **Pivot** — Shift emphasis to a different issue you think matters more

Scoring: Response speed, consistency with agent's source library, alignment with electorate priorities.

#### 2.3 Issue Polling

Real aggregate polling on *issue salience* (not horse-race preferences) defines the simulated electorate's weights:

Real data: "71% of voters prioritize the economy" → Simulated electorate: economy moves from 2x to 3x weight in support calculations.

Real data: "Climate has dropped 5 points in salience" → Your climate messaging this week earns less engagement.

**Crucial:** Polling tells you *what matters*, never *what's right*. A conservative and progressive manager can both win on the same issues, depending on how they message them.

---

## User Experience: Global vs. Local Views

The platform splits into two complementary views:

### Global View (Existing)

What everyone sees:

- **Feed** — Latest debates, trending agents, community reactions
- **Leaderboard** — Top agents by Elo, engagement, format mastery
- **Browse debates** — Spectate any argument, vote, comment

This remains unchanged. It's the shared arena.

### Local View (New)

What only *you* see — your campaign's private war room:

- **Dashboard** — Your agent's name, current support, key metrics
- **Weekly brief** — Calendar, polling, agenda, budget, alerts
- **Coach panel** — Draft output from your agent; approve/redirect/regenerate
- **Budget allocator** — Distribute resources across channels
- **Performance tracker** — Support over time, ROI by week, event outcomes
- **Campaign log** — All decisions and outcomes, annotated

This is *your agent's story*, not a global leaderboard. You're building a narrative arc specific to your management choices.

**Key design principle:** A user who manages Bernie sanders for 12 weeks has a completely different experience from a user who manages Thomas Jefferson. Their timelines diverge. Their agents respond to their coaching. They see different moments and make different bets.

---

## Data Model

### New Entities

#### Campaign

Represents a user's management of a single agent across a season.

```
Campaign {
  Id: Guid
  UserId: Guid (FK → User)
  AgentId: Guid (FK → Agent)
  SeasonYear: int                          -- 2024, 2028, etc.
  StartDate: DateTime
  EndDate: DateTime?                       -- Election night
  Status: string                           -- "active" | "archived" | "paused"
  
  // Campaign state
  SimulatedSupport: decimal                -- 0.0–100.0, user's electorate
  MessagePriority: string[]                -- Current 1–2 focus areas
  BudgetRemaining: decimal                 -- Points for current week
  BudgetSpent: decimal                     -- Lifetime
  
  // Coaching history
  TotalApprovals: int
  TotalRedirects: int
  MessageDisciplineScore: decimal          -- Consistency with agent sources
  
  // Metadata
  CampaignName: string?                    -- "Bernie's Revolution", "Hamilton's Gambit"
  Notes: string?                           -- User's narrative notes
  
  CreatedAt: DateTime
  UpdatedAt: DateTime
}
```

#### CampaignWeek

Represents one week in the campaign (Sun–Sat).

```
CampaignWeek {
  Id: Guid
  CampaignId: Guid (FK → Campaign)
  WeekNumber: int
  StartDate: DateTime
  EndDate: DateTime
  
  // Weekly state
  InitialSupport: decimal
  FinalSupport: decimal
  SupportChange: decimal
  
  // Manager decisions
  MessagingPriorities: string[]
  BudgetAllocation: BudgetAllocation (nested object)
  EventsParticipated: Guid[]               -- ForeignKey to Debates
  
  // Scoring
  EngagementScore: decimal
  MessageDisciplineScore: decimal
  EventROI: decimal
  ResponsiveScore: decimal
  OverallScore: decimal
  
  CreatedAt: DateTime
}
```

#### BudgetAllocation (nested in CampaignWeek)

```
BudgetAllocation {
  TotalPoints: int                         -- 100 per week
  Ads: int
  GroundGame: int
  Digital: int
  Prep: int
  Opposition: int
  
  // Constraints satisfied
  IsBalanced: bool                         -- No single category > 60%
  IsLegal: bool                            -- Total <= 100
}
```

#### ManagerDecision

Every choice the user makes is recorded for transparency and learning.

```
ManagerDecision {
  Id: Guid
  CampaignWeekId: Guid (FK → CampaignWeek)
  DecisionType: string                     -- "messaging_priority" | "budget" | "event_participation" | "output_coaching"
  Choice: string                           -- What they picked
  Rationale: string?                       -- Why (user-provided notes)
  Outcome: string?                         -- What happened
  CreatedAt: DateTime
}
```

#### AgentOutput

Every piece of content the agent generates within a campaign context.

```
AgentOutput {
  Id: Guid
  CampaignId: Guid (FK → Campaign)
  CampaignWeekId: Guid (FK → CampaignWeek)
  OutputType: string                       -- "tweet" | "statement" | "debate_prep" | "rapid_response"
  Topic: string                            -- The news/moment that triggered it
  DraftContent: string
  
  // Manager coaching
  ManagerStatus: string                    -- "pending" | "approved" | "redirected" | "regenerated"
  ManagerFeedback: string?
  FinalContent: string?                    -- Post-coaching version
  
  // Scoring
  ConsistencyWithSources: decimal          -- How well it cites/stays true to agent sources
  ElectorateResonance: decimal             -- How well it aligns with polling priorities
  EngagementPrediction: decimal            -- Estimated reactions
  
  ApprovedAt: DateTime?
  PublishedAt: DateTime?                   -- When it went live (if it did)
}
```

#### CampaignRealEvent

Links real-world events to campaign moments.

```
CampaignRealEvent {
  Id: Guid
  CampaignId: Guid (FK → Campaign)
  RealEventType: string                    -- "debate" | "primary" | "news_trend" | "poll_release"
  RealEventDate: DateTime
  Description: string
  
  // Manager engagement
  ManagerResponded: bool
  ManagerChoice: string?                   -- "participate" | "skip" | "pivot"
  DebateId: Guid?                          -- If manager entered a debate
  
  // Outcome
  OutcomeDescription: string?
  SupportImpact: decimal?                  -- +/- points
  
  CreatedAt: DateTime
}
```

---

## Weekly Campaign Flow (Detailed UX)

### Sunday Evening: The Brief

The manager logs in. They see:

**Week 24 Brief — June 23–29**

| Section | Content |
|---------|---------|
| **Agent** | Bernie Sanders (Your Campaign) |
| **Current Support** | 34.2% (↑ 1.8% from last week) |
| **Message Discipline** | 89/100 |
| **Budget Available** | 100 points |
| **Calendar** | Tuesday: Real CNN debate (8pm ET). Friday: Fed interest rate decision expected. |
| **Trending Issues** | Economy (↑ +8 points salience), Climate (↓ -3 points), Immigration (stable). |
| **Last Week's ROI** | Ads: 2.1x, Ground Game: 1.3x, Digital: 0.8x |
| **Agent Alert** | "Recent Trump statement on tariffs. Real news. Rapid response prep?" |

### Monday–Wednesday: Strategy & Budget

**Set Messaging Priorities** — A decision tree:

> Which issues should you emphasize this week? (Pick up to 2)
>
> - Economy (trending +8 points, strong for your agent)
> - Healthcare (stable, but differentiates you)
> - Climate (trending down, risky focus)
> - Immigration (neutral)

Example choice: Economy + Healthcare.

**Allocate Budget** — Reuse Budget Simulator:

> Distribute 100 points. No category > 60%.
>
> | Channel | Allocation | ROI Last Week |
> |---------|:----------:|:-------------:|
> | Ads | ___ | 2.1x |
> | Ground Game | ___ | 1.3x |
> | Digital | ___ | 0.8x |
> | Debate Prep | ___ | — |
> | Opposition Research | ___ | — |

Example allocation: Ads 40 | Ground 25 | Digital 10 | Prep 20 | Opposition 5.

**Constraint triggering:**

> You allocated 40 to ads. That's 40% of your budget. That's fine, but ads alone won't move support. Would you like to rebalance?

### Wednesday–Thursday: Agent Output Coaching

The agent generates content daily. The manager reviews:

**Your Agent's Draft Statement:**

> *Topic: Economy (from trending news)*
>
> *Status: Pending approval*
>
> *"The Fed's interest rate hikes are crushing working families. We need to rebuild an economy that works for everyone, not just the billionaire class. Here's my plan..."*
>
> **Consistency with Sources:** 92/100 — Strong cite to Sanders' economic writings. ✓
>
> **Electorate Resonance:** 87/100 — Aligns with polling on economy salience. ✓
>
> **Engagement Prediction:** 3.2k reactions
>
> **Your options:**
>
> - ✓ **Approve** — Publish as-is
> - ✏️ **Redirect** — "Focus more on housing costs, less on billionaires"
> - 🔄 **Regenerate** — Start over with different angle
> - ❌ **Kill** — Don't publish

Manager choice: Redirect.

*Feedback: "Strong on economics, but add specific housing policy. We need to differentiate vs. Warren."*

**Agent regenerates**, manager re-reviews. (Cycle 1-2 times per day as needed.)

### Thursday: Event Participation Decision

**Real CNN Debate is Tuesday. Commit now?**

> The real Democratic debate airs Tuesday at 8pm ET. Your candidate has been invited.
>
> Real debate topics (expected): Economy, climate, healthcare, immigration.
>
> **Your messaging priorities align with:** Economy (✓✓), Healthcare (✓✓).
>
> **Decision:**
>
> - **Participate** — Enter as a standard debate argument with debate prep budget (20 pts recommended)
> - **Skip** — Stay focused on earned media; save budget for digital
> - **Partial** — Send a rapid-response statement instead; don't participate live

Manager choice: Participate. Allocate 20 prep points.

The system then schedules a simulated debate turn for Tuesday evening, using the real debate as the topic anchor.

### Saturday: Resolve & Results

**Week 24 Results**

| Metric | Result | vs. Last Week |
|--------|--------|:-------------:|
| Support | 35.9% | ↑ 1.7% |
| Engagement | 18.4k reactions | ↑ 12% |
| Message Discipline | 91/100 | ↑ 2pts |
| Budget ROI | 1.94x | ↑ 0.23x |

**Breakdown by decision:**

| Decision | Outcome |
|----------|---------|
| **Economy messaging** | +2.1% support (polling priority aligned) |
| **Debate participation** | +1.2% support, 8.3k engagement |
| **Healthcare redirect** | Engagement neutral (good discipline, minimal traction) |
| **Digital underinvest** | -0.6% (low reach; consider rebalancing) |

**Next week preview:**

> Polling has shifted. Immigration salience is now +4. Your agent's source library has strong positions on immigration — opportunity to lead.

---

## Personalization: How Campaigns Diverge

Two managers, same agent, same calendar — *completely different experience.*

### Example: Bernie Sanders

**Manager A** (Defensive Strategy):
- Week 1–4: Focuses on building rural support (high Ground Game budget)
- Week 5–8: Emphasizes economic messaging (top messaging priority)
- Week 9–12: Avoids climate pivots; stays disciplined
- **Outcome:** Support climbs to 38%, message discipline 94/100, loses digital voters but wins on consistency

**Manager B** (Aggressive Pivot):
- Week 1–4: Heavy digital spend; hits trending social issues aggressively
- Week 5–8: Pivots to climate after polling surge
- Week 9–12: Takes high-risk roast battles; engagement spikes but discipline drops
- **Outcome:** Support peaks at 41% mid-cycle, crashes to 32% at end; message discipline 71/100, but 40% more engagement overall

Same agent. Same real calendar. Completely different campaigns, completely different arcs. This is what makes it personal and replayable.

---

## Scoring & Standouts

### Campaign Performance Score

Extend your Ranking Engine. Weekly and cumulative scoring:

```
WeeklyScore = 
  (SupportChange × 0.3) +
  (EngagementMetrics × 0.2) +
  (MessageDiscipline × 0.2) +
  (BudgetROI × 0.15) +
  (ResponsivenessBonus × 0.15)
```

**Cumulative Campaign Score** (at season end):
```
CampaignScore = 
  (FinalSupport × 0.25) +
  (TotalEngagement × 0.20) +
  (AvgMessageDiscipline × 0.25) +
  (BudgetEfficiency × 0.15) +
  (EventROI × 0.15)
```

### Standout Moments (for global feed)

Not all manager moments are personal. Some are *interesting enough to surface globally*:

- **Underdog surge** — "Manager took Bernie from 18% to 36% in 6 weeks"
- **Message discipline** — "Held 96/100 discipline through 8-week cycle"
- **Budget mastery** — "Achieved 2.8x ROI on $100 budget"
- **Event performance** — "Won a high-stakes debate against real odds"

These appear in the global feed with a badge: *"Manager: [username]"* — connecting the global arena to local narratives.

---

## Nonpartisanship Guardrails

1. **Real data is environment-only.** Polling tells you what's salient, never what's correct or winning.
2. **Win conditions are fictional.** No agent's sim outcome correlates with real-election outcomes.
3. **All agents have equal machinery.** A left agent, right agent, and centrist agent all use the same coaching, budget, and scoring systems.
4. **No "correct" messaging.** Both a progressive and conservative manager can win with the same electorate by making different choices.
5. **Transparency.** Every decision is logged. Every score is explainable. A manager can see why their budget allocation led to their outcome.

---

## Phased Launch

### Phase 1: Core Loop (Weeks 1–4)
- Campaign entity, weekly cycle, budget simulator
- Agent output coaching (approve/redirect/regenerate)
- Real calendar integration (basic)
- Weekly scoring

### Phase 2: Real-World Inputs (Weeks 5–6)
- News cycle rapid-response prompts
- Issue polling data integration
- Support movement tied to real polling salience

### Phase 3: Global/Local Split (Weeks 7–8)
- Separate campaign dashboard from global feed
- Standout moment surfacing
- Campaign log and decision history

### Phase 4: Polish & Analytics (Week 9+)
- Campaign performance comparisons (anonymized)
- "Lessons learned" post-mortems
- Archive and replay past campaigns

---

## Success Metrics

1. **Engagement:** Weekly active campaigns (target: 40%+ of registered users run one campaign)
2. **Retention:** Campaign completion (target: 60% complete a full 12-week cycle)
3. **Depth:** Manager decisions per week (target: 8+ decisions per manager per week)
4. **Learning:** Pre- and post-campaign self-reported understanding of campaign strategy
5. **Personalization:** Support trajectories diverge meaningfully (target: managers of same agent show >15% variance in final support)
6. **Nonpartisanship:** Equal performance distribution across ideological agent matchups (monitor for systematic bias)

---

## Out of Scope (v1)

- **Multiplayer/head-to-head:** Manager vs. manager competition
- **Non-election years:** Off-cycle modes
- **Primary modeling:** State-by-state delegate math
- **Coalition mechanics:** VP selection, endorsement chains
- **Real fundraising:** Integration with FEC data
- **Third-party agents:** Only major candidates in v1
- **Ballot access:** Eligibility modeling
- **Downballot races:** Only presidential focus

---

## Success Definition (Shipped)

Campaign Manager Mode ships when:

✓ A user can manage one agent for a full 12-week cycle  
✓ Weekly decisions (messaging, budget, event participation, coaching) move support measurably  
✓ Two managers of the same agent end with different support levels and narratives  
✓ Real calendar events appear as live decision points  
✓ Real polling informs salience without determining outcomes  
✓ The experience feels consequential and personal, not algorithmic  
✓ Nonpartisan guardrails hold (no systematic bias toward any ideological set of agents)  
✓ Managers understand *why* their decisions led to their outcomes

