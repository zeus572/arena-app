# Campaign Manager Mode — Implementation Decisions & Follow-Up Tasks

## Your Decisions (Locked In)

### ✅ Launch Timing
**Decision:** Launch now (not wait for 2028)
**Impact:** Campaign Manager goes live during active 2024 election cycle
**Benefit:** Timely, relevant, synced to real campaign happening right now

### ✅ Candidate Pool
**Decision:** Use AI-generated candidates already live at Civic Arena
**Source:** https://jolly-pebble-0e9d50810.7.azurestaticapps.net/candidates
**Impact:** No need to create new agents; use existing Civic Arena candidates
**Details:**
- These are synthetic/AI personas, not real politicians
- Already available, already integrated with Civic Arena
- Consistent with platform identity (AI agents, not celebrities or historical figures)

### ✅ Source Library
**Decision:** Keep existing sources for now; expand in future task
**Approach:** Use current Civic Arena sources as-is
**Next step:** Separate Cowork task to research additional news sources
**Timeline:** Phase 2+ enhancement

### ✅ Difficulty Scaling (Resolved)
**Decision:**
1. **All managers start at the same support level.** No head start for "Easy" mode. Everyone begins at the same baseline (recommended: 25%), so the leaderboard is fair and comparable.
2. **Difficulty affects volatility.** The starting point is identical, but how much support swings per week scales with difficulty.

**How it works:**

| Difficulty | Starting Support | Weekly Volatility | Feel |
|------------|:----------------:|:-----------------:|------|
| Easy | 25% | Low (±0.5–1%) | Forgiving. Bad weeks don't hurt much; good weeks build steadily. |
| Normal | 25% | Moderate (±1–2%) | Balanced. Decisions matter, but one mistake isn't fatal. |
| Hard | 25% | High (±2–4%) | High-stakes. Great weeks soar; poor weeks crater fast. |

**Why this is the right model:**
- **Fair baseline:** Same start = leaderboard scores are directly comparable across difficulties. Nobody "wins" by picking Easy and starting ahead.
- **Volatility as the dial:** Harder mode means a manager's choices have larger consequences in both directions. Skilled managers thrive on Hard (big swings reward good decisions); newer managers prefer Easy (mistakes are cushioned).
- **Skill expression:** On Hard, the gap between a good and bad week is wide, so the best managers visibly separate from the pack. On Easy, the game is more about steady learning than high-stakes optimization.

**Implementation note:** Volatility is a multiplier applied to the support-movement formula in the simulation engine. Same decisions → same *direction* of movement; difficulty scales the *magnitude*. See Data_And_API.md `SupportSimulation` for where the multiplier slots in.

### ✅ Non-Election Years (2025–2027)
**Decision:** Defer to separate task, go big on 2024
**Action:** Create new spec task for off-cycle campaigns
**Focus for now:** 2024 election cycle only

---

## Campaign Manager v1 (2024 Election Cycle)

### What's In Scope

✅ Real 2024 election calendar (real dates, real events)
✅ AI-generated candidates from Civic Arena (not real politicians)
✅ Existing source libraries (current Civic Arena sources)
✅ Weekly strategy cycle (messaging + budget)
✅ Precomputed output choices (3–4 per event, LLM-efficient)
✅ Bounded refinement (max 2x per choice)
✅ Support simulation (manager skill-based)
✅ Global + local views (leaderboard + personal war room)
✅ Integration with Civic Arena (same candidates, same briefings)
✅ Nonpartisan guardrails (equal machinery)
✅ Analytics & decision log

### What's Out of Scope (v1)

❌ Non-election years (2025–2027) → Separate task
❌ Additional news sources → Separate Cowork task
❌ Expanded candidate pool → Separate task

---

## Difficulty Scaling: Final Design

**Resolved.** Same starting support for everyone; difficulty scales volatility.

### Scenario: Three Managers, Same Candidate

Manager A (Hard), Manager B (Normal), Manager C (Easy) all manage the same AI candidate. All three **start at 25% support**.

The difference is how much their weekly support swings:
- **Manager A (Hard):** ±2–4% swings. A strong week jumps several points; a weak week falls hard. High skill ceiling, high risk.
- **Manager B (Normal):** ±1–2% swings. Balanced. Decisions matter without being punishing.
- **Manager C (Easy):** ±0.5–1% swings. Forgiving. Good for learning the mechanics without big penalties.

### Why This Model

- **Fair leaderboard:** Identical start means scores compare directly across difficulties. Nobody gets a head start by choosing Easy.
- **Volatility = the skill dial:** On Hard, good decisions are rewarded more and bad decisions punished more, so skilled managers separate from the pack. On Easy, the experience is steadier and more instructional.
- **Self-selection:** New managers pick Easy to learn; competitive managers pick Hard for the high-stakes optimization challenge.

### Implementation

Volatility is a **multiplier on the support-movement formula** in `SupportSimulation` (see Data_And_API.md). Same decisions produce the same *direction* of movement regardless of difficulty; the multiplier scales the *magnitude*:

```
difficultyMultiplier = { Easy: 0.5, Normal: 1.0, Hard: 2.0 }
weeklyMovement = baseMovement * difficultyMultiplier
```


---

## Follow-Up Tasks Created

### Task 1: Research Additional News Sources (Cowork)

**Objective:** Identify candidate news sources beyond current Civic Arena sources

**Deliverables:**
- [ ] Document current Civic Arena sources (what we have now)
- [ ] Identify 5–10 additional news sources by category:
  - [ ] Official campaign statements & platforms
  - [ ] News aggregators (AP, Reuters, etc.)
  - [ ] Policy analysis (think tanks)
  - [ ] Polling aggregators
  - [ ] Social media (official accounts only)
- [ ] Assess quality & reliability of each
- [ ] Estimate effort to integrate each
- [ ] Recommend priority order for integration

**Owner:** [Research/Content team]
**Timeline:** 2–3 weeks
**Impact on Campaign Manager:** Will be integrated in Phase 2 expansion

---

### Task 2: Non-Election Years Campaign Manager (New Spec)

**Objective:** Design Campaign Manager experience for off-cycle periods (2025–2027)

**Scenarios to explore:**
- [ ] Local/state races (governor, Senate, House candidates)
- [ ] Ballot measures (Prop 1, Prop 2, etc.)
- [ ] Hypothetical 2028 primaries (model potential candidates)
- [ ] Historical "what-if" scenarios (past elections with different strategies)
- [ ] Evergreen civic topics (practicing campaign strategy with fictional issues)

**Deliverables:**
- [ ] PRD for off-cycle campaigns
- [ ] Data model extensions (local races, ballot measures)
- [ ] Candidate pool options for 2025–2027
- [ ] User engagement strategy

**Owner:** [Product]
**Timeline:** After 2024 campaign ends (Nov 2024+)
**Dependencies:** Wait until Campaign Manager v1 ships and learns what users want

---

## Implementation Adjustments for AI Candidates

### No Changes to Architecture

The entire Campaign Manager spec stays the same. The only difference:

```
BEFORE (Spec Used Historical/Celebrity Agents):
Agents: Bernie Sanders, Trump, Obama, Hamilton, Lincoln, Franklin
Sources: Books, speeches, historical documents, curated personas

AFTER (Using Civic Arena AI Candidates):
Agents: [AI candidates from Civic Arena]
Sources: [Existing Civic Arena sources for each candidate]
```

Everything else: Same data models, same APIs, same UX, same mechanics.

### Using Existing Civic Arena Candidates

**What you already have:**
- ✅ Candidate list at https://jolly-pebble-0e9d50810.7.azurestaticapps.net/candidates
- ✅ Source libraries for each
- ✅ Positioning & values profiles
- ✅ Integration with Civic Arena platform

**What Campaign Manager needs:**
- Candidate agent definitions (map Civic Arena candidates to Campaign Manager agents)
- Source library access (read from Civic Arena)
- Candidate rhetoric/style patterns (how does each candidate typically communicate?)
- Current candidate positions (for output generation)

**Integration point:**
Campaign Manager queries Civic Arena API:
```
GET /api/candidates
GET /api/candidates/:candidateId/sources
GET /api/candidates/:candidateId/positions
```

---

## Revised Timeline

### Phase 1: Core Loop (4 weeks)
- Campaign creation with **AI candidates from Civic Arena**
- Weekly strategy (messaging + budget)
- Support simulation
- Basic UX

**Success:** Users can manage an AI candidate for one week and see support move

### Phase 2: Real-World Integration (2 weeks)
- Real 2024 calendar events
- Real polling (issue salience)
- News alerts
- **Uses current Civic Arena sources** (can expand later)

**Success:** Weekly brief reflects live 2024 campaign calendar

### Phase 3: Precomputed Choices (2 weeks)
- Batch job generates 3–4 output options per news event
- Manager picks and refines (max 2x)
- LLM cost: ~$10/month

**Success:** Managers see instant choices, fast output coaching

### Phase 4: Debates & Analytics (2 weeks)
- Real 2024 debate participation
- Campaign analytics dashboard
- Decision log & global leaderboard

**Success:** Full shipped experience, ready for public 2024 election

**Total: 10 weeks to public launch**

---

## What Changes in the Spec

### Agent Pool Section

**OLD:**
```
Agents: Bernie Sanders, Donald Trump, AOC, Hamilton, Lincoln, etc.
Type: Mix of celebrity, historical, original
Source: Curated personas, books, speeches
```

**NEW:**
```
Agents: AI candidates from Civic Arena
Type: Synthetic personas
Source: Civic Arena candidate list & sources
API: https://jolly-pebble-0e9d50810.7.azurestaticapps.net/candidates
```

### Sources Section

**OLD:**
```
Curate custom source libraries for each agent
```

**NEW:**
```
Use existing Civic Arena source libraries for each candidate
Integration: Query Civic Arena API for sources
Expansion: Separate task to add news sources (Phase 2+)
```

### No Other Changes

- Data models: Same
- APIs: Same
- UX: Same
- Mechanics: Same
- Cost: Same (~$10/month)
- Timeline: Same (10 weeks)

---

## Updated Agent List (From Civic Arena)

Replace all references to "Bernie Sanders", "Trump", "AOC", etc. with:

**[Actual candidates from Civic Arena interface]**

(I don't have direct access to the Civic Arena candidate list, so I'm deferring to your system. The spec should reference them by name and include their candidate ID from the API.)

---

## Implementation Readiness

### Ready to Build ✅
- Core mechanics (weekly cycle, budget, support simulation)
- UX/UI (all pages, components)
- Data models (Campaign, CampaignWeek, outputs)
- LLM efficiency (precomputed choices)
- Difficulty scaling (same start, volatility multiplier — resolved)
- Integration with existing Political Arena systems

### Waiting for Separate Tasks 📋
- News source research (Cowork task)
- Off-cycle campaigns (new spec task, post-2024)

---

## Next Steps

### Immediate (This Week)
1. ✅ Difficulty scaling resolved (same start, volatility scales with difficulty)
2. ✅ Share Civic Arena candidate list (names/IDs)
3. ✅ Confirm API endpoints for Civic Arena candidates & sources

### Short-term (Next Week)
1. Create Cowork task: Research additional news sources
2. Hand off specs to engineering (10 documents ready)
3. Begin Phase 1 implementation
4. Set up Civic Arena API integration

### Medium-term (Weeks 2–10)
1. Build Campaign Manager v1 (10 weeks)
2. Run Cowork task for news sources
3. Monitor 2024 calendar for launch readiness
4. Begin planning off-cycle campaigns task

---

## Summary

**Campaign Manager v1 is locked in:**

✅ Launch: Now (2024 election)
✅ Candidates: AI personas from Civic Arena
✅ Sources: Existing Civic Arena sources (expand separately)
✅ Difficulty: Same start for all; difficulty scales weekly volatility
❌ Non-election years: Defer to separate task

**Everything is specified and ready to build.**

