# Campaign Manager Mode — Complete Specification (Final)

## What Has Been Delivered

A **complete, implementation-ready specification for Campaign Manager Mode** — a major new platform feature that makes Political Arena + Civic Arena into an integrated ecosystem for civic learning and democratic engagement.

**Total specification: 10 documents, ~20,000 words**

---

## The 10 Documents

### Foundational (Start Here)
1. **Campaign_Manager_One_Pager.md** — Entire concept on one page
2. **Campaign_Manager_Overview.md** — Executive summary with design decisions

### Core Specifications
3. **Campaign_Manager_Mode_PRD.md** — Full product requirements
4. **Campaign_Manager_Data_And_API.md** — Technical specification (entities, endpoints, scoring)
5. **Campaign_Manager_Frontend_Spec.md** — UI/UX specification (pages, components, flows)
6. **Campaign_Manager_Integration.md** — System architecture and integration with existing products

### Strategic Docs
7. **Campaign_Manager_Civic_Arena_Integration.md** — How Campaign Manager connects to the broader platform
8. **Campaign_Manager_Agent_Selection.md** — **NEW: Why we use real 2024/2028 presidential candidates**
9. **Campaign_Manager_LLM_Efficient.md** — LLM cost optimization (80% reduction via precomputation)
10. **Campaign_Manager_DELIVERY_SUMMARY.md** — Implementation guide and quick reference

---

## Core Concept: Real Elections, Fictional Outcomes

Users become **campaign managers for real 2024/2028 presidential candidates**, making weekly decisions about strategy and messaging that unfold against the **real election calendar**.

```
Real world:
├─ Actual candidate platforms (from Civic Arena)
├─ Actual polling (issue salience)
├─ Actual calendar (debates, primaries, election night)
└─ Actual news cycle

Campaign Manager environment:
├─ Uses real calendar & polling as backdrop
├─ But outcomes are 100% fictional (determined by manager skill)
└─ All grounded in documented candidate positions (no fabrication)
```

**Why this works:**
- Live & timely (synced to real election)
- Educational (learn actual campaign strategy)
- Nonpartisan (all candidates have equal machinery)
- Personal (same candidate, different managers = different campaigns)
- Replayable (each week creates new decisions)

---

## Why Real Presidential Candidates (Not Historical Figures)

### Connection to Civic Arena

Civic Arena teaches teens about **actual 2024 candidates**. Campaign Manager lets them **strategically manage those same candidates**.

```
Teen's journey:
─────────────
1. Civic Arena: "Learn who Harris is, what Trump believes"
   ├─ Read briefings
   ├─ Understand positions
   └─ Take Think Deeper quiz

2. Political Arena: "Watch them debate"
   ├─ See their arguments clash
   ├─ Vote on winner
   └─ Understand tradeoffs

3. Campaign Manager: "Run their campaign yourself"
   ├─ Make strategic choices
   ├─ Coach messaging
   ├─ Manage budget
   └─ See outcomes

Result: Deep understanding of actual 2024 candidates & how campaigns work
```

### Unified Around Reality

```
Same real event appears in all three products:

June 27, 2024: Real CNN Debate
├─ Civic Arena: "What happened in the debate?" (briefing)
├─ Political Arena: "Debate simulation with agents" (entertainment)
└─ Campaign Manager: "Opportunity to participate" (strategy)

All three reference the same real candidates, real topics, real issues.
```

---

## The Weekly Manager Experience

### Sunday Evening: Brief

Manager logs in and sees:
- **Calendar:** Real events this week (debate Tuesday? Primary? News expected?)
- **Polling:** What voters care about this week (economy +8pts, climate -3pts)
- **Alerts:** Breaking news, rapid-response opportunities

### Monday-Wednesday: Strategy

Manager sets:
- **Messaging priorities:** 1–2 issues to emphasize (based on polling & candidate strength)
- **Budget:** Allocate 100 points across ads, ground game, digital, debate prep, opposition research

### Wednesday-Thursday: Output Coaching

System precomputes 3 output options for each daily news story:
```
"Fed Rate Hike Breaking"
├─ Option A: Economy Focus (attack inequality)
├─ Option B: Pragmatism (acknowledge tradeoff)
└─ Option C: Pivot (shift to candidate strength)
```

Manager:
- Picks one option → instantly renders
- Reviews scores (consistency, resonance, engagement)
- Chooses: **Approve** | **Refine** (max 2x) | **Kill**

### Thursday: Event Decisions

Real debate tonight? Real primary? Manager decides:
- **Participate** → Use debate prep budget, compete with other candidates
- **Skip** → Conserve budget, focus on digital
- **Rapid Response** → Issue statement instead of live debate

### Saturday: Results

Support moves based on:
- Messaging alignment with polling priorities
- Output quality (consistency with candidate's actual record)
- Event performance (won/lost/compromised debate)
- Budget ROI (historical channel performance)
- Message discipline (consistency over week)

Manager sees:
- New support level (+1.7% this week)
- Detailed breakdown of what moved the needle
- Weekly score (87/100)
- Comparison to average

---

## Why LLM Efficiency Matters

### The Problem (On-Demand Generation)

```
Manager sees news → Requests custom output → Wait 30–60s for LLM → Review
200 managers × 3 requests/day = 600 LLM calls/day = $180/month
Unpredictable cost spikes during major news
```

### The Solution (Precomputed Choices)

```
System (5pm daily): Precomputes 3 options per agent per trending story
  └─ Manager opens dashboard next morning → Choices already ready
  └─ Picks one → Instant render
  └─ Can refine (max 2x) → 1 additional LLM call per refinement
  
~32 LLM calls/day = $10/month (80% reduction)
Predictable, scheduled, batch
```

### What Managers Get

Better UX *and* lower cost:
- ✅ **Instant availability** (no waiting)
- ✅ **Strategic education** (learns why different angles exist)
- ✅ **Bounded customization** (can refine, but not endless iteration)
- ✅ **Lower cost** (80% cheaper)

---

## How It Integrates with Existing Products

### Civic Arena (Platform: Civic Learning)
```
Briefings: "Who is Kamala Harris?" (positions, values, record)
Concepts: "Healthcare debate" (both candidate positions)
Think Deeper: "Single-payer vs. public option?" (actual debate)
Polling: Issue salience data (what voters care about)
```

↓ Campaign Manager consumes

### Campaign Manager (New Feature: Strategy Simulation)
```
Source library: Same candidate positions from Civic Arena
Rapid response: News from Civic Arena becomes manager decisions
Polling: Same issue weights as Civic Arena
Calendar: Same real events as Civic Arena
```

↓ Feed results to

### Political Arena (Platform: Debate Entertainment)
```
Agents: Same candidates debate independently
Debates: Real debate participation from Campaign Manager managers
Leaderboard: Separate from Campaign Manager scores (no confusion)
```

**Result:** Integrated ecosystem where all three products reference the same real candidates, real issues, real calendar.

---

## Nonpartisanship Model

### How We Stay Neutral

1. **Real data is environment only**
   - Polling tells us *what's salient*, never *what's correct*
   - Manager for Harris can use same polling to build her case
   - Manager for Trump can use same polling to build his case

2. **All candidates have equal machinery**
   - Same budget system
   - Same coaching pipeline
   - Same debate rules
   - Same scoring formula

3. **Outcomes determined by manager skill, not algorithms**
   - Harris manager vs. Trump manager can both achieve 40% support
   - Or one could get 35%, other 45%
   - Determined by how well they manage, not who they chose

4. **Transparent scoring**
   - Every decision traced to every outcome
   - Manager can see: "This week support +1.7% because: economy messaging (59% voters care) + debate win (1.2% impact) + budget ROI (1.9x multiplier)"
   - No black boxes

5. **Monthly audits**
   - Check if any candidate has systematic advantage
   - Monitor for bias in output generation
   - Flag if one ideology's managers significantly outperform

---

## Success Metrics

### Product
- 40% of users create a campaign
- 60% complete a full 12-week cycle
- 8+ manager decisions per week
- Two managers of same candidate diverge >15% in final support

### Educational
- Pre/post quiz improvement on understanding campaign strategy
- Managers report learning specific skills (budgeting, messaging, event management)

### Business
- Campaign completion rate by week (track drop-off)
- Manager retention (weekly active managers)
- Social sharing of campaign results

### Safety
- Monthly ideological bias audit (<5% bias threshold)
- User complaints about perceived unfairness
- Community trust survey

---

## Implementation Timeline

### Phase 1: Core Loop (4 weeks)
Campaign creation, strategy setting (messaging + budget), support simulation
**Success:** Users can manage a candidate for one week and see decisions move support

### Phase 2: Real-World Integration (2 weeks)
Real calendar events, polling data, news alerts
**Success:** Weekly brief reflects live real data

### Phase 3: Precomputed Choices (2 weeks)
Batch job generates output options daily, managers pick and refine
**Success:** Managers see instant choices, not waiting for LLM generation

### Phase 4: Debates & Analytics (2 weeks)
Debate participation, performance dashboard, decision log
**Success:** Managers can participate in real debates, see full analytics

**Total:** 10 weeks to full public launch with 1k beta

---

## Cost Estimate

### LLM Costs (via precomputation)
- Daily news: ~25 calls/day
- Weekly messages: ~2.5 calls/day
- Debate prep: ~1.7 calls/day
- Manager refinements: ~3 calls/day (10% refinement rate)
- **Total: ~32 calls/day = ~$10/month**

vs. on-demand model: ~600 calls/day = $180/month (80% savings)

### Infrastructure
- Database: Standard (Campaign, CampaignWeek, outputs, scores)
- Batch jobs: ~4 scheduled jobs daily
- API endpoints: ~20 new routes (all standard CRUD)
- Frontend: Existing React stack, new pages
- Real-time: WebSocket for notifications (existing infrastructure)

**Estimate:** ~100–150 engineering days total

---

## What's Ready to Build

✅ Complete data models with EF Core definitions
✅ API specifications (every endpoint, request, response)
✅ UX flows and component library
✅ LLM precomputation pipeline with cost analysis
✅ Integration architecture with existing systems
✅ Nonpartisan safeguards and audit procedures
✅ Success metrics and monitoring
✅ Phased rollout plan
✅ Implementation checklist

---

## Open Decisions (For Leadership)

1. **Launch timing:** With 2024 campaign ongoing, or wait for 2028?
2. **Candidate pool:** Start with major candidates only, or include third parties?
3. **Source library ownership:** Who curates candidate sources for Civic Arena?
4. **Agent evolution:** Static sources (v1) or rolling updates (v2+)?
5. **Difficulty modes:** How to adjust starting support / volatility?
6. **Non-election years:** What happens in 2025–2027 between elections?

---

## The Bigger Vision

**Campaign Manager Mode** is the capstone of an integrated civic platform:

```
Civic Arena + Political Arena + Campaign Manager
│
├─ Learn about democracy
├─ See ideas in conflict
├─ Test your own strategy
└─ Understand how real campaigns work

All synchronized to real elections, real candidates, real issues.
All grounded in actual positions and voting records.
All nonpartisan, all educational, all engaging.
```

This is not a partisan tool. This is a **civic education platform** disguised as an election game.

---

## Files Delivered

All 10 documents are ready for your team:

- Campaign_Manager_One_Pager.md
- Campaign_Manager_Overview.md
- Campaign_Manager_Mode_PRD.md
- Campaign_Manager_Data_And_API.md
- Campaign_Manager_Frontend_Spec.md
- Campaign_Manager_Integration.md
- Campaign_Manager_Civic_Arena_Integration.md
- Campaign_Manager_Agent_Selection.md ← **NEW: Real candidates**
- Campaign_Manager_LLM_Efficient.md
- Campaign_Manager_DELIVERY_SUMMARY.md

Plus this final summary.

---

## Next Steps

**For stakeholders:**
1. Read One-Pager (10 min)
2. Read Overview (30 min)
3. Decide on 5 open decisions above
4. Greenlight implementation

**For engineering:**
1. Read Data_And_API.md + LLM_Efficient.md
2. Plan database migrations
3. Build batch jobs (precomputation pipeline)
4. Build API endpoints
5. Build frontend

**For design:**
1. Read Frontend_Spec.md
2. Create mockups for major pages
3. Prototype interactions

**For product:**
1. Finalize open decisions
2. Define success metrics precisely
3. Plan marketing/launch

---

## Bottom Line

**Campaign Manager Mode is ready to build.**

It's a complete feature specification grounded in:
- Real presidential candidates (not fictional agents)
- Real election calendar (live synchronization)
- Fictional outcomes (manager skill-based)
- Nonpartisan mechanics (equal machinery for all)
- LLM efficiency (80% cost reduction via precomputation)
- Deep integration with Civic Arena and Political Arena

All the pieces are specified. The only thing missing is execution.

**Let's build this.**

