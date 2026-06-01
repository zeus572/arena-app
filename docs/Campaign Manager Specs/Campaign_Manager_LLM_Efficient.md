# Campaign Manager Mode — LLM-Efficient Architecture

## Principle: Precompute, Don't Generate On-Demand

Rather than generating output per manager request (expensive, slow), we:

1. **Precompute candidate outputs** — When a news event breaks or weekly outputs are due, generate 3–5 options per agent
2. **Store as choices** — Manager picks from precomputed set, doesn't request custom generation
3. **Coach within bounds** — Manager can redirect (refine) existing choice, not request new generations
4. **Scale LLM usage** — Predictable, batched, not per-manager-request

**Result:** ~80–90% fewer LLM calls, faster manager experience, lower cost.

---

## Output Generation Pipeline

### Daily/Weekly Generation (Scheduled, Batch)

```
Timeline:
─────────

Sunday 6am ET:
  └─ For each agent:
     └─ For each expected real event this week (debates, news):
        └─ Generate 3–4 output options (different angles/tones)
           └─ Store as AgentOutputChoice entities
           
Manager sees on dashboard:
─────────────────────────
"Rapid Response to Fed Rate Hike"
├─ Option A: "Economy angle" (focus on jobs)
├─ Option B: "Inequality angle" (billionaires vs. workers)
├─ Option C: "Neutral pragmatism" (acknowledge complexity)
└─ [Pick one]

Manager picks Option B.
│
▼ Agent generates final version of Option B (2–3 second render)

Manager reviews final:
├─ Consistency: 92/100 ✓
├─ Resonance: 87/100 ✓
└─ [✓ Approve] [✏️ Refine] [❌ Kill]

If [✏️ Refine]: "Add specific housing policy detail"
│
▼ ONE additional LLM call to refine within bounds of Option B
│
Final version regenerates with refinement → Manager re-reviews
```

### Precomputation Strategy

#### 1. News Events (Daily, ~30 min before dashboard update)

**Time:** Daily 5pm ET (news cycle closes, before manager check-in)

**Trigger:** New news story hits that's trending in political discourse

**Generation:**
```
For each agent:
  For each trending story today:
    Generate 3 output options:
    - Option A: Aggressive framing (attack opponent / emphasize strength)
    - Option B: Pragmatic framing (acknowledge tradeoff, solution-focused)
    - Option C: Pivot framing (change subject to agent's strength)
    
    Score each:
      - Consistency with agent sources
      - Estimated electorate resonance
      - Engagement prediction
    
    Store as AgentOutputChoice (pending_manager_selection)
```

**Cost:** ~3 options × 6 agents × 1–2 news events/day = ~18–36 LLM calls/day
(Compare: on-demand = 100+ calls/day if managers are active)

#### 2. Weekly Statements (Sunday, Batch)

**Time:** Sunday 6am ET

**Generation:**
```
For each agent:
  Generate 2–3 weekly message themes based on:
    - Messaging priorities set by previous week's managers
    - Polling salience for the week
    - Expected calendar events
    
  Options:
    - Option A: Lead with top-priority issue
    - Option B: Balanced (mix of priorities)
    - Option C: Pivot to emerging issue
    
  Store as AgentOutputChoice (weekly_message)
```

**Cost:** ~3 options × 6 agents = 18 LLM calls/week

#### 3. Debate Prep (Thursday, If Needed)

**Time:** Thursday morning (if real debate is Tue/Wed/Thu)

**Generation:**
```
For each agent debating that week:
  Generate 3–4 opening statements / debate angles:
    - Option A: Offense (attack opponent)
    - Option B: Defense (strengthen position)
    - Option C: Common ground (find agreement)
    - Option D: Pivot (shift topic to strength)
  
  Store as AgentOutputChoice (debate_prep)
```

**Cost:** ~4 options × agents debating = ~8–16 LLM calls/week

### Total LLM Cost (Precomputed)

```
Daily news options:     ~25 calls/day
Weekly statements:      ~18 calls/week = ~2.5 calls/day
Debate prep:            ~12 calls/week = ~1.7 calls/day
─────────────────────────────────────
Total:                  ~29 calls/day (batch, scheduled)

vs. On-demand model:    ~100+ calls/day (per-manager requests, unpredictable)

Savings: ~70% fewer LLM calls
```

---

## Data Model Changes

### New Entity: AgentOutputChoice

```csharp
public class AgentOutputChoice
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid? CampaignWeekId { get; set; }  // Which week (if applicable)
    
    public string OutputType { get; set; }
    // "rapid_response" | "weekly_message" | "debate_prep" | "event_prep"
    
    public string Trigger { get; set; }
    // "fed_rate_hike" | "climate_news" | "weekly_messaging" | "debate_prep"
    
    public int ChoiceNumber { get; set; }
    // 1, 2, 3, 4 (A, B, C, D)
    
    public string ChoiceLabel { get; set; }
    // "Economy angle", "Pragmatism", "Offensive", etc.
    
    public string DraftContent { get; set; }
    // Pre-generated content
    
    // Pre-generated scores
    public decimal ConsistencyWithSources { get; set; }
    public decimal ElectorateResonance { get; set; }
    public decimal EngagementPrediction { get; set; }
    
    public string ChoiceStrategy { get; set; }
    // Explanation of why this angle (for manager education)
    // "This frames the issue as economic inequality, aligning with your agent's
    //  source library and testing resonance with younger voters."
    
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }  // Choices valid for 1–7 days
    
    // Navigation
    public Agent Agent { get; set; }
    public CampaignWeek? Week { get; set; }
    public List<AgentOutput> UsedByOutputs { get; set; }  // Which outputs chose this
}
```

### Modified Entity: AgentOutput

```csharp
public class AgentOutput
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? CampaignWeekId { get; set; }
    
    public string OutputType { get; set; }
    public string Topic { get; set; }
    
    // CHANGED: Link to AgentOutputChoice instead of storing draft
    public Guid? ChosenChoiceId { get; set; }  // FK to AgentOutputChoice
    public AgentOutputChoice? ChosenChoice { get; set; }
    
    // Final content (either from choice or refined from choice)
    public string FinalContent { get; set; }
    
    // Manager coaching
    public string ManagerStatus { get; set; }
    // "choice_pending" | "refining" | "approved" | "published" | "killed"
    
    public string? RefinementFeedback { get; set; }
    // "Add housing policy detail", "Tone down rhetoric", etc.
    
    public int RefineAttempts { get; set; }
    // How many times manager asked for refinement (max 2 per choice)
    
    // Scores (updated with refinement)
    public decimal ConsistencyWithSources { get; set; }
    public decimal ElectorateResonance { get; set; }
    public decimal EngagementPrediction { get; set; }
    
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ActualEngagement { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
```

---

## Manager UX Flow (New)

### Before (On-Demand Generation)

```
Manager reads news → Clicks "Respond" → LLM generates → Manager reviews (slow, expensive)
```

### After (Precomputed Choices)

```
Manager checks dashboard → Sees precomputed choices → Picks one → Reviews final version

DETAILED FLOW:
───────────────

Sunday evening: Manager logs in
                Dashboard shows pending week
                
Wednesday: News breaks (Fed rate hike)
          Manager gets notification: "Rapid response options ready"
          
Manager opens response options:
┌────────────────────────────────────────────┐
│ Rapid Response: Fed Rate Hike              │
│ (3 options generated for your candidate)   │
├────────────────────────────────────────────┤
│                                            │
│ Option A: ECONOMY FOCUS                   │
│ ┌──────────────────────────────────────┐  │
│ │ The Fed's hike hits working families. │  │
│ │ We need to rebuild for everyone.     │  │
│ │                                      │  │
│ │ Consistency: 92/100 ✓                │  │
│ │ Resonance: 87/100 ✓                 │  │
│ │ Predicted engagement: 3.2k reactions │  │
│ │                                      │  │
│ │ [This frames the issue as economic  │  │
│ │  inequality, testing resonance with │  │
│ │  younger voters.]                    │  │
│ │                                      │  │
│ │ [Pick this] [Preview] [Pass]         │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ Option B: PRAGMATISM FOCUS                │
│ ┌──────────────────────────────────────┐  │
│ │ The Fed faces a tough tradeoff:      │  │
│ │ inflation vs. growth. Here's what    │  │
│ │ I'd do differently...                │  │
│ │                                      │  │
│ │ Consistency: 88/100                  │  │
│ │ Resonance: 81/100                   │  │
│ │ Predicted engagement: 2.1k           │  │
│ │                                      │  │
│ │ [This acknowledges the complexity   │  │
│ │  and positions as pragmatic leader.] │  │
│ │                                      │  │
│ │ [Pick this] [Preview] [Pass]         │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ Option C: PIVOT FOCUS                     │
│ ┌──────────────────────────────────────┐  │
│ │ The real issue is corporate greed.   │  │
│ │ Companies raise prices while fed     │  │
│ │ tightens. Here's the solution...     │  │
│ │                                      │  │
│ │ Consistency: 94/100 ✓                │  │
│ │ Resonance: 72/100                   │  │
│ │ Predicted engagement: 4.1k           │  │
│ │                                      │  │
│ │ [This pivots to your agent's        │  │
│ │  strength (corporate accountability) │  │
│ │  but may feel like dodging question.]│  │
│ │                                      │  │
│ │ [Pick this] [Preview] [Pass]         │  │
│ └──────────────────────────────────────┘  │
│                                            │
└────────────────────────────────────────────┘

Manager thinks: "Option A feels right. Strong, on-message, resonates."
Manager clicks: [Pick this]
│
▼ Final version renders (fast, from pre-scored draft)
│
┌────────────────────────────────────────────┐
│ Final Review: Option A (Economy Focus)     │
├────────────────────────────────────────────┤
│                                            │
│ The Fed's interest rate hikes are         │
│ crushing working families. We need to     │
│ rebuild an economy that works for         │
│ everyone, not just the billionaire class. │
│                                            │
│ Scores:                                   │
│ Consistency: 92/100 ✓                     │
│ Resonance: 87/100 ✓                       │
│ Engagement: 3.2k predicted                │
│                                            │
│ [✓ Approve] [✏️ Refine] [❌ Kill]         │
│                                            │
└────────────────────────────────────────────┘

Manager options:

1. [✓ Approve] → Publish immediately
   Cost: 0 LLM calls
   
2. [✏️ Refine] → "Add specific housing cost policy detail"
   Cost: 1 LLM call (refine within bounds of Option A)
   
   New version renders in 5–10 seconds:
   ┌─────────────────────────────────────────┐
   │ The Fed's rate hikes crush families.    │
   │ Rent is up 15% in 2 years. We need to   │
   │ build 1M affordable homes and cap       │
   │ corporate rent-setting. Here's how...   │
   │                                         │
   │ [✓ Approve] [✏️ Refine Again] [❌ Kill] │
   │ (Note: Can refine up to 2x per choice)  │
   └─────────────────────────────────────────┘
   
3. [❌ Kill] → Don't publish, try different option later
   Cost: 0 LLM calls
```

---

## Choice Refinement (Bounded LLM Use)

Managers can refine a chosen option up to **2 times** per choice.

```
Manager picks Option A: Economy Focus
  │
  ├─ Refine #1: "Add housing policy"
  │  ├─ LLM regenerates Option A with housing detail
  │  ├─ Scores recalculated
  │  └─ Manager re-reviews
  │
  ├─ If approved: Done. Publish.
  │
  └─ If manager refines again:
     ├─ Refine #2: "Tone down anti-billionaire rhetoric"
     ├─ LLM regenerates with tone adjustment
     └─ Scores recalculated
        │
        ├─ If approved: Done. Publish.
        │
        └─ If manager wants more: "Sorry, max 2 refinements per choice.
                                   Pick a different option or approve as-is."
```

**Why 2 refinements?**
- Balances manager customization with LLM cost
- Prevents endless iteration
- Teaches decision-making (commit or move on)

---

## API Changes

### GET /api/campaigns/:id/pending-choices
Get precomputed output choices for the manager.

**Response:**
```json
{
  "weekNumber": 24,
  "pendingChoices": [
    {
      "id": "choice-uuid-1",
      "outputType": "rapid_response",
      "trigger": "fed_rate_hike",
      "choices": [
        {
          "choiceNumber": 1,
          "label": "Economy Focus",
          "strategy": "This frames the issue as economic inequality...",
          "scores": {
            "consistency": 92,
            "resonance": 87,
            "engagement": 3200
          }
        },
        {
          "choiceNumber": 2,
          "label": "Pragmatism Focus",
          "strategy": "This acknowledges the Fed's tradeoff...",
          "scores": { ... }
        },
        {
          "choiceNumber": 3,
          "label": "Pivot Focus",
          "strategy": "This shifts to corporate accountability...",
          "scores": { ... }
        }
      ]
    }
  ]
}
```

### POST /api/campaigns/:id/outputs/choose
Manager picks a precomputed choice.

**Request:**
```json
{
  "choiceId": "choice-uuid-1",
  "selectedChoice": 1,
  "decision": "approve" | "refine"
}
```

**Response (if approve):**
```json
{
  "success": true,
  "outputId": "output-uuid",
  "finalContent": "...",
  "status": "approved",
  "publishedAt": "2024-06-27T14:45:00Z"
}
```

**Response (if refine):**
```json
{
  "success": true,
  "outputId": "output-uuid",
  "status": "refining",
  "refinementFeedback": "Add housing policy detail",
  "message": "Agent is refining within this choice. New version in 5–10 seconds."
}
```

### POST /api/campaigns/:id/outputs/:outputId/refine
Manager refines a chosen option.

**Request:**
```json
{
  "feedback": "Add specific housing cost policy detail",
  "refinement_number": 1
}
```

**Response:**
```json
{
  "success": true,
  "outputId": "uuid",
  "status": "refinement_ready",
  "newContent": "...",
  "updatedScores": {
    "consistency": 93,
    "resonance": 89,
    "engagement": 3400
  },
  "message": "Refined version ready for review. (1 of 2 refinements used)"
}
```

---

## Precomputation Scheduling

### Batch Job: Daily News Response Generation

**Time:** 5:00pm ET daily

**Steps:**
1. Query trending political stories (from news service)
2. For each trending story:
   - For each agent:
     - Generate 3 response options (aggressive, pragmatic, pivot)
     - Score each
     - Store as AgentOutputChoice
3. Notify managers: "New rapid response options available"

**Cost:** ~18–36 LLM calls/day

**Code sketch:**
```csharp
public class PrecomputeNewsResponses : BackgroundJob
{
    public async Task Execute()
    {
        var trendingStories = await _newsService.GetTrendingAsync();
        
        foreach (var story in trendingStories)
        {
            foreach (var agent in _agents.GetAll())
            {
                var choices = await _llmService.GenerateNewsResponseChoicesAsync(
                    agent,
                    story,
                    numberOfChoices: 3  // Aggressive, pragmatic, pivot
                );
                
                foreach (var choice in choices)
                {
                    await _db.AgentOutputChoices.AddAsync(new AgentOutputChoice
                    {
                        AgentId = agent.Id,
                        OutputType = "rapid_response",
                        Trigger = story.Id,
                        ChoiceNumber = choice.Number,
                        ChoiceLabel = choice.Label,
                        DraftContent = choice.Content,
                        ConsistencyWithSources = choice.Scores.Consistency,
                        ElectorateResonance = choice.Scores.Resonance,
                        EngagementPrediction = choice.Scores.Engagement,
                        ExpiresAt = DateTime.UtcNow.AddDays(7)
                    });
                }
            }
        }
        
        await _db.SaveChangesAsync();
        await _notificationService.NotifyManagersOfNewChoices();
    }
}
```

### Batch Job: Weekly Message Generation

**Time:** Sunday 6:00am ET

**Steps:**
1. For each agent:
   - Fetch previous week's manager messaging priorities (aggregate across all managers)
   - Fetch current week's polling salience
   - Generate 2–3 message theme options
   - Score and store

**Cost:** ~18 LLM calls/week

### Batch Job: Debate Prep Generation

**Time:** Thursday 8:00am ET (if debate is coming)

**Steps:**
1. Query real events for upcoming debates
2. For each agent with a scheduled debate:
   - Generate 3–4 opening strategies (offense, defense, common ground, pivot)
   - Score and store

**Cost:** ~12–16 LLM calls/week

---

## LLM Cost Comparison

### On-Demand Model (Not Recommended)

```
Manager reads news
  └─ Clicks "Rapid Response"
  └─ Requests custom generation
  └─ LLM generates 1 output
  └─ Manager reviews
  └─ Manager wants refinement
  └─ Another LLM call

If 200 active managers, each making 3 daily requests:
  200 × 3 = 600 LLM calls/day
  × 30 days = 18,000 calls/month
  × $0.01 per call ≈ $180/month (at Claude 3.5 Sonnet prices)

Unpredictable spikes during major news.
```

### Precomputed Choices Model (Recommended)

```
Daily news responses:       ~25 calls/day
Weekly messages:            ~2.5 calls/day
Debate prep:                ~1.7 calls/day
Manager refinements (10%):  ~3 calls/day
─────────────────────────────────────
Total:                      ~32 calls/day
                            ~960 calls/month
                            ~$10/month

Predictable, scheduled, batch.
80% cost reduction.
```

**Additional benefit:** Managers see choices instantly (precomputed) vs. waiting 30–60 seconds (on-demand generation).

---

## Manager Experience Improvement

### Speed
- **Before:** Manager requests generation → waits 30–60s → reviews
- **After:** Manager sees choices instantly → picks → reviews in 5s → done

### Transparency
- **Before:** "An agent generated this for you"
- **After:** "3 precomputed angles (economy, pragmatism, pivot). Here's the strategy for each. Pick one."

Manager learns *why* different angles exist and what tradeoff each represents.

### Coaching Depth
- **Before:** Approve or request regeneration (limited agency)
- **After:** Pick an angle, then refine (2x) within that angle

Manager has more nuanced control without unlimited LLM spend.

---

## Fallback: If No Precomputed Choice Matches

Occasionally, a real event happens that wasn't anticipated (major breaking news, unexpected resignation, etc.).

**Fallback flow:**
```
Manager opens dashboard
  "No precomputed choices for this event."
  └─ [Wait for precomputed choices (next batch, ~30 min)]
  └─ [Generate on-demand (costs 1 LLM call now, 30s wait)]

Manager picks: [Generate on-demand]
  │
  ▼ LLM generates 1 output (blocking call, 30s)
  │
  ▼ Manager reviews (now has a choice, can refine 2x if needed)
```

**Guardrail:** Rate limit on-demand generation to prevent abuse.
- Max 1 on-demand per manager per day
- Or require a "prep budget" point spend (ties back to weekly budget allocation)

---

## Dashboard Changes

### Manager Dashboard Update

**Before:** "Pending Approvals (3 outputs)"

**After:** 
```
Pending Choices (3)
├─ Rapid Response: Fed Rate Hike (3 options)
├─ Weekly Message Theme (2 options)
└─ Upcoming Debate Prep (4 options)

Pending Approvals (1)
└─ [Already chose debate option, awaiting final review]
```

Manager clearly sees:
- Choices they haven't made yet
- Outputs they've already chosen (pending final approval)

---

## Training / Education

Include brief tooltips explaining *why* different choices exist:

```
Option A: ECONOMY FOCUS
"This frames the issue as economic inequality, emphasizing your agent's
core message. Tests resonance with younger voters (higher engagement
prediction: 3.2k). Consistent with source library (92/100)."

Option B: PRAGMATISM FOCUS
"This acknowledges real tradeoffs and positions as pragmatic leader.
More measured tone may appeal to swing voters. Lower engagement
prediction (2.1k) but stronger with moderates."

Option C: PIVOT FOCUS
"This shifts focus to corporate accountability, your agent's strength.
Highest consistency (94/100) but may look like dodging the Fed question
(lower resonance: 72/100)."
```

Managers learn strategic communication through choosing, not generating.

---

## Implementation Phases

### Phase 1: Core (Weeks 1–4)
- Campaign, budget, support simulation
- Manual output (hardcoded, no LLM)
- No precomputation yet

### Phase 2: Precomputed Choices (Weeks 5–6)
- Implement AgentOutputChoice entity
- Build daily news batch job (precompute 3 choices per story per agent)
- Update manager dashboard to show choices
- Implement choice selection flow

**Success:** Managers see precomputed choices, pick from them.

### Phase 3: Refinement (Week 7)
- Implement refinement flow (manager can refine 2x per choice)
- Add LLM call for refinement
- Update scoring after refinement

### Phase 4: Weekly + Debate Prep (Week 8)
- Add weekly message generation batch job
- Add debate prep batch job
- Full scheduling and notifications

---

## Monitoring & Alerting

Track LLM usage to ensure it stays low:

```
Metrics:
─────
- Daily LLM calls (target: <40 calls/day)
- LLM cost per week (target: <$10/week)
- Manager choice response time (target: <2s)
- Manager refinement acceptance rate (% of managers who refine: should be 10–20%)
- Choice expiration rate (% of choices never picked: should be <20%)

Alerts:
───────
- If daily calls exceed 60 → investigate unusual load
- If cost exceeds $15/week → review batch jobs
- If manager response time > 5s → check database query
- If choice expiration > 40% → reassess choice quality/relevance
```

---

## FAQ: Why Precomputation?

### Q: Won't managers feel constrained by 3 choices?

**A:** Research shows 3–5 options is ideal for decision-making (reduces decision paralysis). Managers also have refinement (2x per choice), so they still have customization. It's not "zero or many"; it's "structured choice with refinement."

### Q: What if a manager hates all 3 precomputed choices?

**A:** They can:
1. Wait for the next batch (~30 min for news, daily for weekly)
2. Spend 1 on-demand LLM call (rate-limited)
3. Kill the output and try a different approach

The vast majority of cases will be covered by precomputation.

### Q: Doesn't this reduce manager agency?

**A:** No. Managers are making strategy decisions (which angle to emphasize, how to refine), not generation decisions. Refinement and killing outputs preserve agency within a bounded system.

### Q: What about niche agents or edge cases?

**A:** Start with 6 major agents (Bernie, Trump, AOC, Hamilton, Lincoln, Franklin). Each gets full precomputation. For future agents, we add them to the batch job.

### Q: Can we A/B test precomputation vs. on-demand?

**A:** Yes. In beta, run both: some users get precomputed choices (fast, cheap), others get on-demand (slower, more expensive). Measure engagement, satisfaction, cost. Almost certainly precomputation wins.

---

## Conclusion

**Precomputed choices with bounded refinement** achieves:

✓ **Cost:** ~80% reduction in LLM calls (~$10/week vs. $180/week)
✓ **Speed:** Instant choice availability vs. 30–60s generation waits
✓ **UX:** Clear strategy education (why these angles?)
✓ **Agency:** Managers choose and refine, not just approve
✓ **Scalability:** Predictable batch jobs, no per-request generation

This is the right tradeoff between manager customization and LLM cost.

