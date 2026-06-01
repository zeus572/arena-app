# Campaign Manager Mode — Agent Selection & Integration with Civic Arena

## Core Clarification: Presidential Candidates Only

**Campaign Manager Mode** uses **real 2024/2028 presidential candidates** as the agents managers can run campaigns for.

These are **not** the celebrity/historical agents from Political Arena (Trump, Bernie, Obama, etc.). These are the **actual candidates** from the real election cycle that users learn about in Civic Arena.

---

## Agent Pool (v1): 2024 Presidential Candidates

For the 2024 election cycle, the initial agent pool includes:

### Democratic Ticket
- **Kamala Harris** (Democratic nominee / incumbent VP)
- **Joe Biden** (incumbent President, depending on election cycle)

### Republican Ticket
- **Donald Trump** (Republican nominee)
- **JD Vance** (VP nominee)

### Other Candidates (if applicable)
- **Robert F. Kennedy Jr.** (independent / third-party option)
- **Jill Stein** (Green Party)
- **Chase Oliver** (Libertarian Party)

**Total agents for 2024:** 6–8 real presidential candidates

### For 2028 and Beyond
The agent pool **updates each election cycle** to reflect that year's actual candidates.

---

## How They're Different from Political Arena Agents

### Political Arena Agents (Existing)
- **Mix:** Historical figures (Lincoln, Jefferson), celebrities (Trump, Bernie), originals
- **Source library:** Curated from books, speeches, policy documents
- **Updated:** Static (don't change mid-election)
- **Purpose:** Debate entertainment, exploring arguments across ideological spectrum

### Campaign Manager Agents (New)
- **Pool:** Real 2024/2028 presidential candidates only
- **Source library:** Actual voting records, policy statements, debate transcripts, official documents
- **Updated:** Dynamically as the election unfolds (new statements, policy shifts)
- **Purpose:** Strategic simulation tied to real election

---

## Agent Source Library (Grounded in Reality)

Each presidential candidate agent has a **documented source library** based on their actual public record:

### Example: Kamala Harris (2024)

```
AgentSources:
├─ Voting record as senator (2017–2021)
├─ AG record as CA Attorney General (2011–2017)
├─ Campaign speeches & policy documents (2024)
├─ Town halls and interviews
├─ Official statements on key issues
│  ├─ Healthcare (supports expansion, public option)
│  ├─ Climate (aggressive on green jobs)
│  ├─ Border (balanced enforcement + immigration pathway)
│  ├─ Abortion (strong pro-choice stance after 2022 SCOTUS ruling)
│  └─ Economy (focus on middle class support)
└─ Debate performance transcripts (2020 primary, 2024 VP debate context)
```

### Example: Donald Trump (2024)

```
AgentSources:
├─ 2017–2021 presidency record
├─ Inaugural address & State of the Union speeches
├─ Policy documents (tax cuts, trade policy, immigration)
├─ 2024 campaign statements & Truth Social posts
├─ Legal documents & court filings (post-presidency)
├─ Debate performances (2024 primary)
├─ Rally speeches (characteristic rhetoric & messaging patterns)
└─ Official policy platform
   ├─ Trade (tariffs, reshoring)
   ├─ Immigration (border wall, enforcement)
   ├─ Healthcare (repeal ACA promises)
   └─ Economy (tax cuts for business & wealthy)
```

**Key difference from Political Arena:** These source libraries are **live data**, not curated character interpretations. They're grounded in **actual voting records, statements, and policy platforms**.

---

## Why Presidential Candidates?

### Civic Arena Integration
- Civic Arena teaches teens about **actual 2024 candidates and their positions**
- Campaign Manager lets those same teens **test their strategy** against those real candidates
- Same candidates appear across both products, creating coherent experience

### Educational Value
- Manager learns: "How would I run *a campaign for an actual candidate*?"
- Not: "How would I run a campaign for a fictional Bernie clone?"
- Connected to real stakes, real platforms, real disagreements

### Nonpartisanship
- **All candidates represented equally** in Campaign Manager
- No candidate has an algorithmic advantage
- Same coaching machinery, same budget system, same debate rules
- Manager skill determines outcome, not which candidate they choose

### Real-Time Relevance
- Candidates' actual statements, positions, debates feed into their agent behavior
- When a real debate happens, candidates debate in both Civic Arena (learning context) and Political Arena (entertainment) and Campaign Manager (strategic context)
- Unified around **real events, real candidates, real issues**

---

## Data Flow: Civic Arena → Campaign Manager

### Civic Arena Publishes
```
Briefing: "Kamala Harris on Healthcare"
├─ Her voting record
├─ Her stated positions
├─ Her campaign platform
├─ Values in conflict
└─ Think Deeper: "Universal healthcare vs. Market competition"
```

### Campaign Manager Uses Same Data
```
Manager running Kamala campaign:
├─ Can emphasize healthcare (her strength based on Civic Arena briefing)
├─ Knows her source library positions (from Civic Arena research)
├─ Sees real polling on healthcare salience (Civic Arena data)
└─ When real healthcare news breaks:
   └─ Three precomputed options generated:
      ├─ Option A: Aggressive (expand coverage, control costs)
      ├─ Option B: Pragmatic (acknowledge trade-offs, incremental progress)
      └─ Option C: Pivot (shift to different strength, e.g., economy)
```

---

## How Real Elections Sync

### 2024 Cycle Example

```
June 2024: Biden steps aside, Harris becomes presumptive nominee
  │
  ▼ Civic Arena
  ├─ Updates all Harris briefings, sources
  ├─ Adds Harris-specific policy documents
  └─ Publishes "Who is Kamala Harris?" briefing

  ▼ Political Arena
  ├─ Adds Harris agent to debate pool
  ├─ Schedules debates with her as participant
  └─ Community can vote on/predict her performance

  ▼ Campaign Manager
  ├─ Adds Harris as manageable agent
  ├─ Pulls her source library from Civic Arena
  ├─ Managers can now run "Harris for President" campaigns
  └─ Syncs to real 2024 campaign calendar (real DNC, real debates, real polling)
```

---

## Integration with Civic Arena Source System

### Shared Source Library

Rather than duplicate work, **Campaign Manager and Civic Arena share the candidate source libraries**:

```
Database:
├─ Agent (from Political Arena)
│  └─ AgentSources (shared across both platforms)
│
├─ Civic Arena uses sources for:
│  ├─ Briefings (what did they actually say/vote?)
│  ├─ Concept pages (how do their positions fit civic concepts?)
│  └─ Think Deeper (argue both sides using their actual positions)
│
└─ Campaign Manager uses same sources for:
   ├─ Agent output generation (what would they actually say?)
   ├─ Consistency scoring (does output match their record?)
   └─ Source citations (why did agent say that?)
```

**Benefit:** One team curates candidate sources once. Both products use it.

---

## Agent Authenticity Guardrails

Since we're using **real candidates**, we need strict authenticity rules:

### Rules for Agent Output Generation

```
When generating output for a real candidate, the LLM must:

1. Ground in documented positions
   ✓ "Harris supports public healthcare option"
   ✗ "Harris now supports single-payer" (not in her platform)

2. Use characteristic rhetoric
   ✓ Trump: Short, declarative, superlatives, nicknames
   ✗ Trump: Long policy treatises with nuance

3. Respect voting record
   ✓ Harris on justice: Balance between accountability and reform
   ✗ Harris as tough-on-crime hawk (oversimplifies her AG record)

4. Never fabricate positions
   ✓ "If asked about Mars colonization, Harris might say..."
   ✗ "Harris believes in X" (if no documented position exists)

5. Acknowledge evolution
   ✓ "Harris has shifted on marijuana legalization (2014→2020→2024)"
   ✗ "Harris always opposed legalization" (ignores her evolution)

Prompt guardrail: "You are simulating Kamala Harris for a political strategy game.
Stay grounded in her documented public record. Never invent positions she hasn't
taken. If she hasn't spoken on an issue, acknowledge that uncertainty."
```

---

## Campaign Manager Agent Lifecycle

### Before Election (Active Simulation)

```
March 2024: Campaign Manager launches with Harris, Trump, and others
├─ Real primary happens (managers can run Harris or Trump campaigns)
├─ Real debates happen (managers participate via Campaign Manager)
├─ Real polling updates (affects manager environment)
└─ Real campaign happens (managers are learning alongside the real thing)
```

### Election Night (Culmination)

```
November 5, 2024: Real election happens
├─ Real outcome: Harris wins or Trump wins
├─ Campaign Manager campaigns END
│  └─ Manager sees final score: "Harris: 37% simulated support"
│     (Completely independent from real election outcome)
├─ Manager can review decision log:
│  "I focused on healthcare (her strength). With better digital strategy,
│   I could have reached 42%. Here's where I made mistakes..."
└─ Campaigns archive; leaderboards finalize
```

### Post-Election (Retrospective)

```
December 2024–beyond: Campaign Manager shifts to analysis mode
├─ Real campaign is over
├─ Managers can do "sandbox replays" (retry decisions differently)
├─ Civic Arena publishes post-mortems: "How did Harris actually campaign?"
├─ Comparison: "Your simulated campaign vs. the real thing"
└─ Planning begins for 2028 candidates (next election cycle)
```

---

## Agent Updates During Election

Real candidates' positions and rhetoric **evolve during the campaign**. How do we handle that?

### Approach 1: Static Sources (Simpler)
```
Source library set at campaign start (March 2024)
Manager has consistent agent throughout 12 weeks
No mid-campaign updates to source library
Trade-off: Less real-time, but cleaner for manager experience
```

### Approach 2: Rolling Updates (More Real)
```
New statement from candidate → Civic Arena publishes briefing
  └─ Source library updated
  └─ Campaign Manager agent reflects new position
  └─ Manager sees: "[Candidate] just pivoted on trade policy"
  └─ Manager must adjust strategy accordingly

Trade-off: More complex, but reflects real dynamics
```

**Recommendation:** Start with **Approach 1 (static)**. Simpler for v1. Can evolve to rolling updates in v2.

---

## Civic Arena Integration Points

### 1. Source Library
Civic Arena curates and stores candidate sources. Campaign Manager consumes them.

### 2. Briefings
When Civic Arena publishes a briefing about a candidate's position, Campaign Manager can:
- Use the briefing as context for rapid-response options
- Reference the briefing in output explanations
- Link to related briefings when managers need background

### 3. Polling
Civic Arena polls on issue salience. Campaign Manager uses those same polls to weight manager environment.

### 4. Debates
When a real debate happens:
- Civic Arena publishes briefing about the debate
- Political Arena hosts a debate between agents
- Campaign Manager creates debate participation opportunity for managers
- All three products reference **the same real event**

---

## Example: Real Event Across All Three Products

### June 27, 2024: Real CNN Debate (First Presidential Debate)

#### Civic Arena
```
Briefing: "What Happened in the First 2024 Debate?"
├─ Summary of key moments
├─ Candidate performances
├─ Values in conflict
└─ Think Deeper: "Did either candidate win on policy?"
```

#### Political Arena
```
Debate: "CNN Debate Simulation - Harris vs. Trump"
├─ Agents debate real topics from the debate
├─ Community votes on winner
├─ Leaderboard updates
└─ Analysis of argument strength
```

#### Campaign Manager
```
Real Event: CNN Debate June 27

If manager is running Harris campaign:
├─ Brief shows: "Real debate tonight, 9pm ET"
├─ Manager decides: Participate / Skip / Rapid Response
├─ If Participate:
│  └─ Three precomputed prep options generated
│     ├─ Option A: Offense (attack Trump record)
│     ├─ Option B: Defense (protect record)
│     └─ Option C: Common Ground (find agreement)
├─ Manager chooses
└─ Debate happens in real time (same topics as real debate)
└─ Outcome: "Won/Lost/Compromised" → Support moves
```

**All three products:** Same real event, different angles.

---

## What This Means for the Specification

### Changes to Agent Pool

**Update all previous specs:**
- Remove "Bernie Sanders", "AOC", "Hamilton", "Lincoln"
- Replace with: "Harris 2024", "Trump 2024", "RFK Jr.", "Stein", etc.
- Update source library examples to use real candidate positions
- Add guardrails about authenticity

### No Architectural Changes

The Campaign Manager Mode architecture stays exactly the same:
- Same data models
- Same UX flows
- Same precomputation pipeline
- Same LLM efficiency approach

Just different agents (real candidates instead of historical figures).

### Integration with Civic Arena

Add a new section clarifying:
- How source libraries are shared
- How briefings feed into campaign context
- How real debates appear across both platforms
- How campaigns stay authentic to real candidates

---

## FAQ

### Q: What if a candidate drops out mid-campaign?

**A:** 
- If they drop in real life, we could:
  - Freeze their campaign in Campaign Manager (can't continue past their withdrawal)
  - Or allow "what-if" continuation (sandbox mode, clearly marked)
  - Most likely: freeze and archive

### Q: What if a candidate changes positions mid-campaign?

**A:**
- Version 1: Source library is static (set at campaign start)
- Version 2+: Rolling updates (candidate evolves, agent evolves)
- Managers get notification: "[Candidate] updated position on X"

### Q: How do we stay nonpartisan when managing real candidates?

**A:**
- All candidates get same machinery
- Source libraries grounded in documented positions (not interpretations)
- No algorithmic advantage to any candidate
- Monthly audits for bias
- Transparent scoring (manager can see why outcome happened)

### Q: What about third-party candidates?

**A:**
- Include if they reach debate threshold
- Treat identically to major candidates
- Source library: official platform + statements
- Equal campaign simulation opportunity

### Q: How does this work in 2028 with different candidates?

**A:**
- Campaign Manager resets with 2028 candidates
- Civic Arena briefings update to 2028 candidates
- Political Arena adds new agents
- Managers from 2024 can run 2028 campaigns (new cycle)
- Archives of 2024 campaigns stay available for learning

---

## Implementation Impact

### Minimal Changes Needed

Most of the Campaign Manager Mode specification **stays exactly the same**. The only updates needed:

1. **Agent pool documentation:** Specify 2024/2028 candidates instead of historical/celebrity
2. **Source library examples:** Use real candidates' actual positions
3. **Civic Arena integration:** Clarify how source libraries are shared
4. **Authenticity guardrails:** LLM constraints to stay grounded in real record

### Zero Architecture Changes

- Data models: same
- API: same
- UX flows: same
- LLM efficiency: same
- Cost estimates: same

Just **different agents** (real candidates vs. historical figures).

---

## Bottom Line

**Campaign Manager Mode now uses 2024/2028 presidential candidates** as its agent pool, fully integrated with Civic Arena.

This means:
- ✅ Users learn about real candidates in Civic Arena
- ✅ Users see them debate in Political Arena
- ✅ Users strategically manage them in Campaign Manager
- ✅ Same source libraries, same briefings, same real calendar across all three

**All grounded in reality, all nonpartisan, all educational.**

The entire platform (Civic Arena + Political Arena + Campaign Manager) now centers on **real candidates, real elections, real issues**.

