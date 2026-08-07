# Product Requirements Document: Theme Rooms

**Product:** Civersify  
**Feature:** Theme Rooms  
**Status:** Draft  
**Owner:** Product  
**Related documents:** Story Rooms, Conversation Map, Knowledge Graph, Money Trail, Interactive News Engagement, Editorial Trust & Safety

---

## 1. Summary

A Theme Room is a long-lived, evolving destination for a major public issue. It contains multiple Story Rooms plus persistent knowledge, actors, institutions, bills, budget items, claims, reactions, sources, and predictions.

Examples include:

- U.S.–Iran conflict and regional escalation
- Immigration policy and border enforcement
- Artificial intelligence regulation
- Federal budget negotiations
- Housing affordability
- A Supreme Court term
- A presidential election cycle
- Climate and energy policy

A Theme Room is not a long article. It is an interactive, progressively disclosed knowledge environment whose front door always answers:

1. What changed?
2. Why does it matter?
3. What should I watch next?

---

## 2. Problem

News coverage is event-oriented and fragmented. A user encountering the tenth article in a developing story often lacks:

- The history required to understand it
- Definitions of unfamiliar concepts
- Knowledge of the institutions involved
- Connection to related bills and funding
- Distinction between established facts and unresolved claims
- A coherent view of disagreements
- A way to return when the story changes

A standard topic page typically solves this by creating an endless reverse-chronological feed. That still requires users to synthesize the story themselves.

---

## 3. Product Goal

Enable a user to enter an unfamiliar major issue and, within five minutes, understand its current state, essential history, principal actors, government actions, financial stakes, unresolved claims, major arguments, and likely next developments.

---

## 4. Non-Goals

The first version will not:

- Replace comprehensive reporting from news organizations
- Provide a raw social-media feed
- Claim to measure representative public opinion from social data
- Produce real-time military, emergency, or investment advice
- Host unrestricted user comments
- Cover every active news topic
- Become a general-purpose encyclopedia
- Generate a single definitive ideological judgment

---

## 5. Target Users and Jobs

### Curious learner

**Job:** “Help me catch up on this issue without making me read fifteen articles.”

### Returning follower

**Job:** “Show me only what changed since my last visit.”

### Young adult or student

**Job:** “Explain the terms, history, and government mechanics I am expected to already know.”

### Engaged citizen

**Job:** “Connect the news to bills, official actions, budgets, and original sources.”

### Teacher or parent

**Job:** “Give me a credible, bounded package I can use to start a discussion.”

---

## 6. Information Architecture

Every Theme Room should use a consistent structure, though sections may be hidden when no useful content exists.

### 6.1 Overview

The default view contains:

- Theme title and neutral subtitle
- Current status sentence
- Last meaningful update timestamp
- “New since your last visit” indicator
- Three essential facts
- Top unresolved question
- One “watch next” item
- A short progress path: **Catch Up → Understand → Explore → Predict**

### 6.2 Latest

A bounded list of meaningful developments, not every article.

Each development includes:

- Timestamp or date
- One-sentence summary
- Why it matters
- Confidence or evidence status
- Link to a Story Room
- “New” marker when applicable

### 6.3 Story Rooms

The Theme Room contains related Story Rooms grouped by type:

- Military or security development
- Legislative development
- Court ruling
- Executive action
- Diplomatic development
- Economic data or market effect
- Election or polling development
- Investigation or accountability event
- Humanitarian development

### 6.4 Understand

Durable context, including:

- “How we got here” narrative
- Timeline
- Map or geographic explainer
- Key concepts and vocabulary
- Frequently confused terms
- Historical precedents
- Institutional mechanics

### 6.5 People and Power

Interactive actor map containing:

- Countries and governments
- Elected officials
- Agencies
- Courts
- Committees
- Military organizations
- International organizations
- Companies
- Advocacy organizations
- Affected communities

Each actor card answers:

1. What role do they play?
2. What power do they have?
3. What do they publicly say they want?
4. What constrains them?
5. Which Story Rooms involve them?

### 6.6 Government and Law

This section reuses items from the Civersify bills experience and adds:

- Bills and resolutions
- Committee activity
- Recorded votes
- Executive orders or agency actions
- Court cases and rulings
- Treaties or international agreements
- Relevant constitutional or statutory powers
- State-level actions when relevant

### 6.7 Money

The Theme Room embeds the Money Trail module:

- Major funding requests
- Authorizations
- Appropriations
- Obligations and reported spending
- Aid packages
- Estimated future costs
- Economic effects
- Uncertainty and source notes

### 6.8 Claims and Evidence

A claim ledger containing:

- Claim text
- Claim source
- Evidence status
- Supporting evidence
- Contradicting evidence
- Last reviewed timestamp
- Related Story Rooms

Allowed statuses:

- Confirmed
- Strongly supported
- Plausible but unresolved
- Disputed
- Unsupported
- False
- Outdated
- Prediction

### 6.9 Conversation

A Conversation Map presenting:

- Major argument clusters
- Frequently asked questions
- Repeated claims
- Values and concerns underlying disagreement
- Community differences
- Changes over time
- Carefully selected source-linked examples

### 6.10 Predictions

A small number of measurable questions:

- Will a bill receive a vote by a defined date?
- Will an agency issue a rule?
- Will negotiations resume?
- Will a court accept a case?
- Will an official policy change?

The Theme Room shows aggregate probability distributions and later resolves questions with evidence.

### 6.11 Sources and Methodology

Includes:

- Primary documents
- Government data
- Reporting
- Analysis
- Source-type labels
- Update log
- Correction log
- Social sampling methodology where used

---

## 7. Key User Journeys

### 7.1 First visit: Catch me up

1. User lands from search or social sharing.
2. User sees current status, three essential facts, and latest change.
3. User answers a pre-exposure question or confidence prompt.
4. User opens one Story Room or knowledge item.
5. User completes one interaction.
6. User chooses to follow the Theme Room or make a prediction.

### 7.2 Returning visit: What changed?

1. User returns through a follow notification or bookmark.
2. Theme Room defaults to “Since your last visit.”
3. User sees changed facts, new Story Rooms, resolved claims, and updated predictions.
4. Previously viewed sections retain completion state.

### 7.3 Deep research

1. User opens People and Power, Government, Money, or Claims.
2. User filters objects by actor, date, source type, or evidence status.
3. User opens original sources and related Story Rooms.
4. User exports or shares a source-grounded summary card.

### 7.4 Classroom mode

1. Teacher opens a simplified classroom link.
2. Students complete a bounded sequence of readings and interactions.
3. The final screen provides discussion prompts rather than ideological scores.

---

## 8. Functional Requirements

### TR-1: Theme creation

Authorized editors can create a Theme Room with:

- Canonical title
- Alternate names
- Neutral subtitle
- Scope statement
- Inclusion and exclusion rules
- Editorial sensitivity classification
- Geography
- Active date range
- Primary editor

### TR-2: Theme lifecycle

Supported states:

- Candidate
- Draft
- Active
- Monitoring
- Dormant
- Archived

An active Theme Room must have a defined monitoring cadence and freshness owner.

### TR-3: Content composition

Editors can attach existing objects or create new ones:

- Story Rooms
- Knowledge items
- Actors
- Policy items
- Budget items
- Claims
- Sources
- Reactions
- Predictions
- Interactions

### TR-4: Meaningful-update detection

The platform should distinguish meaningful changes from repetitive coverage. A new article should not automatically create a Theme Room update.

Potential meaningful changes include:

- New official action
- Change in verified facts
- New vote or ruling
- Material casualty or economic update
- Changed negotiation status
- Major correction
- Prediction resolution

### TR-5: “Since your last visit”

For authenticated users, store the last meaningful revision seen and display a delta view.

For anonymous users, use local storage when permitted.

### TR-6: Progressive disclosure

The default view must remain scannable. Detailed timelines, source records, methodology, and complex data should be available without overwhelming first-time users.

### TR-7: Cross-linking

Every attached object should surface its other relevant contexts. For example, a bill can link to:

- `/bills`
- Its Story Room
- The Theme Room
- Relevant representatives
- Related budget items
- Related predictions

### TR-8: Following

Users can follow a Theme Room. Notifications should be triggered only by meaningful changes, not every content edit.

### TR-9: Sharing

Users can share:

- Theme overview
- “What changed” card
- Timeline card
- Claim card
- Budget card
- Prediction card
- “Three things I learned” card

### TR-10: Accessibility

- Keyboard navigable
- Screen-reader labels for all interactive diagrams
- Text alternatives for maps and visual timelines
- Reading-level controls where feasible
- Captions and transcripts for audio or video

---

## 9. Example Theme: U.S.–Iran Conflict and Regional Escalation

The final title must be editorially reviewed because naming an evolving conflict can imply a disputed classification. A placeholder Theme Room could include:

### Latest

- New military, diplomatic, legislative, or economic developments
- Current escalation level
- Immediate humanitarian or security consequences

### Understand

- U.S.–Iran relations timeline
- Regional proxy relationships
- Strait of Hormuz geography
- Sanctions, embargoes, deterrence, and war-powers concepts

### People and Power

- U.S. executive branch
- Congress and relevant committees
- Iranian government and security institutions
- Regional governments
- International organizations

### Government and Law

- War-powers resolutions
- Sanctions legislation
- Funding and aid measures
- International-law questions

### Money

- Operational costs
- Foreign assistance
- Equipment replacement
- Humanitarian assistance
- Energy and shipping effects

### Conversation

- Humanitarian concern
- Deterrence arguments
- Legal authority
- Regional escalation risk
- Energy prices
- Reliability of official information

The room should avoid pretending that social reactions establish representative public opinion.

---

## 10. Editorial Workflow

1. Editor proposes or activates a Theme Room.
2. Editor defines scope and disputed terminology.
3. Ingestion services identify candidate Story Rooms and related objects.
4. AI creates structured drafts, never final publication authority.
5. Editor reviews essential facts, claims, source quality, and uncertainty.
6. Trust and safety review is triggered for sensitive content.
7. Theme Room publishes with an initial methodology and revision ID.
8. Meaningful updates create a changelog entry.
9. Corrections propagate to all reused objects.

---

## 11. Metrics

### Primary

- Percentage of visitors completing the Catch Up path
- Return rate after a meaningful update
- Average number of distinct sections explored
- Comprehension improvement
- Follow and prediction participation rate

### Guardrails

- Correction rate
- Time from source correction to Civersify correction
- Unresolved claims presented without labels
- Social-content report rate
- User perception of partisan framing

---

## 12. MVP Scope

One pilot Theme Room should include:

- Overview and Latest
- Five to ten Story Rooms
- Ten to twenty knowledge items
- A timeline
- An actor map
- Two to five relevant policy items
- A lightweight Money Trail
- Ten to twenty claims
- Three predictions
- Two reusable interactions
- A small, disclosed Conversation Map
- Source and correction pages

Not required for MVP:

- Fully automated real-time ingestion
- Open user comments
- Personalized ideological recommendations
- Comprehensive historical archives
- Every social platform

---

## 13. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Theme becomes an overwhelming encyclopedia | Progressive disclosure and bounded “Catch Up” path |
| Scope expands indefinitely | Explicit inclusion and exclusion rules |
| Naming implies an editorial conclusion | Alternate-name support and terminology notes |
| Content becomes stale | Lifecycle states, monitoring owner, meaningful-update rules |
| AI merges uncertain claims into facts | Claim ledger, provenance, editorial review |
| Social reactions appear representative | Methodology labels and argument-cluster framing |
| Users are drawn only to conflict content | Healthy engagement metrics and bounded sessions |

---

## 14. Acceptance Criteria

A Theme Room is ready for public launch when:

- The overview accurately summarizes the current state in plain language.
- Every factual statement has traceable provenance.
- All unresolved claims are visibly labeled.
- At least one durable knowledge path exists.
- Related Story Rooms, bills, and budget objects are cross-linked.
- A user can identify what changed since the previous revision.
- The room includes at least one meaningful interaction.
- The page remains useful when no new event has occurred for several days.
- Accessibility checks pass.
- Editorial, trust, and safety review is complete.
