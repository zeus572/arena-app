# Campaign Manager Mode — Data Models & API Specification

## Data Models (EF Core)

### Campaign

```csharp
public class Campaign
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AgentId { get; set; }
    public int SeasonYear { get; set; }  // 2024, 2028, etc.
    
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; }  // "active" | "archived" | "paused"
    public string Difficulty { get; set; } = "Normal";  // "Easy" | "Normal" | "Hard" — set at creation, scales weekly volatility
    
    // Campaign state
    public decimal SimulatedSupport { get; set; }  // 0.0–100.0
    public List<string> MessagingPriorities { get; set; } = new();
    public decimal BudgetRemaining { get; set; }
    public decimal BudgetSpent { get; set; }
    
    // Coaching history
    public int TotalApprovals { get; set; }
    public int TotalRedirects { get; set; }
    public decimal MessageDisciplineScore { get; set; }
    
    // Metadata
    public string? CampaignName { get; set; }
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public User User { get; set; }
    public Agent Agent { get; set; }
    public List<CampaignWeek> Weeks { get; set; } = new();
    public List<AgentOutput> AgentOutputs { get; set; } = new();
    public List<ManagerDecision> Decisions { get; set; } = new();
    public List<CampaignRealEvent> RealEvents { get; set; } = new();
}
```

### CampaignWeek

```csharp
public class CampaignWeek
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public int WeekNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    // Weekly state
    public decimal InitialSupport { get; set; }
    public decimal FinalSupport { get; set; }
    public decimal SupportChange => FinalSupport - InitialSupport;
    
    // Manager decisions (stored as JSON)
    public string MessagingPrioritiesJson { get; set; }  // ["economy", "healthcare"]
    public string BudgetAllocationJson { get; set; }    // { "ads": 40, "ground": 25, ... }
    public string EventsParticipatedJson { get; set; }  // [debate-id-1, debate-id-2]
    
    // Scoring
    public decimal EngagementScore { get; set; }
    public decimal MessageDisciplineScore { get; set; }
    public decimal EventROI { get; set; }
    public decimal ResponsiveScore { get; set; }
    public decimal OverallScore { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public Campaign Campaign { get; set; }
    public List<ManagerDecision> Decisions { get; set; } = new();
    public List<AgentOutput> Outputs { get; set; } = new();
}
```

### BudgetAllocation (Value Object)

```csharp
public class BudgetAllocation
{
    public int TotalPoints { get; set; } = 100;
    public int Ads { get; set; }
    public int GroundGame { get; set; }
    public int Digital { get; set; }
    public int Prep { get; set; }
    public int Opposition { get; set; }
    
    public int Total => Ads + GroundGame + Digital + Prep + Opposition;
    
    public bool IsValid =>
        Total <= TotalPoints &&
        Ads <= 60 && GroundGame <= 60 && Digital <= 60 && Prep <= 60 && Opposition <= 60;
    
    public Dictionary<string, decimal> GetROIMultipliers(List<CampaignWeek> history)
    {
        // Calculate historical ROI for each channel
        // Used in budget allocation UI to show trends
        var rois = new Dictionary<string, decimal>();
        // ... calculation logic
        return rois;
    }
}
```

### ManagerDecision

```csharp
public class ManagerDecision
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? CampaignWeekId { get; set; }
    
    public string DecisionType { get; set; }
    // "messaging_priority" | "budget_allocation" | "event_participation" | "output_coaching" | "rapid_response"
    
    public string Choice { get; set; }     // What they picked (JSON for complex choices)
    public string? Rationale { get; set; } // Why (user-provided notes)
    public string? Outcome { get; set; }   // What happened (system-populated after resolution)
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public Campaign Campaign { get; set; }
    public CampaignWeek? Week { get; set; }
}
```

### AgentOutput

```csharp
public class AgentOutput
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? CampaignWeekId { get; set; }
    
    public string OutputType { get; set; }
    // "tweet" | "statement" | "debate_prep" | "rapid_response" | "event_prep"
    
    public string Topic { get; set; }
    public string DraftContent { get; set; }
    
    // Manager coaching
    public string ManagerStatus { get; set; }
    // "pending_approval" | "approved" | "redirected" | "regenerating" | "published"
    
    public string? ManagerFeedback { get; set; }
    public string? FinalContent { get; set; }
    
    // Scoring
    public decimal ConsistencyWithSources { get; set; }   // 0–100
    public decimal ElectorateResonance { get; set; }      // 0–100
    public decimal EngagementPrediction { get; set; }     // 0–10000 reactions
    
    // Publishing
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ActualEngagement { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public Campaign Campaign { get; set; }
    public CampaignWeek? Week { get; set; }
}
```

### CampaignRealEvent

```csharp
public class CampaignRealEvent
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    
    public string RealEventType { get; set; }
    // "debate" | "primary" | "news_trend" | "poll_release" | "convention" | "election_day"
    
    public DateTime RealEventDate { get; set; }
    public string Description { get; set; }
    public string? Context { get; set; }  // Poll data, news snippet, etc.
    
    // Manager engagement
    public bool ManagerResponded { get; set; }
    public string? ManagerChoice { get; set; }
    // "participate" | "skip" | "rapid_response" | "prepare"
    
    public Guid? DebateId { get; set; }  // If manager entered a debate
    
    // Outcome
    public string? OutcomeDescription { get; set; }
    public decimal? SupportImpact { get; set; }  // +/- points
    public int? EngagementGenerated { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    // Navigation
    public Campaign Campaign { get; set; }
    public Debate? Debate { get; set; }
}
```

### Messaging Priority (Value Object)

```csharp
public static class MessagingPriorityOptions
{
    public const string Economy = "economy";
    public const string Healthcare = "healthcare";
    public const string Climate = "climate";
    public const string Immigration = "immigration";
    public const string Education = "education";
    public const string DefenseStrategy = "defense_strategy";
    public const string Civility = "civility";
    public const string Culture = "culture";
    
    public static Dictionary<string, decimal> WeightFromPolling(DateTime date)
    {
        // Fetch real polling data for the week
        // Return: { "economy": 3.2x, "climate": 1.1x, ... }
        // Used in scoring AgentOutput resonance
    }
}
```

---

## API Endpoints

### Campaign Management

#### POST /api/campaigns
Create a new campaign.

**Request:**
```json
{
  "agentId": "uuid",
  "campaignName": "Bernie's Revolution",
  "seasonYear": 2024
}
```

**Response:**
```json
{
  "id": "uuid",
  "agentId": "uuid",
  "agentName": "Bernie Sanders",
  "campaignName": "Bernie's Revolution",
  "status": "active",
  "simulatedSupport": 25.0,
  "messagingPriorities": [],
  "budgetRemaining": 0,
  "messagesDisciplineScore": 0,
  "createdAt": "2024-06-23T12:00:00Z"
}
```

#### GET /api/campaigns/:id
Retrieve a campaign (manager view).

**Response:**
```json
{
  "id": "uuid",
  "agentName": "Bernie Sanders",
  "agentBio": "...",
  "agentSources": [...],
  "campaignName": "Bernie's Revolution",
  "status": "active",
  
  "currentWeekNumber": 24,
  "startDate": "2024-06-23T00:00:00Z",
  "endDate": null,
  
  "simulatedSupport": 35.9,
  "supportTrend": "+1.7%",
  "messagesDisciplineScore": 91,
  
  "budgetRemaining": 100,
  "budgetSpent": 2300,
  "budgetROI": 1.94,
  
  "totalApprovals": 156,
  "totalRedirects": 28,
  
  "campaignLog": [
    { "weekNumber": 24, "decisionsCount": 7, "supportChange": 1.7, "overallScore": 87.3 },
    { "weekNumber": 23, "decisionsCount": 6, "supportChange": -0.3, "overallScore": 81.1 },
    ...
  ]
}
```

#### GET /api/campaigns/:id/brief
Weekly brief for the manager.

**Query params:** `?weekNumber=24` (defaults to current)

**Response:**
```json
{
  "campaignId": "uuid",
  "weekNumber": 24,
  "weekRange": { "start": "2024-06-23", "end": "2024-06-29" },
  
  "agent": { "id": "uuid", "name": "Bernie Sanders", "currentSupport": 35.9 },
  
  "calendar": [
    { "date": "2024-06-25", "eventType": "debate", "description": "Real CNN Democratic Debate", "status": "upcoming" },
    { "date": "2024-06-28", "eventType": "poll_release", "description": "New CNN/SSRS National Poll", "status": "upcoming" }
  ],
  
  "issueSalience": {
    "economy": { "salience": 71, "trend": "+8", "agentStrength": "strong" },
    "healthcare": { "salience": 58, "trend": "-2", "agentStrength": "strong" },
    "climate": { "salience": 43, "trend": "-3", "agentStrength": "medium" },
    "immigration": { "salience": 52, "trend": "stable", "agentStrength": "weak" }
  },
  
  "lastWeekROI": { "ads": 2.1, "groundGame": 1.3, "digital": 0.8, "prep": 1.6 },
  
  "alerts": [
    { "type": "news", "content": "Trump tariff announcement. Rapid response opportunity?" },
    { "type": "debate_prep", "content": "CNN debate in 2 days. Allocate prep budget?" }
  ]
}
```

### Budget & Messaging

#### POST /api/campaigns/:id/weekly-strategy
Set messaging priorities and allocate budget for the week.

**Request:**
```json
{
  "weekNumber": 24,
  "messagingPriorities": ["economy", "healthcare"],
  "budgetAllocation": {
    "ads": 40,
    "groundGame": 25,
    "digital": 10,
    "prep": 20,
    "opposition": 5
  },
  "rationale": "Economy is trending hot. Healthcare differentiates us."
}
```

**Response:**
```json
{
  "success": true,
  "weekNumber": 24,
  "messagingPriorities": ["economy", "healthcare"],
  "budgetAllocation": { ... },
  "validation": { "isValid": true, "constraints": [] },
  "message": "Budget allocated. Messaging priorities set."
}
```

#### POST /api/campaigns/:id/messaging/adjust
Update messaging priorities mid-week (limited adjustments allowed).

**Request:**
```json
{
  "weekNumber": 24,
  "newPriorities": ["immigration"],
  "reason": "Breaking: Immigration policy shift in news"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Priorities updated. Note: Changing mid-week may reduce ROI."
}
```

### Agent Output Coaching

#### GET /api/campaigns/:id/pending-outputs
Get all pending AgentOutputs awaiting manager approval.

**Response:**
```json
[
  {
    "id": "uuid",
    "outputType": "rapid_response",
    "topic": "Fed Rate Hike Announcement",
    "draftContent": "The Fed's interest rate hike is crushing working families...",
    "scores": {
      "consistencyWithSources": 92,
      "electorateResonance": 87,
      "engagementPrediction": 3200
    },
    "status": "pending_approval",
    "createdAt": "2024-06-27T14:30:00Z"
  }
]
```

#### POST /api/campaigns/:id/outputs/:outputId/approve
Approve and publish an AgentOutput.

**Request:**
```json
{
  "choice": "approve"
}
```

**Response:**
```json
{
  "success": true,
  "outputId": "uuid",
  "finalContent": "...",
  "publishedAt": "2024-06-27T14:45:00Z"
}
```

#### POST /api/campaigns/:id/outputs/:outputId/redirect
Redirect output with coaching feedback.

**Request:**
```json
{
  "choice": "redirect",
  "feedback": "Good on economics, but add specific housing policy. Differentiate from Warren."
}
```

**Response:**
```json
{
  "success": true,
  "outputId": "uuid",
  "status": "regenerating",
  "message": "Agent is regenerating with your feedback. You'll see the new draft in 30 seconds."
}
```

#### POST /api/campaigns/:id/outputs/:outputId/regenerate
Start over with a new angle.

**Request:**
```json
{
  "choice": "regenerate",
  "newAngle": "Focus on housing affordability instead of billionaire rhetoric"
}
```

**Response:**
```json
{
  "success": true,
  "outputId": "uuid",
  "status": "regenerating",
  "newDraftIncoming": true
}
```

### Event Participation

#### GET /api/campaigns/:id/upcoming-events
Get upcoming real events the manager can participate in.

**Response:**
```json
[
  {
    "id": "uuid",
    "eventType": "debate",
    "description": "Real CNN Democratic Debate",
    "date": "2024-06-25T20:00:00Z",
    "expectedTopics": ["economy", "healthcare", "climate"],
    "managerAlignment": {
      "messagingPriorities": ["economy", "healthcare"],
      "alignedTopics": 2
    },
    "prepRecommendation": 20,
    "status": "upcoming_decision_needed"
  }
]
```

#### POST /api/campaigns/:id/event-participation
Decide whether to participate in a real event.

**Request:**
```json
{
  "eventId": "uuid",
  "decision": "participate",
  "debateFormat": "standard",
  "prepBudget": 20
}
```

**Response:**
```json
{
  "success": true,
  "eventId": "uuid",
  "debateId": "newly-created-debate-uuid",
  "prepBudget": 20,
  "message": "Campaign scheduled for debate on June 25. Debate prep starts now."
}
```

Alternatively:
```json
{
  "decision": "skip",
  "reason": "Don't expect favorable topics"
}
```

or:

```json
{
  "decision": "rapid_response",
  "prepBudget": 5
}
```

### Campaign Performance

#### GET /api/campaigns/:id/performance
Get aggregated performance metrics.

**Response:**
```json
{
  "campaignId": "uuid",
  "agentName": "Bernie Sanders",
  "weeksCompleted": 24,
  "finalSupport": 35.9,
  "supportTrend": {
    "weeks": [25.0, 26.3, 27.1, 28.5, 29.2, 30.1, 31.4, 32.0, 32.8, 33.5, 34.2, 35.9],
    "peak": 35.9,
    "peakWeek": 24,
    "low": 25.0,
    "lowWeek": 1
  },
  
  "messagesDiscipline": 89,
  "budgetROI": 1.94,
  "engagementTotal": 187400,
  "debatesParticipated": 8,
  "debatesWon": 3,
  "debatesLost": 2,
  "debatesCompromised": 3,
  
  "decisions": {
    "totalApprovals": 156,
    "totalRedirects": 28,
    "approvalRate": 0.85,
    "averageDecisionsPerWeek": 7
  },
  
  "bestWeek": { "weekNumber": 18, "supportChange": 2.8, "score": 94.2 },
  "worstWeek": { "weekNumber": 12, "supportChange": -1.5, "score": 68.1 },
  
  "campaignScore": 87.3
}
```

#### GET /api/campaigns/:id/decision-log
Full log of all manager decisions.

**Query params:** `?weekNumber=24&decisionType=all`

**Response:**
```json
[
  {
    "weekNumber": 24,
    "decisionType": "messaging_priority",
    "choice": ["economy", "healthcare"],
    "rationale": "Economy trending hot.",
    "timestamp": "2024-06-23T18:00:00Z"
  },
  {
    "weekNumber": 24,
    "decisionType": "budget_allocation",
    "choice": { "ads": 40, "groundGame": 25, "digital": 10, "prep": 20, "opposition": 5 },
    "timestamp": "2024-06-23T18:15:00Z"
  },
  {
    "weekNumber": 24,
    "decisionType": "output_coaching",
    "choice": "redirect",
    "feedback": "Add housing policy.",
    "timestamp": "2024-06-27T14:30:00Z"
  }
]
```

---

## Real-World Data Integration

### Polling Integration

Service: `PollingDataService`

```csharp
public interface IPollingDataService
{
    // Fetch real polling data weekly
    Task<Dictionary<string, decimal>> GetIssueSalienceAsync(DateTime weekOf);
    
    // Example output:
    // { "economy": 71, "healthcare": 58, "climate": 43, ... }
    
    Task<IssuePolling> GetDetailedPollingAsync(string issue, DateTime weekOf);
    // Returns: trend, change from previous week, demographic breakdown
}
```

**Used in:**
- Weekly brief (issue salience display)
- AgentOutput scoring (resonance vs. polling priorities)
- Support movement simulation (weeks where output aligns with polling spike gain multipliers)

### News Cycle Integration

Service: `NewsCycleService`

```csharp
public interface INewsCycleService
{
    // Fetch trending political stories
    Task<List<NewsEvent>> GetTrendingTopicsAsync(DateTime? date = null);
    
    // Example output:
    // { topic: "Fed Rate Hike", trend: "↑ breaking", relevantAgentSources: [sources] }
    
    Task<NewsEvent> GetEventContextAsync(string eventName);
    // Returns: full context, related issues, expected debate topics
}
```

**Used in:**
- Weekly brief (alerts section)
- Rapid response prompts
- Real event expected topics

### Calendar Integration

Service: `CalendarService`

```csharp
public interface ICalendarService
{
    // Real election calendar
    Task<List<CampaignEvent>> GetRealEventsAsync(DateTime start, DateTime end);
    
    // Example output:
    // { date: "2024-06-25", eventType: "debate", description: "CNN Democratic Debate" }
    
    Task<List<CampaignEvent>> GetUpcomingEventsAsync(int weeksAhead = 4);
}
```

**Used in:**
- Weekly brief (calendar section)
- Event participation decisions
- Debate scheduling

---

## Simulation Engine

### Support Movement Calculation

`SupportSimulation` service

```csharp
public class SupportSimulation
{
    public decimal CalculateWeeklyMovement(
        Campaign campaign,
        CampaignWeek week,
        List<AgentOutput> publishedOutputs,
        Dictionary<string, decimal> issueSalience,
        List<Debate> participatedDebates)
    {
        decimal baseChange = 0m;
        
        // 1. Messaging alignment
        foreach (var output in publishedOutputs)
        {
            var topicSalience = issueSalience.GetValueOrDefault(output.Topic, 1.0m);
            var alignment = output.ElectorateResonance / 100m;
            baseChange += alignment * topicSalience * 0.01m;  // Up to 1% per strong output
        }
        
        // 2. Event performance
        foreach (var debate in participatedDebates)
        {
            var outcome = debate.Outcome;  // "won" | "lost" | "compromised"
            var impact = outcome switch
            {
                "won" => 1.5m,
                "compromised" => 0.5m,
                "lost" => -0.8m,
                _ => 0m
            };
            baseChange += impact * 0.01m;
        }
        
        // 3. Budget ROI multiplier
        var budgetROI = CalculateBudgetROI(week.BudgetAllocationJson, issueSalience);
        baseChange *= (0.8m + budgetROI * 0.2m);  // 20% impact from budget efficiency
        
        // 4. Message discipline bonus/penalty
        var disciplineMultiplier = week.MessageDisciplineScore / 100m;
        baseChange *= disciplineMultiplier;
        
        // 5. Difficulty volatility multiplier
        // All managers start at the same support level; difficulty only scales
        // the MAGNITUDE of weekly swings, not the direction. Same decisions ->
        // same direction of movement; harder difficulty amplifies both gains and losses.
        var difficultyMultiplier = campaign.Difficulty switch
        {
            "Easy" => 0.5m,    // Forgiving: ±0.5–1% swings
            "Normal" => 1.0m,  // Balanced: ±1–2% swings
            "Hard" => 2.0m,    // High-stakes: ±2–4% swings
            _ => 1.0m
        };
        baseChange *= difficultyMultiplier;
        
        return baseChange;
    }
    
    private decimal CalculateBudgetROI(
        string budgetAllocationJson,
        Dictionary<string, decimal> issueSalience)
    {
        var allocation = JsonSerializer.Deserialize<BudgetAllocation>(budgetAllocationJson);
        
        // Channels with historical data
        var channelROI = new Dictionary<string, decimal>
        {
            ["ads"] = 2.0m,        // Baseline
            ["groundGame"] = 1.5m,
            ["digital"] = 1.2m,
            ["prep"] = 1.4m,
            ["opposition"] = 0.9m
        };
        
        var weightedROI = (
            allocation.Ads * channelROI["ads"] +
            allocation.GroundGame * channelROI["groundGame"] +
            allocation.Digital * channelROI["digital"] +
            allocation.Prep * channelROI["prep"] +
            allocation.Opposition * channelROI["opposition"]
        ) / (decimal)allocation.Total;
        
        return weightedROI;
    }
}
```

---

## Architecture Notes

### Storage

- Campaign, CampaignWeek, ManagerDecision, AgentOutput, CampaignRealEvent → SQL (primary)
- Real-time polling, news, calendar data → cached in Redis, refreshed daily
- Campaign decision log → append-only for audit trail

### Real-Time Updates

- WebSocket subscriptions for:
  - New AgentOutputs (pending approval)
  - Real event updates (debate tonight, poll dropped)
  - Support movement (updated at end of week)

### Isolation

- Campaign data is user-private (no other manager sees decision logs, performance, etc.)
- Only aggregated, anonymized campaign stats surface globally ("top performing campaigns this week")

