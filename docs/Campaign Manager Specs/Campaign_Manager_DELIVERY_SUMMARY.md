# Campaign Manager Mode — Complete Specification Delivered

## What Has Been Created

A complete, implementation-ready specification for **Campaign Manager Mode**, a new layer on top of Political Arena that turns the platform into an active strategy game with real-time synchronization.

**Total specification: ~130 pages of design, data models, API contracts, UX flows, and integration architecture.**

---

## The Eight Documents (Read in Order)

### 1. Campaign_Manager_One_Pager.md (10 pages)
**Audience:** Everyone. Start here.

**Contains:**
- The core idea (1 sentence)
- Weekly UX flow (what users do)
- Why it works (6 key features)
- How support moves (formula + example)
- Global + local split (personalization)
- Data sources (calendar, polling, news)
- Budget mechanics
- Output coaching process
- Leaderboards (two layers)
- Nonpartisanship safeguards
- Experience flows (selection → weekly loop → results)
- Success criteria
- Phased rollout
- Open questions

**Use this to:** Get the whole concept in 10 minutes.

---

### 2. Campaign_Manager_Overview.md (17 pages)
**Audience:** Stakeholders, leads, anyone making decisions.

**Contains:**
- Executive summary
- Product vision (what, why, who)
- Document structure (this guide)
- Key design decisions (single agent, real calendar, global/local split, nonpartisan guardrails, coaching loop)
- Core mechanics (weekly cycle, support movement, scoring, real-world integration)
- Global/local split (detailed)
- Nonpartisan model (how it works architecturally)
- Success criteria
- Implementation roadmap
- Remaining open questions
- Files in this specification

**Use this to:** Understand the full vision and make go/no-go decisions.

---

### 3. Campaign_Manager_Mode_PRD.md (20 pages)
**Audience:** Product, design, stakeholders.

**Contains:**
- Vision and product goals
- Core systems:
  - Weekly campaign cycle (5-phase loop)
  - Real-world inputs (calendar, news, polling)
  - User experience split (global arena vs. local war room)
- Data model (Campaign, CampaignWeek, ManagerDecision, AgentOutput, CampaignRealEvent)
- Detailed weekly UX flow (Sunday brief → Saturday results)
- Personalization (two managers, same agent, different outcomes)
- Scoring & standout moments
- Nonpartisanship guardrails
- Phased launch plan
- Success metrics

**Use this to:** Make product decisions, write acceptance criteria, plan design work.

---

### 4. Campaign_Manager_Data_And_API.md (21 pages)
**Audience:** Backend engineers, data architects, full-stack devs.

**Contains:**
- EF Core entity models (Campaign, CampaignWeek, BudgetAllocation, ManagerDecision, AgentOutput, CampaignRealEvent)
- Value objects and enums
- API endpoints (CRUD for campaigns, strategy, outputs, events, analytics)
- Real-world data service interfaces (polling, news, calendar)
- Support movement simulation engine (with formula)
- Database schema changes (new tables, modified tables)
- API surface summary
- Real-world data feed architecture
- Implementation notes (storage, caching, isolation)

**Use this to:** Build the backend, write migrations, design APIs, coordinate with data teams.

---

### 5. Campaign_Manager_Frontend_Spec.md (29 pages)
**Audience:** Designers, frontend engineers.

**Contains:**
- Navigation & layout (main app shell with new "My Campaign" nav)
- Campaign selection page (hero, agent grid, past campaigns)
- War room dashboard (main interface with 6 sections)
  - Weekly brief card
  - Issue salience card
  - Message & budget card
  - Pending approvals card
  - Performance trend chart
  - Channel ROI card
- Agent output coaching modal (detailed coaching UX)
- Event participation decision modal
- Analytics pages (performance, decisions, debates, outputs)
- Component library (15+ reusable components)
- Mobile responsiveness
- Accessibility requirements
- Real-time updates (WebSocket)
- Success metrics (UX)

**Use this to:** Build the frontend, create mockups, design interactions, implement components.

---

### 6. Campaign_Manager_Integration.md (20 pages)
**Audience:** Architects, senior engineers, platform leads.

**Contains:**
- System dependencies (what existing systems are used/modified)
- New dependencies (real-world data feeds)
- Data flow diagrams (weekly cycle, debate participation, output coaching)
- Impact on existing systems (Bot Heartbeat, Ranking Engine, Civic Arena)
- Database schema changes
- API surface (summary)
- Real-world data feeds (polling, news, calendar)
- Simulation engine
- Nonpartisanship safeguards (architectural)
- Phased rollout timeline (4 phases, 9 weeks)
- Risk mitigation (5 key risks)
- Future directions (6 expansions, not v1)

**Use this to:** Understand system architecture, plan integration work, manage dependencies, identify risks.

---

### 7. Campaign_Manager_Civic_Arena_Integration.md (14 pages)
**Audience:** Product, platform leads, Civic Arena team.

**Contains:**
- The bigger picture (3 layers: sensemaking, debate, campaign)
- How they connect (briefing → campaign moment, polling → manager environment, Think Deeper → real decision)
- Data alignment (source of truth, content reuse)
- User journey (civic learner → campaign manager)
- Content reuse example (Student Data Privacy)
- Nonpartisanship across platform
- Data architecture (logical view)
- Cross-product moments
- Unified user experience
- Success definition (understand → see → test → connect)
- Development synchronization
- Platform vision

**Use this to:** Understand how Campaign Manager fits into the larger Civic Arena vision, coordinate with Civic Arena team.

### 8. Campaign_Manager_LLM_Efficient.md (10 pages)
**Audience:** Engineering, product, anyone concerned with LLM cost.

**Contains:**
- Core principle: Precompute choices, don't generate on-demand per manager request
- Output generation pipeline (daily news batch, weekly messages, debate prep)
- Cost calculations and comparison (on-demand vs. precomputed: 80% cost reduction)
- Data model changes (new AgentOutputChoice entity, modified AgentOutput)
- Manager UX flow (manager picks from 3–4 precomputed options, then can refine 2x)
- API changes (choice selection, refinement, pending choices endpoint)
- Batch job scheduling (daily 5pm news, weekly Sunday, Thursday debate)
- Manager experience improvements (instant availability, strategic transparency)
- Fallback for unanticipated events (on-demand with rate limiting)
- Monitoring and alerting (track cost, response time, choice quality)
- FAQ addressing manager concerns (constrained choices, edge cases)
- LLM cost: ~$10/week vs. $180/week on-demand

**Use this to:** Understand how to scale LLM usage affordably, make cost estimates, decide on batch precomputation strategy.

---

## How to Use These Documents

### For Product & Stakeholders
1. Read: One-Pager (10 min)
2. Read: Overview (30 min)
3. Read: PRD (45 min)
4. Discuss: Open questions
5. Decision: Go/No-Go

### For Design
1. Read: One-Pager
2. Read: Frontend Spec (carefully, design-focused sections)
3. Review: Campaign_Manager_Mode_PRD.md (UX flow section)
4. Create: Mockups for key pages (dashboard, brief, coaching modal)
5. Prototype: Budget allocator, output approval interactions

### For Backend Engineering
1. Read: Data_And_API.md (thoroughly)
2. Read: LLM_Efficient.md (how to manage LLM cost)
3. Skim: Integration.md (architecture section)
4. Read: PRD (to understand product context)
5. Plan: 
   - Database migrations (Campaign, CampaignWeek, AgentOutputChoice)
   - API endpoints (in order)
   - Real-world data services (polling, calendar, news)
   - Support simulation logic
   - Batch jobs (daily news, weekly messages, debate prep precomputation)

### For Frontend Engineering
1. Read: Frontend_Spec.md (thoroughly)
2. Skim: Data_And_API.md (API sections)
3. Read: PRD (to understand product context)
4. Plan:
   - Component library
   - Page structure (dashboard, coaching modal, analytics)
   - State management
   - Real-time updates (WebSocket integration)

### For Platform / Architecture
1. Read: Integration.md (thoroughly)
2. Read: Overview.md
3. Review: PRD (success metrics, data model)
4. Plan:
   - System dependencies
   - Data integration points
   - Risk mitigation
   - Monitoring & auditing

### For Civic Arena Team
1. Read: Civic_Arena_Integration.md
2. Review: PRD (real-world inputs section)
3. Discuss: Data feed APIs (polling, calendar, news)
4. Coordinate: Briefing tagging, polling format, nonpartisan standards

---

## Key Metrics to Track

### Product Metrics
- 40% of users create a campaign
- 60% complete a full 12-week cycle
- 8+ manager decisions per week (engagement)
- Two managers of same agent diverge >15% in support (proves skill matters)
- Campaign completion rate by week (track attrition)

### Data Quality Metrics
- Real polling → output resonance correlation (>0.7)
- Message discipline score reliability (>0.8 consistency)
- Nonpartisan score distribution (<5% bias)
- Support movement predictability (manager can predict direction 60%+)

### Business Metrics
- Weekly active managers
- Campaign completion rate
- Time spent per session (target: 20–30 min)
- Leaderboard engagement
- Social sharing of campaign results

### Safety Metrics
- Monthly ideological bias audit
- Nonpartisan guardrails violations
- User complaints about perceived bias
- Community trust score (anonymous survey)

---

## Implementation Checklist

### Phase 1: Core Loop (Weeks 1–4)

**Backend:**
- [ ] Campaign, CampaignWeek entities
- [ ] ManagerDecision entity
- [ ] BudgetAllocation value object
- [ ] Database migrations
- [ ] Campaign CRUD endpoints
- [ ] Weekly strategy endpoint
- [ ] Support simulation engine (basic)

**Frontend:**
- [ ] Campaign selection page
- [ ] War room dashboard (basic layout)
- [ ] Messaging & budget selector
- [ ] Performance chart (placeholder)

**Success:** Users can create campaign, set strategy, see support move.

### Phase 2: Real-World Integration (Weeks 5–6)

**Backend:**
- [ ] Calendar service (real events)
- [ ] Polling service (issue salience)
- [ ] News service (trending topics)
- [ ] Weekly brief generation
- [ ] CampaignRealEvent entity

**Frontend:**
- [ ] Weekly brief display (calendar, polling, alerts)
- [ ] Issue salience visualization
- [ ] Event participation decision modal

**Success:** Weekly brief reflects real news and polling.

### Phase 3: Agent Outputs & Debates (Week 7)

**Backend:**
- [ ] AgentOutput entity
- [ ] Output coaching endpoints (approve/redirect/regen)
- [ ] Debate participation endpoint
- [ ] Debate outcome → support impact calculation

**Frontend:**
- [ ] Pending approvals panel
- [ ] Coaching modal (approve/redirect/regen UI)
- [ ] Event decision modal

**Success:** Managers can coach outputs and enter debates.

### Phase 4: Analytics & Polish (Weeks 8–9)

**Backend:**
- [ ] Campaign performance aggregation
- [ ] Decision log storage & retrieval
- [ ] Global leaderboard (top campaigns)
- [ ] Campaign score calculation

**Frontend:**
- [ ] Analytics pages (performance, decisions, debates)
- [ ] Decision log view
- [ ] Global leaderboard integration
- [ ] Email summary (optional)

**Success:** Full shipped experience.

---

## Open Decisions (For Leadership)

1. **Real data sources:** Which polling API? Which calendar source? Which news feed?
2. **Support scaling:** How is starting support determined? (Same for all agents, or agent-specific?)
3. **Difficulty modes:** Do difficulty modes affect starting support, volatility, or both?
4. **Budget constraints:** Are there penalties for missing a week? Can managers catch up later?
5. **Campaign length:** Is it always 12 weeks, or does it vary by election (primaries shorter, general longer)?
6. **Non-election years:** How does Campaign Manager work in off-years? (Scope for v2, but needs planning)

---

## Success Looks Like

**Week 4 (End of Phase 1):**
- 100+ beta testers have created campaigns
- Feedback: "I can see how my decisions move support"
- No major bugs in support simulation
- Dashboard feels intuitive

**Week 9 (End of Phase 4):**
- 1k beta testers, 60% completed campaigns
- Feedback: "I learned something about campaign strategy"
- Nonpartisan audits show no systematic bias
- Leaderboard is active and competitive
- Ready for public launch

---

## Long-Term Vision (Beyond v1)

- **Head-to-head manager competition** — Two managers in real time
- **Primary modeling** — State-by-state delegate strategy
- **Coalition building** — VP selection, endorsement chains
- **Off-cycle campaigns** — Local races, ballot measures
- **Team mode** — Classrooms managing one campaign together
- **Leaderboard seasons** — Monthly/yearly tournaments
- **Campaign AI coaching** — "Here's why your messaging didn't resonate"

All documented in Integration.md under "Future Directions."

---

## Questions?

### For Product Direction
→ Read Overview.md or PRD.md

### For Architecture
→ Read Integration.md or Civic_Arena_Integration.md

### For Technical Implementation
→ Read Data_And_API.md or LLM_Efficient.md

### For UX/Design
→ Read Frontend_Spec.md or One-Pager.md

### For Civic Learning Context
→ Read Civic_Arena_Integration.md

### For LLM Cost & Efficiency
→ Read LLM_Efficient.md (80% cost reduction through precomputation)

---

## Bottom Line

**You have a complete, specification-ready design for Campaign Manager Mode.** 

It includes:
- Vision and product strategy
- Data models and API contracts
- UX flows and component library
- Integration architecture
- **LLM cost efficiency strategy (80% reduction via precomputation)**
- Risk mitigation
- Success metrics
- Phased rollout plan

**Everything needed to build it is here. The only missing pieces are decisions (which real data feeds to use, difficulty settings, etc.) and execution.**

**Ready to build.**

