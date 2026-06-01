# Campaign Manager Mode — One-Page Summary

## The Idea
Users become campaign managers for one AI agent, making weekly decisions about messaging, budget, and content approval as the 2024 election unfolds in real time.

**Real calendar + fictional outcomes = skill-based competition on a live stage**

---

## What Users Do (Weekly)

```
SUNDAY          MONDAY–WED          WEDNESDAY–THU         THURSDAY           SATURDAY
─────────────────────────────────────────────────────────────────────────────────────────

Review Brief    Set Strategy         Coach Outputs      Decide Events      See Results
├─ Calendar    ├─ 1–2 Issues        ├─ Approve          ├─ Debate?         ├─ Support Moved
├─ Polling      ├─ Budget 100pts     ├─ Redirect         ├─ Town Hall?       ├─ Engagement
├─ News        └─ Rationale         └─ Regenerate       └─ Rapid Response   └─ ROI
└─ Alerts                                                                      
```

**Time commitment:** ~20–30 min per week
**Replayability:** Same agent, different decisions → different outcomes

---

## Why It Works

| Feature | Benefit |
|---------|---------|
| **Real calendar** | Feels live & timely; debates happen on real dates |
| **Real polling** | Teaches what voters care about; grounds strategy |
| **Fictional outcomes** | Stays nonpartisan; manager skill determines results |
| **Single agent** | Cleaner narrative; clear cause-and-effect |
| **Coaching loop** | Teaches message discipline; not just approval/rejection |
| **Global + local views** | Spectate arena OR manage campaign; both matter |

---

## How Support Moves

```
Manager Decisions + Output Quality + Event Performance + Budget Efficiency → Support Change

Example Week:
─────────────
✓ Economy messaging + high resonance (87/100)           → +2.5%
✓ Won debate on healthcare                              → +1.2%
✓ Budget ROI: 1.9x                                      → ×0.9 multiplier
✓ Message discipline: 91/100                            → ×0.95 multiplier
─────────────────────────────────────────────────────────────────────────
RESULT: +1.7% support (25 → 26.7%)
```

---

## Global View vs. Local View

### Global Arena (Today)
- Everyone watches the same debates
- Agent leaderboards (Elo)
- Shared community reactions
- Feed recommendations

### + Local War Room (New)
- Your private campaign dashboard
- Weekly brief (personalized)
- Your agent's outputs (pending your approval)
- Your decision log (all choices traced)
- Your support trend (unique to your management)

**Result:** Same agent, different managers = different campaigns

```
Bernie Sanders (Agent)
├─ Manager A: Economy focus + ads → 38% support
└─ Manager B: Digital focus + climate → 32% support (then crash)
```

---

## Data Sources (Environment)

Real data informs the **environment**, never the **outcome**.

| Data | Used For | Impact |
|------|----------|--------|
| **Election Calendar** | Debate dates, primaries, events | When manager makes decisions |
| **Issue Polling** | What voters care about | Which messages resonate |
| **News Trends** | Breaking political stories | Rapid response opportunities |

**Key guardrail:** Real polling determines *salience* (weight), never *preference* (winner).

---

## Mechanics: The Weekly Budget

Manager allocates 100 points across:

| Channel | ROI (example) | Best For |
|---------|:-------------:|----------|
| **Ads** | 2.1x | Reach & frequency |
| **Ground Game** | 1.3x | Localized support |
| **Digital** | 0.8x | Young, engaged voters |
| **Debate Prep** | 1.6x | Event participation |
| **Opposition** | 0.9x | Contrast & differentiation |

**Constraint:** No category > 60%. Forces tradeoff thinking.

---

## Agent Output Coaching

Rather than "vote on output," managers **coach it**:

```
Agent Generates Draft
         ↓
Manager Reviews Scores:
  ├─ Consistency w/ sources: 92/100 ✓
  ├─ Resonance w/ polling: 87/100 ✓
  └─ Predicted engagement: 3.2k
         ↓
Manager Chooses:
  ├─ [✓ Approve] → Ship as-is
  ├─ [✏️ Redirect] → "Focus more on housing, less on billionaires"
  ├─ [🔄 Regenerate] → Start over with new angle
  └─ [❌ Kill] → Don't publish
         ↓
If Redirect/Regen → Agent regenerates in 30s → Manager re-reviews
```

**Teaches:** Message discipline, iteration, strategic communications

---

## Global Leaderboard (Two Layers)

### Agent Leaderboard (Existing)
Agents ranked by Elo (from debates)

### Campaign Leaderboard (New)
Managers ranked by Campaign Score

```
Campaign Score = 
  0.25 × FinalSupport +
  0.20 × Engagement +
  0.25 × MessageDiscipline +
  0.15 × BudgetROI +
  0.15 × EventROI
```

**Decoupled:** A campaign's success doesn't change the agent's Elo.

---

## Nonpartisanship Safeguards

| Safeguard | How |
|-----------|-----|
| **Real data is environment only** | Polling informs salience, never determines outcomes |
| **All agents equal machinery** | Same scoring, same budget system, same debate rules |
| **Transparent scoring** | Every decision traced to every outcome; fully explainable |
| **Monthly audits** | Check for systematic bias; flag any ideology advantage >5% |
| **Manager controls outcome** | Real data sets the backdrop; decisions determine results |

---

## Experience Flows

### Campaign Selection
1. User chooses agent (Bernie, Trump, Hamilton, AOC, etc.)
2. Names campaign ("Bernie's Revolution")
3. Picks difficulty (affects starting support, volatility)
4. Enters war room

### Weekly Loop
1. **Sunday:** Read brief (calendar, polling, news)
2. **Mon–Wed:** Set strategy (messaging + budget)
3. **Wed–Thu:** Coach outputs (approve/redirect/regen)
4. **Thu:** Decide on events (debate? yes/no)
5. **Sat:** See results (support moved, engagement, scores)

### End of Cycle
1. Campaign archive + performance summary
2. Decision log (every choice)
3. Campaign score + global rank
4. Can replay with different decisions (sandbox mode)

---

## Why This Matters

### For Players
- **Agency:** Your decisions move the needle, not algorithms
- **Narrative:** Your unique 12-week campaign arc (not predetermined)
- **Learning:** How campaigns actually work (strategy, messaging, budget, tradeoffs)
- **Competition:** Skill-based leaderboard (not partisan, not gambling)

### For Platform
- **Deeper engagement:** From passive (watch) to active (manage)
- **Replayability:** Same agent, different managers = unlimited variations
- **Personalization:** Each user's experience is unique
- **Civic education:** Teaches strategy + message discipline + nonpartisan thinking
- **Network effects:** Managers compete, compare, discuss decisions

---

## Success Criteria (v1)

✓ 40% of users create a campaign
✓ 60% complete a full 12-week cycle
✓ Two managers of same agent diverge >15% in final support (proves player skill matters)
✓ 8+ manager decisions per week (engagement intensity)
✓ All outcomes fully transparent (manager can explain why support moved)
✓ No systematic ideological bias in scoring
✓ Nonpartisan guardrails hold under audit

---

## Phased Rollout

| Phase | What | Duration |
|-------|------|----------|
| **Phase 1** | Core loop: strategy, budget, coaching, simulation | 4 weeks |
| **Phase 2** | Real-world data: calendar, polling, news | 2 weeks |
| **Phase 3** | Debates: manager can enter real debates | 1 week |
| **Phase 4** | Polish: analytics, log, global leaderboard | 2+ weeks |

**Launch:** 1k beta users → scale to 10k+

---

## Key Documents

| Doc | Audience | What |
|-----|----------|------|
| **Overview** | Everyone | This page (you are here) |
| **PRD** | Product, Design | Vision, mechanics, UX flow, data model |
| **Data & API** | Engineering | Entities, endpoints, scoring engine, integration |
| **Frontend** | Design, Frontend | Pages, components, UI, accessibility |
| **Integration** | Engineering, Leadership | How it fits into existing systems, risks, timeline |

---

## What's Next

1. **Stakeholder review** — Read PRD, discuss open questions
2. **Backend start** — Data migrations, campaign entity, support simulation
3. **Frontend start** — War room dashboard mockups, coaching panel
4. **Real data services** — Calendar API, polling integration, news feed
5. **Beta launch** — 1k managers, monitor for bias, iterate
6. **Public launch** — Full feature set, global leaderboard

---

## Open Questions (For Discussion)

1. **Difficulty scaling?** Hard mode = lower starting support or higher volatility?
2. **Budget ROI calibration?** What are baseline multipliers per channel? (Historical data?)
3. **Stochastic noise?** Deterministic (clear) or random (realistic)?
4. **Support ceiling?** Is there a max? (E.g., 45%?)
5. **Real event constraints?** Can manager participate in every debate? Or are there limits?
6. **Replay/sandbox?** Can managers replay their campaign to test alternatives?

---

## Bottom Line

**Campaign Manager Mode turns Political Arena into a strategy game with real-time synchronization and fictional stakes.** Users manage AI agents, make real decisions, compete on skill, and learn how campaigns actually work—all while the real election unfolds in the background.

**Same agent, different managers → different campaigns. That's the magic.**

