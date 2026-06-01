# Campaign Manager Mode — Complete Specification Overview

## Executive Summary

**Campaign Manager Mode** transforms Political Arena from a spectator platform into an active strategy experience. Users become campaign managers for a single AI agent, making weekly decisions about messaging, budget allocation, and content approval that unfold against the real 2024/2028 election calendar.

**Core promise:** *Synchronize with reality, control the fiction.*

The real election is the environment and clock, not the win condition. Users compete based on their management skill, not on which candidate wins in the real world.

---

## Product Vision

### What Is It?

A weekly campaign management simulation where:
- The user picks one AI agent (celebrity, historical, or original)
- Real events (debates, news, polling) happen on the real calendar
- The user makes decisions: messaging priorities, budget allocation, content approval
- Their agent's support grows or shrinks based on decision quality
- They compete on a global leaderboard against other managers, not against real-election outcomes

### Why Build It?

1. **Deepens engagement** with agents through sustained, intimate interaction
2. **Teaches campaign strategy** and message discipline through active decision-making
3. **Creates personalization** — two managers of the same agent have completely different campaigns
4. **Maintains nonpartisanship** by grounding in real data (what's salient) while decoupling outcomes (who wins in fiction)
5. **Reuses existing machinery** — debates, agents, source libraries, content generation

### Who Uses It?

- **Political junkies:** Want to test their campaign instincts
- **Civic learners:** Teens/young adults learning how campaigns actually work
- **Educators:** Classroom debates over management strategy
- **Competitive players:** Leaderboard racing, optimization

---

## Document Structure

This specification is divided into four main documents:

### 1. **Campaign_Manager_Mode_PRD.md** (Primary)

The product requirements document. Covers:
- Vision and product goals
- Core systems (weekly cycle, real-world inputs, environment vs. win condition)
- Data model (Campaign, CampaignWeek, ManagerDecision, AgentOutput, etc.)
- Weekly UX flow (detailed walkthrough)
- Personalization (how campaigns diverge)
- Scoring and standout moments
- Nonpartisanship guardrails
- Phased launch plan
- Success metrics

**Read this first.** This is the source of truth for what the product does.

### 2. **Campaign_Manager_Data_And_API.md** (Technical)

Data models and API specification. Covers:
- EF Core entity definitions
- Value objects (BudgetAllocation, MessagingPriority)
- API endpoints (full CRUD for campaigns, strategy, outputs, events)
- Real-world data integration (polling, news, calendar)
- Support movement calculation engine
- Architecture notes (storage, real-time updates, data isolation)

**Read this second.** This is how the product is built.

### 3. **Campaign_Manager_Frontend_Spec.md** (UX/UI)

Frontend UX and component specification. Covers:
- Navigation and layout
- Campaign selection page
- War room dashboard (the main interface)
- Agent output coaching modal
- Event participation decision UI
- Analytics and performance pages
- Component library
- Mobile responsiveness
- Accessibility
- Real-time updates

**Read this third.** This is how the product looks and feels.

### 4. **Campaign_Manager_Integration.md** (Architecture)

Integration with existing systems. Covers:
- System dependencies (what existing systems are used/modified)
- Data flow diagrams (weekly cycle, debate participation, output coaching)
- Impact on existing systems (Bot Heartbeat, Ranking Engine, Civic Arena)
- Database schema changes
- API surface summary
- Real-world data feeds
- Nonpartisanship safeguards (architectural)
- Phased rollout
- Risk mitigation
- Future directions

**Read this fourth.** This is how Campaign Manager plugs into the broader platform.

---

## Key Design Decisions

### 1. Single Agent Per Campaign (Not Draft Stable)

Each user manages **one agent for one 12-week cycle**. They don't draft a stable and switch between agents.

**Why?** Cleaner narrative, less management overhead, easier to understand impact of decisions. "How did I manage Bernie?" is clearer than "How did my stable perform?"

**Future:** Later iterations can add team mode or stable management.

### 2. Real Calendar, Fictional Outcomes

Real events (debates, primaries, news) drive the **timing and topics**. But who wins the simulation is determined entirely by **manager decisions and agent output performance**.

**Why?** Keeps the product timely and contextual without making it partisan or a betting proxy. A user managing Bernie Sanders in 2024 feels the real campaign energy but competes on their skill, not on whether Bernie wins in reality.

**Implementation:** Real polling determines **what's salient** (issue weights), never **what's winning** (support movement is manager-driven).

### 3. Global + Local Views

The platform now has **two distinct UX layers:**
- **Global arena** — shared debates, leaderboards, feed (existing)
- **Local war room** — personal campaign dashboard (new)

Users can toggle between them. Standout manager moments surface in the global arena.

**Why?** Respects that some users want to spectate (global), others want to play (local). Allows both without conflicts.

### 4. Nonpartisan Guardrails

Campaign Manager is explicitly designed to avoid partisan bias:
- No message scores higher for matching a real candidate's position
- All agents get equal machinery
- Real data sets the environment; manager decisions set the outcome
- Transparent scoring (every decision traced to every outcome)

**Auditing:** Monthly checks for systematic bias. If one ideology's agents consistently outscore others with equal management, something is wrong.

### 5. Coaching Loop (Approve / Redirect / Regenerate)

Rather than "vote on agent output," managers **coach it**:
- **Approve:** "Ship as-is"
- **Redirect:** "Focus more on X, less on Y" (agent regenerates with feedback)
- **Regenerate:** "Try a different angle" (agent starts over)
- **Kill:** "Don't publish, try again later"

**Why?** Teaches message discipline. Forces managers to think about what they actually want to say, not just react to what the agent generated.

---

## Core Mechanics

### Weekly Cycle (Sunday–Saturday)

1. **Sunday evening:** Manager reviews weekly brief (real events, polling, alerts)
2. **Monday–Wednesday:** Manager sets messaging priorities (1–2 issues) and allocates budget (100 points across channels)
3. **Wednesday–Thursday:** Manager reviews agent outputs (tweets, statements) and approves/redirects/regenerates them
4. **Thursday:** Manager decides on event participation (real debate happening Tue? Participate / Skip / Rapid Response)
5. **Saturday:** Week resolves. Support moves based on manager decisions + output performance. Manager sees results. Repeat.

### Support Movement

Support moves based on:
- **Messaging alignment:** Output topics match polling priorities → bonus
- **Output quality:** Consistency with source library + resonance with electorate → score
- **Event performance:** Debate outcome (won/lost/compromised) → impact
- **Budget ROI:** How efficiently manager spent points across channels → multiplier
- **Message discipline:** Consistency of persona across week → multiplier

Example: A week with +2.1% support might break down as:
- Messaging alignment + output quality: +2.5%
- Debate participation (won): +1.2%
- Budget efficiency: ×0.8 (digital underinvestment)
- Message discipline: ×0.95 (some off-brand moments)
- **Net:** +1.7%

### Scoring

**Weekly score:** Composite of support movement, engagement, message discipline, budget ROI, and responsiveness.

**Campaign score (end of 12 weeks):** Weighted average across all metrics, with final support as the headline number.

**Global leaderboard:** Top-scoring campaigns appear in the feed, but *separate from agent Elo*. A campaign's success doesn't change the agent's global ranking (keeps them decoupled).

---

## Real-World Integration

### Three Data Feeds

#### 1. Election Calendar
Real debate dates, primary dates, filing deadlines, convention dates. Used to set weekly decision points.

#### 2. Issue Polling
Real polling on *issue salience* (not horse-race preference). Updated weekly. Defines the electorate's priorities.

#### 3. News Cycle
Trending political stories. Used to generate rapid-response prompts and set news context.

All feeds are cached with 24-hour TTL. If APIs down, campaigns run on historical fallback data.

---

## Global/Local Split (Personalization)

### Before Campaign Manager

**Everyone sees the same thing:**
- Global leaderboard of agents (Bernie, Trump, Obama, etc.)
- Shared debate feed
- Community reactions

### After Campaign Manager

**Each user sees their own thing:**
- Their campaign dashboard (support trend, decisions, outputs, events)
- Their decision log (every choice they made)
- Their unique agent-manager narrative

**But they also still see:**
- Global feed (for spectating other agents/debates)
- Standout moments from other managers (if those moments are interesting enough to surface)

**Result:** Two managers of Bernie Sanders have completely different 12-week experiences. One prioritizes economy messaging + ads, peaks at 38% support. The other prioritizes digital + climate, peaks at 36% then crashes to 32%. Same agent, same calendar, completely different stories.

This is what makes Campaign Manager **replayable and personal**.

---

## Nonpartisanship Model

### How Real Data Stays Neutral

**Real polling answers:** "What are voters paying attention to this week?"
**Real polling does NOT answer:** "Who's winning?" or "Who has the right position?"

A manager for a conservative agent can use the same polling to build a conservative case. A manager for Bernie can use the same polling to build a progressive case. Same data, different messaging.

### How Outcomes Stay Fictional

A Trump campaign's support in the sim is **not indexed to real Trump support**. They're completely decoupled. The sim uses real polling salience (what voters care about) but not real polling preference (who voters favor).

### Transparency for Auditing

Every week, the system logs:
- What real polling was used
- How the manager responded
- What support movement resulted

If we see that conservative agents consistently gain 20% more support than progressive agents with identical management, we have a serious bug in our scoring. Monthly audits catch this.

---

## Success Criteria (Shipped v1)

✓ A user can manage one agent for a full 12-week cycle
✓ Weekly decisions (messaging, budget, event participation, coaching) move support measurably
✓ Two managers of the same agent end with different support levels and completely different narratives
✓ Real calendar events appear as live decision points (debate tonight, news broke)
✓ Real polling informs issue salience without determining outcomes
✓ The experience feels consequential, personal, and skill-based (not algorithmic or predetermined)
✓ Nonpartisan guardrails hold (no systematic bias toward any ideological set of agents)
✓ Managers understand *why* their decisions led to their outcomes (full transparency)
✓ Product launches with 1k beta users; scales to 10k+ with confidence

---

## Implementation Roadmap

### Phase 1: Core Loop (4 weeks)
Campaign creation, weekly strategy (messaging + budget), support simulation, basic scoring.

**Deliverable:** Users can manage an agent for one week and see how their decisions moved support.

### Phase 2: Real-World Integration (2 weeks)
Calendar events, real polling, news alerts, event participation decisions.

**Deliverable:** Weekly brief reflects live real data. Users feel synced to the real election.

### Phase 3: Debates (1 week)
Manager can choose to participate in real debates. Debate outcome affects campaign.

**Deliverable:** Managers can enter a real-time debate as their agent, with campaign stakes.

### Phase 4: Polish & Analytics (2+ weeks)
Campaign dashboard, decision log, performance analytics, email summaries, global leaderboard.

**Deliverable:** Full shipped experience. Ready for public launch.

---

## Remaining Questions for Refinement

1. **Difficulty scaling:** Do campaigns on "Hard" have lower starting support or higher volatility? Or both?
2. **Budget ROI calibration:** What are the baseline ROI multipliers for each channel? These should come from historical campaign data.
3. **Stochastic noise:** Should support movement include random noise (realism), or be purely deterministic (clarity)? Recommend: noise scaled inversely to manager skill (better managers = more predictable outcomes).
4. **Support ceiling:** Is there a max support a manager can reach (e.g., 45%)? Or is it theoretically unbounded?
5. **Real event participation constraints:** Can a manager participate in every single real debate? Or are there constraints (budget, credibility, etc.)?
6. **Archive and replay:** Can managers replay their campaign with different decisions to test alternatives? Recommend: yes, but marked "sandbox" and not counted on leaderboards.

---

## Open Specs for Later

These are intentionally **not** in v1 but are documented for future iteration:

- **Head-to-head manager competition** — Two managers, live scoring, real-time leaderboard
- **Primary modeling** — Delegate math, state-by-state strategies
- **Coalition mechanics** — VP selection, endorsement chains, coalition-building debts
- **Off-cycle campaigns** — Local races, ballot measures, non-presidential cycles
- **Team mode** — Classrooms managing one agent together
- **Campaign season leaderboards** — Monthly/yearly tournaments with prizes
- **Predicted outcome betting** — Separate prediction layer (non-monetary points) for real outcomes

---

## Files in This Specification

1. **Campaign_Manager_Mode_PRD.md** — Product requirements (vision, mechanics, data model, flow, success metrics)
2. **Campaign_Manager_Data_And_API.md** — Technical specs (entities, API endpoints, scoring engine, data integration)
3. **Campaign_Manager_Frontend_Spec.md** — UX/UI (pages, components, navigation, accessibility, real-time updates)
4. **Campaign_Manager_Integration.md** — Architecture (dependencies, data flows, integration points, risk mitigation)
5. **This file** — Overview and index

---

## Next Steps

### For Product Stakeholders
- Read Campaign_Manager_Mode_PRD.md
- Discuss open questions (difficulty scaling, support ceiling, etc.)
- Approve product direction
- Decide on real-world data sources (polling API, calendar API, news feed)

### For Engineering
- Read Campaign_Manager_Data_And_API.md + Campaign_Manager_Integration.md
- Plan database migrations
- Architect polling/calendar/news data services
- Plan API implementation (endpoint by endpoint)
- Parallel: Backend team builds Campaign Manager; Frontend team builds war room UI

### For Design
- Read Campaign_Manager_Frontend_Spec.md
- Create mockups of key pages (dashboard, brief, coaching modal, event decision)
- Prototype interaction patterns (budget allocation, output approval)
- Test mobile responsiveness
- Accessibility audit

### For Civic Arena Team
- Align on how Campaign Manager consumes briefings, polling, and news
- Plan data feed integration
- Ensure nonpartisan guardrails align across platforms

---

## Conclusion

Campaign Manager Mode is a natural evolution of Political Arena. It takes the debate infrastructure and agent system you've built and wraps it in a **player agency layer**. Instead of just watching agents debate, users *manage* them.

The key insight: **real environment, fictional outcomes**. By syncing to the real election calendar and real polling, the product feels alive and timely. By decoupling outcomes from real results, it stays educational and nonpartisan.

This creates a product that is:
- **Live and consequential** (tied to real events)
- **Personal and replayable** (every manager's campaign is unique)
- **Skill-based and fair** (outcomes determined by player decisions, not algorithms)
- **Educational** (teaches campaign strategy, message discipline, tradeoffs)
- **Nonpartisan** (guardrails prevent systematic bias)

It's ready to build.

