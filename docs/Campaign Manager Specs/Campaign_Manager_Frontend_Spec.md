# Campaign Manager Mode — Frontend UX & Component Specification

## Overview

The frontend for Campaign Manager Mode consists of two complementary experiences:

1. **Global Arena** (existing) — Feed, leaderboards, spectating
2. **Campaign War Room** (new) — Private manager dashboard for one campaign

Users can toggle between them seamlessly. A campaign manager will spend most time in the War Room but may reference the global feed for context and rival campaigns.

---

## Navigation & Layout

### Main App Shell (Modified)

```
┌─────────────────────────────────────────────────────┐
│ Logo   Arena | My Campaign | Briefings | Profiles   │ (Top nav)
└─────────────────────────────────────────────────────┘
│                                                       │
│  [Left Sidebar]          [Main Content]              │
│  - My Campaign                                        │
│  - Campaign List (...)                               │
│  - Arena                                              │
│  - Learn                                              │
│                                                       │
│  Selected: "My Campaign" →  War Room Dashboard       │
└─────────────────────────────────────────────────────┘
```

**New navigation item:** "My Campaign" — takes you to your active campaign's war room. Visible only if you have an active campaign.

---

## Page: Campaign Selection / Launch

**Route:** `/campaigns` or `/campaign-manager`

When a user first visits or has no active campaign.

### UI Structure

**Hero section (top 40% of viewport):**

```
╔════════════════════════════════════════╗
║  Manage an AI Campaign in Real Time    ║
║  ─────────────────────────────────     ║
║  Pick an agent. Make decisions. See    ║
║  results unfold against the real 2024  ║
║  campaign calendar. Your strategy, not ║
║  algorithms, moves the needle.         ║
║                                        ║
║  [Primary CTA: Start a Campaign]       ║
╚════════════════════════════════════════╝
```

**Campaign cards (agent picker):**

A grid of agent cards. Each shows:
- Agent name + portrait
- Quick bio (1 line)
- "Agent type" badge (Celebrity | Historical | Original)
- Source library preview (e.g., "4 primary sources")
- CTA: "Start Campaign with [Name]"

Example layout:
```
┌──────────────┬──────────────┬──────────────┐
│ Bernie       │ Trump        │ Obama        │
│ Sanders      │              │              │
│              │              │              │
│ Senator,     │ Business,    │ Former       │
│ Vermont      │ Real Estate  │ President    │
│              │              │              │
│ Historical   │ Celebrity    │ Celebrity    │
│ Socialist    │ Populist     │ Centrist     │
│              │              │              │
│ Sources: 7   │ Sources: 6   │ Sources: 5   │
│              │              │              │
│ [Start]      │ [Start]      │ [Start]      │
└──────────────┴──────────────┴──────────────┘
```

**Active campaigns section (below):**

If the user has archived or paused campaigns:
```
Your Past Campaigns
─────────────────────────────────────
Bernie's Revolution (2024) — Completed
  Final Support: 35.9% | Score: 87.3
  [Resume] [View Results]

Lincoln's Union (2024) — Paused
  Current Support: 42.1% | Week 8 of 12
  [Resume] [Archive]
```

### Agent Picker Modal

When clicking "Start Campaign with [Agent]", a dialog:

```
╔══════════════════════════════════════╗
║ Start a Campaign with Bernie Sanders ║
║ ──────────────────────────────────   ║
║                                      ║
║ Campaign Name (optional)             ║
║ [___________________]                ║
║ (e.g., "Bernie's Revolution")        ║
║                                      ║
║ Season                               ║
║ [2024 ▼]                             ║
║                                      ║
║ Difficulty (affects support scaling) ║
║ [Easy] [Normal] [Hard]               ║
║                                      ║
║ [Cancel] [Create Campaign]           ║
╚══════════════════════════════════════╝
```

---

## Page: Campaign War Room (Dashboard)

**Route:** `/campaign/:campaignId` or `/my-campaign`

This is the primary manager interface. Used ~15–30 min per week.

### Header

```
┌───────────────────────────────────────────────┐
│ Bernie Sanders Campaign                       │
│ Week 24 of 12 | June 23–29                  │
├───────────────────────────────────────────────┤
│ Support: 35.9% ↑1.7% | Discipline: 91/100  │
│ Budget: 100 pts | ROI: 1.94x                │
│ [View Full Analytics] [Campaign Settings]   │
└───────────────────────────────────────────────┘
```

### Main Content Grid (4-column layout on desktop, responsive)

#### Section 1: Weekly Brief (Top, spans 2 cols on desktop)

**Card: "This Week's Agenda"**

```
┌────────────────────────────────────┐
│ This Week's Agenda                 │
├────────────────────────────────────┤
│ 📅 Calendar                        │
│                                    │
│ Tue, Jun 25 @ 8pm ET               │
│ Real CNN Democratic Debate         │
│ Expected: Economy, Healthcare       │
│ [Respond: Participate / Skip]       │
│                                    │
│ Fri, Jun 28                        │
│ New Polling Release                │
│ [Auto-track]                       │
│                                    │
│ 🔔 Real News Alerts                │
│                                    │
│ ⚡ Fed Rate Hike Announcement     │
│    Markets down 2%. Economy        │
│    dominating headlines.           │
│    [Rapid Response Prompt]         │
└────────────────────────────────────┘
```

#### Section 2: Issue Salience (Top right, spans 1 col)

**Card: "Electorate Priorities This Week"**

```
┌──────────────────────────┐
│ Issue Salience           │
├──────────────────────────┤
│ Economy         71% ↑+8  │ ████
│ Healthcare      58% ↓-2  │ ███
│ Climate         43% ↓-3  │ ██
│ Immigration     52% →    │ ██
│                          │
│ Your strength:           │
│ ✓ Economy (strong)       │
│ ✓ Healthcare (strong)    │
│ • Climate (medium)       │
│ ✗ Immigration (weak)     │
└──────────────────────────┘
```

#### Section 3: Message & Budget (Left, spans 2 cols)

**Card: "Set This Week's Strategy"**

Tabs: [Messaging] [Budget]

**Messaging tab:**
```
┌──────────────────────────────────┐
│ Set Messaging Priorities         │
│                                  │
│ Pick up to 2 priorities:         │
│                                  │
│ ☑ Economy      (Trending ↑)     │
│ ☑ Healthcare   (Strong for you) │
│ ☐ Climate      (Trending ↓)     │
│ ☐ Immigration  (Neutral)        │
│                                  │
│ Rationale (optional):            │
│ [____________________________]    │
│                                  │
│ [Save Strategy]                  │
└──────────────────────────────────┘
```

**Budget tab:**
```
┌──────────────────────────────────┐
│ Allocate 100 Points              │
│                                  │
│ Channel        Budget   Last ROI │
│ Ads            [40 ]    2.1x ✓  │
│ Ground Game    [25 ]    1.3x    │
│ Digital        [10 ]    0.8x    │
│ Debate Prep    [20 ]    1.6x    │
│ Opposition     [ 5 ]    0.9x    │
│                                  │
│ Total: 100/100 ✓                │
│ No category > 60% ✓              │
│                                  │
│ [Visualize Budget] [Save]       │
└──────────────────────────────────┘
```

#### Section 4: Pending Approvals (Right, spans 2 cols)

**Card: "Agent Output — Pending Your Review"**

```
┌──────────────────────────────────────┐
│ Pending Approvals (3)               │
├──────────────────────────────────────┤
│                                      │
│ 📌 Rapid Response                   │
│    "Fed Rate Hike"                  │
│    Just now                         │
│                                      │
│    The Fed's hike is crushing...    │
│                                      │
│    Consistency: 92/100 ✓            │
│    Resonance: 87/100 ✓             │
│    Predicted: 3.2k reactions        │
│                                      │
│ [✓ Approve] [✏️ Redirect] [🔄 Regen] │
│                                      │
├──────────────────────────────────────┤
│ 📌 Statement                        │
│    "Healthcare Victory"             │
│    2 hours ago                      │
│                                      │
│ [✓ Approve] [✏️ Redirect] [🔄 Regen] │
└──────────────────────────────────────┘
```

#### Section 5: Recent Performance (Bottom left)

**Card: "Support Trend (Last 6 Weeks)"**

A simple line chart:
```
┌──────────────────────────────┐
│ Support Trend (Last 6 Wks)   │
│                              │
│ 40% ┤          ╱╲         ◄─ 35.9% (now)
│     │       ╱╲╱  ╲        │
│ 30% ├──╱╲╱       ╲       │
│     │ ╱           ╲      │
│ 20% ├──────────────╲─────│
│     │               ╲    │
│     └─────────────────╲──┴
│     W19   W20 ... W24
│
│ Peak: 35.9% (W24) ↑
│ Low: 29.2% (W19) ↓
└──────────────────────────────┘
```

#### Section 6: This Week's ROI (Bottom right)

**Card: "Channel Performance (Last 3 Weeks Avg)"**

```
┌─────────────────────────────┐
│ Channel ROI (3-week avg)    │
├─────────────────────────────┤
│ Ads          2.1x ████      │
│ Ground Game  1.3x ███       │
│ Digital      0.8x ██        │
│ Prep         1.6x ████      │
│ Opposition   0.9x ██        │
│                             │
│ Your Avg: 1.94x             │
│ (Good! Above 1.8x target)  │
└─────────────────────────────┘
```

---

## Page: Agent Output Coaching

**Route:** `/campaign/:campaignId/outputs/:outputId` (modal or full page)

When a user clicks into a pending approval to coach it.

### Full Output Coaching View

```
┌────────────────────────────────────────────┐
│ Output Coaching: Rapid Response            │ [X]
├────────────────────────────────────────────┤
│                                            │
│ Topic: Fed Rate Hike Announcement         │
│ Generated: 27 min ago                     │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ DRAFT CONTENT                        │  │
│ ├──────────────────────────────────────┤  │
│ │                                      │  │
│ │ The Fed's interest rate hikes are   │  │
│ │ crushing working families. We need  │  │
│ │ to rebuild an economy that works    │  │
│ │ for everyone, not just the          │  │
│ │ billionaire class...                │  │
│ │                                      │  │
│ │ [Read full draft →]                 │  │
│ │                                      │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ SCORES                                    │
│ ┌──────────────────────────────────────┐  │
│ │ Consistency with Sources: 92/100 ✓  │  │
│ │   Referenced: Sanders' economic     │  │
│ │   writings on wealth inequality     │  │
│ │                                      │  │
│ │ Electorate Resonance: 87/100 ✓     │  │
│ │   Aligns with economy salience      │  │
│ │   polling (+8 pts this week)        │  │
│ │                                      │  │
│ │ Predicted Engagement: 3.2k reactions │  │
│ │   (vs. avg 2.1k for this type)     │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ YOUR DECISION                              │
│ ┌──────────────────────────────────────┐  │
│ │ [✓ APPROVE]                         │  │
│ │  Publish as-is                      │  │
│ │                                      │  │
│ │ [✏️ REDIRECT]                         │  │
│ │  Feedback: ______________________ │  │
│ │  E.g., "Focus more on housing     │  │
│ │  costs, less on billionaires"     │  │
│ │                                      │  │
│ │ [🔄 REGENERATE]                     │  │
│ │  New angle: __________________ │  │
│ │  E.g., "Lead with Fed impact on   │  │
│ │  jobs, not inequality rhetoric"   │  │
│ │                                      │  │
│ │ [❌ KILL]                            │  │
│ │  Don't publish; try later          │  │
│ └──────────────────────────────────────┘  │
│                                            │
└────────────────────────────────────────────┘
```

**Feedback flow:**

If user selects "REDIRECT" or "REGENERATE", they type feedback, hit "Send", and see:

```
┌────────────────────────────────────────────┐
│ Agent is processing your feedback...       │
│                                            │
│ ⏳ New draft incoming in ~30 seconds       │
│                                            │
└────────────────────────────────────────────┘
```

After ~30s, a new draft appears with a "Revision 2" badge.

---

## Page: Event Participation Decision

**Route:** `/campaign/:campaignId/events/:eventId` (modal or full page)

When a user needs to decide whether to participate in a real event (debate, primary, etc.).

### Event Briefing

```
┌─────────────────────────────────────────────┐
│ Upcoming Event: Real CNN Debate             │ [X]
├─────────────────────────────────────────────┤
│                                             │
│ 📺 Event Details                           │
│                                             │
│ Date & Time: Tue, Jun 25 @ 8pm ET          │
│ Network: CNN                                │
│ Format: Democratic Primary Debate          │
│                                             │
│ Expected Topics:                            │
│ ✓ Economy (your priority)                  │
│ ✓ Healthcare (your priority)               │
│ • Climate (neutral)                        │
│ ✗ Immigration (weak for you)               │
│                                             │
│ 🎯 Your Alignment: 2 of 4 topics strong   │
│                                             │
│ ─────────────────────────────────────────  │
│                                             │
│ 💰 Budget Recommendation                   │
│                                             │
│ Debate Prep Budget Suggested: 20 points    │
│ (Your week's Prep allocation: 20 pts)      │
│                                             │
│ Expected ROI: 1.6x (based on history)     │
│                                             │
│ ─────────────────────────────────────────  │
│                                             │
│ 📋 Your Options                            │
│                                             │
│ [PARTICIPATE]                               │
│  Your agent enters the debate.              │
│  Use debate prep budget.                   │
│  Happens: Tue 8pm ET (3 days from now)    │
│                                             │
│ [RAPID RESPONSE]                            │
│  Your agent issues a statement.             │
│  Skip the live debate.                     │
│  Lower budget (5 pts), lower stakes        │
│                                             │
│ [SKIP]                                      │
│  Conserve budget; focus on digital.        │
│  May lose ground on debate topics.         │
│                                             │
│ [Cancel] [Make Decision]                   │
└─────────────────────────────────────────────┘
```

---

## Page: Campaign Analytics & Log

**Route:** `/campaign/:campaignId/analytics`

A deeper dive into performance, decisions, and outcomes. Useful for post-mortems or mid-campaign analysis.

### Tabs: [Performance] [Decisions] [Debates] [Outputs]

#### Performance Tab

```
┌───────────────────────────────────────────┐
│ Campaign Performance                      │
├───────────────────────────────────────────┤
│                                           │
│ OVERALL SCORE: 87.3 / 100                │
│ ████████████████░                        │
│                                           │
│ Final Support: 35.9%                     │
│ Starting Support: 25.0%                  │
│ Total Gain: +10.9 pts (+43.6%)           │
│                                           │
│ Message Discipline: 89 / 100              │
│ Total Approvals: 156                     │
│ Total Redirects: 28                      │
│ Approval Rate: 85%                       │
│                                           │
│ Budget ROI: 1.94x                        │
│ Budget Spent: 2,300 points (23 weeks)   │
│ Average per week: 100 points             │
│                                           │
│ Total Engagement: 187,400 reactions      │
│ Avg per week: 8,190                     │
│ Peak week: Week 18 (14,200)              │
│                                           │
│ Debates Participated: 8                  │
│  Won: 3 | Compromised: 3 | Lost: 2      │
│ Win Rate: 37.5%                          │
│                                           │
│ [Export Summary] [Share with Team]       │
└───────────────────────────────────────────┘
```

#### Decisions Tab

```
┌───────────────────────────────────────────┐
│ Decision Log (24 weeks)                   │
├───────────────────────────────────────────┤
│                                           │
│ Week 24 (Jun 23–29)                     │
│ ─────────────────────────────────────── │
│ ✓ Messaging: Economy + Healthcare       │
│   Rationale: Economy trending hot       │
│ ✓ Budget: Ads 40 | Ground 25 | ...     │
│ ✓ Event: Participate in debate         │
│ ✓ Approval: Fed Rate response approved │
│                                           │
│ Week 23 (Jun 16–22)                     │
│ ─────────────────────────────────────── │
│ ✓ Messaging: Climate + Immigration     │
│ ✓ Budget: Digital 35 | ...             │
│ ✓ Event: Skip primary debate           │
│ ✓ Redirect: Healthcare statement       │
│                                           │
│ [Load more weeks ↓]                    │
│ [Filter by decision type]                │
│ [Export CSV]                             │
└───────────────────────────────────────────┘
```

#### Debates Tab

```
┌───────────────────────────────────────────┐
│ Debate Performance                        │
├───────────────────────────────────────────┤
│ Win Rate: 37.5% (3 wins / 8 debates)    │
│                                           │
│ Debate 1 (Week 5, vs. Elizabeth Warren) │
│ Result: Won                              │
│ Topic: Healthcare                       │
│ Engagement: 12.3k reactions             │
│ Support Impact: +2.1%                   │
│ [View Debate]                            │
│                                           │
│ Debate 2 (Week 8, vs. Joe Biden)        │
│ Result: Compromised                     │
│ Topic: Economy                          │
│ Engagement: 9.8k reactions              │
│ Support Impact: +0.4%                   │
│ [View Debate]                            │
│                                           │
│ [Load more debates ↓]                   │
│ [Filter by outcome]                      │
└───────────────────────────────────────────┘
```

---

## Component Library (New)

### CampaignDashboard

Parent component. Manages layout, data fetching, real-time updates.

```tsx
<CampaignDashboard campaignId={campaignId} />
```

Children:
- `CampaignHeader`
- `WeeklyBrief`
- `MessagingBudgetPanel`
- `PendingApprovalsPanel`
- `PerformanceChart`
- `ChannelROI`

### PendingApproval

Reusable card for a single AgentOutput pending approval.

```tsx
<PendingApproval
  output={agentOutput}
  onApprove={(id) => handleApprove(id)}
  onRedirect={(id, feedback) => handleRedirect(id, feedback)}
  onRegenerate={(id, angle) => handleRegen(id, angle)}
/>
```

### BudgetAllocator

Budget slider component. Shows constraints, ROI history, allocation chart.

```tsx
<BudgetAllocator
  allocation={current}
  history={lastThreeWeeks}
  onSave={(newAllocation) => saveStrategy(newAllocation)}
/>
```

### IssueSalienceBar

Shows current issue polling and agent strength.

```tsx
<IssueSalienceBar
  issueSalience={polling}
  agentStrengths={agent.strengths}
/>
```

### EventDecisionModal

Modal for deciding on event participation.

```tsx
<EventDecisionModal
  event={realEvent}
  campaign={campaign}
  onDecide={(choice) => handleEventDecision(choice)}
/>
```

### SupportTrendChart

Line chart showing support over time.

```tsx
<SupportTrendChart
  data={supportHistory}
  annotations={eventImpacts}
/>
```

---

## Mobile Responsiveness

Campaign Manager is primarily a **desktop experience** (planning, budget allocation, detailed coaching). However, mobile users should be able to:

- View the weekly brief
- Approve/reject pending outputs (swipe to approve/reject)
- Make simple event decisions (Participate / Skip)
- Check support trend
- View alerts

**Responsive breakpoints:**

| Breakpoint | Behavior |
|------------|----------|
| < 640px | Single-column layout; tabs for budget/messaging; modal panels |
| 640–1024px | 2-column grid; BudgetAllocator simplified |
| > 1024px | Full 4-column dashboard grid |

---

## Accessibility

- All form inputs labeled with `<label>` or `aria-label`
- Color-coded charts (support trend, salience) include tooltips with numeric values
- Buttons have descriptive text (not just icons)
- Focus states on all interactive elements
- Keyboard navigation for budget allocation (arrow keys to adjust sliders)
- Alt text on all charts and images
- Reduced motion preferences respected (disable animations if `prefers-reduced-motion`)

---

## Real-Time Updates

Use WebSocket (e.g., Socket.io or SignalR) for live updates:

- **New AgentOutput** → Notification toast + badge on PendingApprovalsPanel
- **Real event update** → Updated alert in WeeklyBrief
- **Week resolved** → Refresh PerformanceChart, show new metrics
- **Support moved** → Animate SupportTrendChart

Example toast notification:
```
┌────────────────────────────────┐
│ ✨ New rapid response ready   │
│ "Fed Rate Hike Response"       │
│ [Review] [Dismiss]             │
└────────────────────────────────┘
```

---

## Success Metrics (UX)

- **Time to decision:** Manager can set weekly strategy (messaging + budget) in < 5 min
- **Coaching throughput:** Manager can approve/redirect 3+ outputs per session
- **Clarity:** Manager correctly predicts support movement direction on 60%+ of weeks
- **Engagement:** 40%+ of registered users start a campaign; 60%+ complete one cycle

