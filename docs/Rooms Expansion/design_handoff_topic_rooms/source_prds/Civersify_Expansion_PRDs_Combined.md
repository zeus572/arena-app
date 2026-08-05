<!-- BEGIN 00_Civersify_Expansion_PRD_Index.md -->



---

# Civersify Expansion PRD Set

**Product:** Civersify  
**Document status:** Working product requirements  
**Primary audience:** Product, design, engineering, editorial, data, trust and safety  
**Core premise:** Turn current events into interactive, connected, source-grounded experiences that help users understand what happened, how institutions work, what tradeoffs exist, what people are debating, and what may happen next.

---

## 1. Product Vision

Civersify should not become another infinite news feed or a collection of disconnected civics games. Its differentiated experience is a structured path from **news awareness** to **systems understanding** to **active reasoning**.

A user should leave Civersify able to answer:

1. What happened?
2. Why does it matter?
3. How did we get here?
4. Who has power to change it?
5. Where is the money going?
6. Which claims are established, disputed, or unresolved?
7. What are thoughtful people disagreeing about?
8. What should I watch next?
9. What do I predict will happen?

The core product hierarchy is:

```text
Theme Room
  A long-lived, evolving subject or public issue

Story Room
  A specific event, development, decision, bill, ruling, speech, or data release

Knowledge Item
  A durable concept, actor, institution, law, budget item, place, claim, or source

Interaction
  A quiz, prediction, simulation, map, timeline, classification task, or tradeoff
```

---

## 2. Why a PRD Set Instead of One Document

The proposed expansion contains several products that share a platform but have different users, risks, and operational requirements. This set separates them while keeping their dependencies explicit.

| File | Scope | Primary outcome |
|---|---|---|
| `01_Theme_Rooms_PRD.md` | Long-lived topic hubs | Create a living destination for major issues |
| `02_Story_Rooms_PRD.md` | Atomic story experiences | Make each development understandable and interactive |
| `03_Conversation_Map_PRD.md` | Social and public reactions | Explain the shape of debate without presenting social media as public opinion |
| `04_Knowledge_Graph_Content_Platform_PRD.md` | Shared data and authoring platform | Reuse facts, actors, bills, budgets, claims, and sources across Civersify |
| `05_Money_Trail_Budget_Intelligence_PRD.md` | Government and policy spending | Clarify requested, authorized, appropriated, obligated, and spent amounts |
| `06_Interactive_News_Engagement_PRD.md` | Games, predictions, and learning mechanics | Reward curiosity, reasoning, evidence use, and opinion updating |
| `07_Editorial_Trust_Safety_PRD.md` | Editorial standards and safeguards | Build a credible, youth-safe, auditable product |
| `08_MVP_Rollout_and_Operating_Plan.md` | Sequencing and launch plan | Pilot the system without overbuilding |

---

## 3. Shared Product Principles

### 3.1 Inform before persuading

Civersify may present disagreements, values, and policy tradeoffs, but it should not optimize for moving users toward a preferred ideology.

### 3.2 Make uncertainty visible

Current events are often incomplete. The product must distinguish:

- Confirmed facts
- Strongly supported claims
- Disputed interpretations
- Unverified reports
- Predictions
- Unknowns

### 3.3 Reward intellectual behaviors

Reward users for:

- Opening original sources
- Distinguishing fact from opinion
- Recognizing uncertainty
- Considering multiple stakeholders
- Making calibrated predictions
- Asking useful questions
- Revising a view when evidence changes

Do not reward outrage, partisan victory, rapid clicking, or ideological conformity.

### 3.4 Show systems, not only events

A news event becomes more useful when connected to:

- Institutions and powers
- Historical precedent
- Relevant laws and bills
- Budget flows
- Stakeholders
- Geographic context
- Claims and evidence
- Public arguments

### 3.5 Preserve source provenance

Every synthesized statement must be traceable to one or more sources, with publication date, source type, retrieval date, and correction history.

### 3.6 Design for young adults without making the product childish

Use plain language, clear visual hierarchy, progressive disclosure, short interactions, and safety protections. Preserve intellectual depth for adults and educators.

### 3.7 Prefer finite experiences over infinite feeds

Users should be able to complete a meaningful session and know what they learned. Theme Rooms may update continuously, but each visit should have a bounded path.

---

## 4. Shared Audiences

### Curious learner

Wants a fast explanation without spending an hour reading multiple articles.

### Young adult or high-school student

Needs context, vocabulary, institutional explanations, and confidence that material is appropriate and credible.

### Engaged citizen

Already follows the news but wants source comparison, policy detail, bills, budgets, and consequences.

### Teacher or parent

Wants a structured experience that can be assigned, discussed, or shared.

### News-fatigued user

Wants to understand important events without an endless stream of outrage or repetition.

---

## 5. Shared Success Metrics

### Understanding

- Percentage of users who correctly answer a post-experience comprehension check
- Improvement between pre-exposure and post-exposure confidence calibration
- Ability to distinguish fact, interpretation, opinion, and prediction

### Depth

- Theme Room sections explored per session
- Story Rooms completed per Theme Room visit
- Original-source open rate
- Knowledge-item expansion rate

### Retention

- Return rate when a followed Theme Room changes
- Prediction resolution return rate
- Daily or weekly Civic Sprint completion

### Trust

- User-rated clarity and fairness
- Correction rate and correction time
- Percentage of claims with complete provenance
- User reports involving misleading framing or unsafe content

### Healthy engagement

- Meaningful completion rate rather than raw time-on-site
- Percentage of sessions ending with a saved question, prediction, or source
- Low incidence of rage-click loops or repeated exposure to harmful material

---

## 6. Shared Glossary

| Term | Definition |
|---|---|
| Theme Room | A long-lived hub for an evolving issue containing multiple Story Rooms and persistent knowledge |
| Story Room | A focused page about one development or event |
| Story Bundle | Structured data used to generate a Story Room and related interactions |
| Knowledge Item | A reusable explanation of a concept, person, institution, law, place, or policy mechanism |
| Claim | A specific assertion that can be sourced and assigned an evidence status |
| Conversation Map | A synthesized view of arguments, concerns, questions, and repeated claims in public discourse |
| Money Trail | A module that connects policy announcements to government funding stages and economic consequences |
| Prediction | A measurable question with a deadline or resolution condition |
| Civic Sprint | A short, bounded daily or weekly interactive news-learning experience |
| Provenance | Metadata showing where a statement came from and how it was transformed |

---

## 7. Dependency Overview

```text
Knowledge Graph and Content Platform
        ↓
Story Rooms ────────────────┐
        ↓                   │
Theme Rooms                 │
        ↓                   │
Conversation Map            │
Money Trail                 │
Interactive News Engagement│
        ↓                   │
Editorial Trust & Safety ───┘
```

Theme Rooms should not be implemented as manually curated static pages. They should be composed from structured objects created by the shared content platform.

---

## 8. Recommended Product Sequence

1. Define the shared object model and editorial workflow.
2. Upgrade one existing bill experience into a Story Room.
3. Build one pilot Theme Room around a major evolving issue.
4. Add durable knowledge items, actors, timeline, bills, and claims.
5. Add a lightweight Money Trail.
6. Add predictions and two reusable interaction types.
7. Add a tightly scoped Conversation Map using approved sources.
8. Add following, update notifications, and a daily Civic Sprint.
9. Scale ingestion and authoring only after quality and retention are demonstrated.

---

## 9. North-Star Experience

A user enters a Theme Room and sees:

> Here is what changed. Here is how the system works. Here is where the money goes. Here is what people disagree about. Here is what the evidence supports. Here is what remains uncertain. Now make your prediction.

---



<!-- END 00_Civersify_Expansion_PRD_Index.md -->


---

<!-- BEGIN 01_Theme_Rooms_PRD.md -->



---

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

---



<!-- END 01_Theme_Rooms_PRD.md -->


---

<!-- BEGIN 02_Story_Rooms_PRD.md -->



---

# Product Requirements Document: Story Rooms

**Product:** Civersify  
**Feature:** Story Rooms  
**Status:** Draft  
**Related documents:** Theme Rooms, Knowledge Graph, Interactive News Engagement, Editorial Trust & Safety

---

## 1. Summary

A Story Room is the atomic Civersify experience for one meaningful development. It combines a concise synthesis, source-grounded facts, context, institutions, claims, consequences, and one or more interactions.

A Story Room may cover:

- A congressional bill or vote
- A court ruling
- An executive action
- A military or diplomatic development
- An economic report
- An election result
- A regulatory proposal
- A major investigation
- A public-health or environmental development

A Story Room can stand alone, appear inside one or more Theme Rooms, and reuse objects from the Civersify knowledge graph.

---

## 2. Problem

Most news articles assume background knowledge and mix several layers:

- What happened
- What the reporter or source says it means
- Historical context
- Unresolved claims
- Predictions

Readers must separate these themselves. They also tend to consume stories passively, which reduces retention and makes uncertainty easy to miss.

---

## 3. Product Goal

Within two to four minutes, a user should understand the development, identify its most important established facts, recognize what remains uncertain, connect it to relevant institutions or policies, and complete one active reasoning task.

---

## 4. Story Room Design Pattern

Each Story Room follows a common sequence:

```text
1. Before You Know
2. What Happened
3. Why It Matters
4. How It Works
5. What Is Known and Unknown
6. Who Is Affected
7. Explore or Play
8. What Happens Next
9. Sources and Updates
```

Sections may be collapsed or omitted when irrelevant.

---

## 5. Core Page Requirements

### 5.1 Header

- Neutral headline
- One-sentence summary
- Event date and last-reviewed date
- Related Theme Room tags
- Evidence status
- Estimated completion time
- Sensitive-content note when appropriate

### 5.2 Before You Know

Present one pre-exposure prompt before revealing the full answer.

Examples:

- Which branch of government made this decision?
- Did this proposal receive bipartisan support?
- Which group is most directly affected?
- How much do you think the program costs?
- Which source would best verify this claim?

The prompt should create curiosity, not trick the user.

### 5.3 What Happened

- Three to five essential facts
- Chronological clarity
- Direct distinction between event and interpretation
- No unexplained jargon

### 5.4 Why It Matters

Explain consequences across several dimensions:

- Immediate effect
- Institutional effect
- Financial effect
- Legal effect
- Human effect
- Longer-term possibility

### 5.5 How It Works

Connect the event to reusable knowledge items:

- Government power
- Legislative process
- Court procedure
- Economic mechanism
- Geographic context
- Historical precedent

### 5.6 Known, Disputed, and Unknown

Use an explicit claim ledger:

| Claim | Status | Evidence | Last reviewed |
|---|---|---|---|
| Example assertion | Confirmed | Primary source and corroborating report | Date |

Users should be able to open a claim and view the evidence trail.

### 5.7 Stakeholder View

Show how the story may affect different groups. Avoid implying all members of a group share one view.

### 5.8 Interactive Module

At least one interaction is required for flagship Story Rooms. The interaction should match the story rather than being attached arbitrarily.

Possible types:

- Fact, opinion, or prediction
- Arrange the timeline
- Match actor to power
- Map challenge
- Identify missing evidence
- Budget allocation
- Coalition builder
- Consequence tree
- Calibrated prediction
- Vote before reading

### 5.9 What Happens Next

List two to five observable next steps, each with:

- Responsible actor
- Expected or possible timing
- What evidence would confirm it
- Related prediction when available

### 5.10 Sources

Display source groups:

- Primary documents
- Government data
- Direct statements
- Reporting
- Analysis
- Social or public reaction sources

Each source includes date, author or organization, source type, and use within the synthesis.

---

## 6. Story Types and Specialized Modules

### 6.1 Bill Story Room

Builds on the current Civersify bills page.

Required fields:

- Bill title and identifier
- Sponsor and co-sponsors
- Current status
- Committee
- Major provisions
- Estimated cost when available
- Supporters’ strongest argument
- Opponents’ strongest argument
- Recorded votes
- Related Theme Rooms

Recommended interactions:

- Vote before reading
- Build an amendment
- Coalition builder
- Who wins, who pays?

### 6.2 Court Story Room

Required fields:

- Court and case
- Question presented
- Holding
- Majority rationale
- Dissent or competing rationale
- Immediate legal effect
- What remains unresolved

Recommended interactions:

- Match precedent to claim
- Identify which level of court acts next
- Separate holding from commentary

### 6.3 Executive or Agency Action

Required fields:

- Issuing authority
- Legal basis asserted
- Implementation mechanism
- Effective date
- Affected groups
- Legal or legislative challenges

### 6.4 Economic Data Story

Required fields:

- Metric definition
- Current value
- Prior value
- Expected value when relevant
- Revision history
- What the metric does and does not show

Recommended interactions:

- Interpret the chart
- Percentage versus absolute amount
- Correlation versus causation

### 6.5 Conflict or Crisis Story

Required fields:

- Confirmed sequence of events
- Geography
- Parties involved
- Civilian and humanitarian effects
- Source reliability notes
- Unverified or disputed claims
- Content warnings

Do not turn casualty counts or graphic details into game mechanics.

---

## 7. Story Bundle Data Model

Each Story Room should be generated from a structured Story Bundle rather than free-form article text.

```yaml
story:
  id:
  canonical_title:
  dek:
  event_time:
  published_time:
  last_reviewed_time:
  story_type:
  theme_ids: []
  geography_ids: []
  sensitivity:

essential_facts:
  - statement:
    claim_id:
    importance:

context:
  knowledge_item_ids: []
  timeline_event_ids: []
  precedent_ids: []

actors:
  actor_ids: []

policy:
  bill_ids: []
  executive_action_ids: []
  court_case_ids: []

money:
  budget_item_ids: []

claims:
  claim_ids: []

stakeholders:
  - group:
    impact_summary:
    confidence:

interactions:
  interaction_ids: []

next_steps:
  - description:
    actor_id:
    verification_condition:
    prediction_id:

sources:
  source_ids: []

editorial:
  author:
  reviewer:
  revision_id:
  correction_ids: []
```

---

## 8. AI-Assisted Authoring Workflow

### 8.1 Candidate detection

The system identifies a potential meaningful development based on official actions, source diversity, relevance to active Theme Rooms, and novelty.

### 8.2 Source collection

The pipeline retrieves approved source types and records metadata before generation.

### 8.3 Structured extraction

AI proposes:

- Events
- Actors
- Claims
- Evidence links
- Concepts
- Bills and budget links
- Potential interactions

### 8.4 Draft synthesis

AI generates a draft using the Story Bundle structure.

### 8.5 Editorial review

An editor verifies:

- Headline neutrality
- Essential facts
- Claim statuses
- Source quality
- Missing perspectives
- Uncertainty language
- Age appropriateness

### 8.6 Publication and propagation

Approved objects update related Theme Rooms, actor pages, bill pages, predictions, and timelines.

AI must never silently convert an unresolved claim into a fact.

---

## 9. Updating and Corrections

### 9.1 Update types

- New evidence
- Clarification
- Correction
- Retraction
- Prediction resolution
- Terminology change
- Source removal

### 9.2 User-facing behavior

Material changes should be visible through:

- “Updated” marker
- Changelog entry
- Highlight of changed sentences where practical
- Correction note when prior content was wrong

### 9.3 Object propagation

When a reused claim or fact changes, all dependent Story Rooms and Theme Rooms should be flagged for review.

---

## 10. Personalization

Permitted personalization:

- Reading depth
- Preferred format
- Followed themes
- Previously viewed content
- Knowledge-level adaptation
- Relevant geography

Avoid:

- Ideological echo-chamber ranking
- Hiding good-faith counterarguments
- Behavioral advertising based on political views
- Presenting inferred political identity as fact

---

## 11. Metrics

### Primary

- Story completion rate
- Pre-question to post-question learning improvement
- Interaction completion rate
- Source-open rate
- Related-knowledge-item open rate
- Return rate after an update

### Quality

- Claim provenance completeness
- Editorial correction rate
- Time to correct propagated errors
- User-rated clarity
- User-rated fairness

---

## 12. MVP Scope

The MVP Story Room template should support:

- Bill story
- General event story
- Court or agency story
- Essential facts
- Why it matters
- Knowledge items
- Claim ledger
- One interaction
- Next steps
- Sources
- Changelog

The MVP should initially require editorial approval for every published Story Room.

---

## 13. Acceptance Criteria

A Story Room is publishable when:

- It covers one clearly defined development.
- The headline does not overstate disputed facts.
- Essential facts are independently traceable.
- Facts, opinions, interpretations, and predictions are distinguishable.
- Jargon is defined or linked.
- At least one meaningful consequence is explained.
- Unresolved claims are labeled.
- Sources are grouped by type.
- The user can identify what may happen next.
- Any interaction reinforces the content rather than trivializing it.

---



<!-- END 02_Story_Rooms_PRD.md -->


---

<!-- BEGIN 03_Conversation_Map_PRD.md -->



---

# Product Requirements Document: Conversation Map

**Product:** Civersify  
**Feature:** Conversation Map and Social Reaction Corpus  
**Status:** Draft  
**Related documents:** Theme Rooms, Knowledge Graph, Editorial Trust & Safety

---

## 1. Summary

The Conversation Map explains how a public issue is being discussed across official statements, informed interpretation, and selected public communities. It clusters arguments, concerns, questions, values, and repeated claims instead of presenting a raw social feed or simplistic sentiment score.

The core framing is:

> This is evidence about the conversation, not proof of reality and not a representative measure of public opinion.

---

## 2. Problem

Public reaction is important because it reveals:

- Which concerns are salient
- Which claims are spreading
- How different communities frame the same event
- What people misunderstand
- What questions remain unanswered
- Which values underlie disagreement

However, simply crawling social media creates serious problems:

- Platform populations are not representative
- Algorithms amplify unusual or emotional content
- Engagement can be manipulated
- Bots and coordinated campaigns distort prevalence
- Context is lost when posts are extracted
- Toxic or graphic material may reach young users
- Platform terms may limit storage, transformation, or commercial use

---

## 3. Product Goal

Help users understand the structure of a public debate while preserving methodological honesty, source context, privacy, platform compliance, and youth safety.

---

## 4. Non-Goals

The Conversation Map will not:

- Claim to represent national public opinion
- Publish a raw, infinite social feed
- Rank comments solely by likes, reposts, or upvotes
- Compute a single “positive versus negative” sentiment score as the main insight
- Infer private political identity
- Dox or unnecessarily identify ordinary users
- Reproduce large amounts of platform content
- Treat expert opinion as verified fact
- Automatically publish unreviewed high-risk content

---

## 5. Reaction Layers

The product must visibly separate three layers.

### 5.1 Official response

Examples:

- Governments
- Members of Congress
- Agencies
- Courts
- International organizations
- Political parties
- Military organizations

### 5.2 Informed interpretation

Examples:

- Subject-matter experts
- Legal scholars
- Economists
- Regional specialists
- Journalists
- Former officials
- Humanitarian organizations

Credentials and potential conflicts should be shown where known. Expertise does not make a claim automatically correct.

### 5.3 Public conversation

Examples:

- Reddit posts and comments
- Bluesky or similar public posts
- YouTube comments
- Public forum discussions
- Firsthand accounts

This layer must include sampling and representativeness warnings.

---

## 6. User Experience

### 6.1 Conversation summary

The top of the module includes:

- A neutral summary of dominant argument clusters
- A clear sample description
- Date range
- Platforms or communities included
- Major exclusions
- A note that prevalence refers only to the collected sample

### 6.2 Argument clusters

A cluster represents a coherent concern or argument, such as:

- Humanitarian consequences
- Risk of escalation
- Legal authority
- Economic costs
- Deterrence and security
- Distrust of official information
- Effects on allies
- Domestic political incentives

Each cluster contains:

- Neutral title
- One-sentence explanation
- Underlying value or concern
- Approximate prevalence in the collected sample
- Representative examples
- Strong counterargument or competing concern
- Related verified facts
- Claims needing verification
- Communities in which it appears
- Change over time

### 6.3 Questions people are asking

Extract and group authentic questions:

- What is unclear?
- Which mechanisms are misunderstood?
- Which questions lack reliable public answers?
- Which questions can be answered by existing Civersify knowledge items?

### 6.4 Repeated claims

The system identifies claims that are repeated across sources and attaches a claim status:

- Confirmed
- Supported
- Unresolved
- Disputed
- Unsupported
- False
- Outdated

Virality must not increase evidence status.

### 6.5 Community comparison

Where methodologically defensible, show differences between communities:

- Which concerns dominate
- Which sources are cited
- Which actors are trusted or distrusted
- Which values are emphasized
- Which claims are repeated

Avoid ranking communities as smarter, more moral, or more truthful.

### 6.6 Conversation-over-time

A timeline can show how discussion changed before and after:

- An official announcement
- A vote
- A military or diplomatic event
- A fact-check
- A correction
- A new economic release

---

## 7. Social Content Ingestion

### 7.1 Connector architecture

Each platform should be isolated behind an adapter.

```text
Connector
├── Authentication and quota handling
├── Search or stream configuration
├── Retrieval
├── Normalization
├── Deletion and edit reconciliation
├── Display-permission enforcement
└── Audit logging
```

Initial platform candidates should be selected based on:

- Approved API access
- Commercial-use compatibility
- Reliable deletion handling
- Stable identifiers
- Sufficient conversation context
- Moderation capabilities
- Value of the content for civic understanding

Any Reddit, social-media, or forum ingestion must undergo current legal and platform-terms review before launch.

### 7.2 Raw social item schema

```yaml
social_item:
  internal_id:
  platform:
  native_id:
  original_url:
  author_display_policy:
  author_type:
  community_or_channel:
  parent_id:
  thread_id:
  created_at:
  retrieved_at:
  edited_at:
  deleted_at:
  language:
  permitted_excerpt:
  engagement_snapshot:
  display_permission:
  safety_flags: []
  provenance_record:
```

### 7.3 Minimal retention

Store only what is needed for:

- Analysis
- Auditability
- Display permitted by the platform
- Deletion reconciliation
- Model evaluation

Do not retain unnecessary personal-profile data.

### 7.4 Deletion and edits

The system must periodically reconcile featured items. Deleted or materially edited source content must be removed or updated promptly according to platform rules and Civersify policy.

---

## 8. Analysis Pipeline

### 8.1 Relevance filtering

Remove content that is:

- Off-topic
- Pure spam
- Duplicate or copied text
- Unintelligible without unavailable context
- Primarily harassment
- Prohibited graphic content

### 8.2 Content classification

For each item, estimate:

- Argument or concern cluster
- Stance toward a defined proposition
- Claim versus opinion versus prediction
- Question type
- Value or priority
- Emotional framing
- Firsthand-experience indicator
- Satire or sarcasm likelihood
- Coordination or copypasta likelihood
- Toxicity and safety flags

### 8.3 Clustering

Clusters should be based on semantic argument structure, not only keywords or sentiment.

The system should prevent one highly active account or repeated copy from dominating prevalence.

### 8.4 Representative-example selection

Examples should optimize for:

- Clarity
- Faithfulness to the cluster
- Non-duplication
- Evidence of reasoning
- Safety
- Context availability
- Source permission

Engagement is one signal, not the primary ranking criterion.

### 8.5 Human review

Human review is mandatory for:

- Featured examples on sensitive topics
- Firsthand claims about violence or harm
- Claims that could defame a person
- Content involving minors
- Graphic, hateful, or extremist material
- High-visibility misinformation clusters

---

## 9. Methodology Disclosure

Every Conversation Map must publish:

- Platforms and communities included
- Collection dates
- Query or inclusion strategy at a meaningful level
- Number of items collected and number retained
- Deduplication approach
- Known biases
- Whether prevalence is item-based, account-based, or conversation-based
- Human-review status
- Last refresh date

Preferred language:

> Among the public posts collected from the listed sources during this period, the most common concerns were…

Avoid:

> The public believes…

---

## 10. Interactive Experiences

### 10.1 Claim, opinion, or prediction

User classifies a reaction and sees the reasoning.

### 10.2 Find the missing evidence

User selects which source would be necessary to verify a claim.

### 10.3 Same event, different concern

User maps reactions to values such as security, liberty, humanitarian welfare, cost, fairness, or sovereignty.

### 10.4 Viral versus prevalent

Show a highly engaged item next to the broader sampled distribution and explain why engagement is not representativeness.

### 10.5 Did the conversation change?

User compares argument clusters across two periods.

Social content involving trauma, death, or graphic violence should not be turned into playful interactions.

---

## 11. Moderation and Safety

### Required protections

- No unmoderated feed
- No doxxing or personal contact information
- No unnecessary usernames for ordinary individuals
- No graphic images in the default experience
- No slurs or hateful text without strong editorial need and warning
- No amplification of obscure harmful claims merely to debunk them
- No location exposure for vulnerable firsthand witnesses
- Strong controls for content involving minors
- Clear reporting mechanism
- Editorial takedown workflow

### Firsthand accounts

Label as:

- Firsthand account
- Independently corroborated
- Partially corroborated
- Not independently verified

Never present a vivid personal account as verified merely because it is emotionally compelling.

---

## 12. Ranking and Diversity

The cluster ranking system should balance:

- Prevalence in the disclosed sample
- Relevance to the Theme Room
- Distinctiveness
- Evidence quality
- Stakeholder coverage
- Good-faith argument representation
- Safety

It should explicitly reduce:

- Duplicate text
- Coordinated amplification
- Single-account dominance
- Engagement bait
- Extreme content selected only because it is extreme

Diversity refers to argument and stakeholder coverage, not a forced equal number of partisan positions.

---

## 13. Metrics

### Value

- Percentage of users who report better understanding of the disagreement
- Cluster expansion rate
- Related fact or source open rate
- Completion of reaction-classification interactions
- Number of repeated questions converted into knowledge items

### Trust and safety

- User report rate
- Featured-item removal rate
- Deletion reconciliation time
- False-representativeness complaints
- Harmful-content exposure incidents
- Percentage of maps with complete methodology disclosure

---

## 14. MVP Scope

For one pilot Theme Room:

- One approved public-discussion source or a very small number of sources
- Five to eight argument clusters
- Twelve to twenty representative examples
- Five frequently asked questions
- Five to ten repeated claims linked to the claim ledger
- One community-comparison view only if sample sizes are sufficient
- One interaction
- Manual editorial approval
- Public methodology page

Not in MVP:

- Real-time firehose
- Nationwide public-opinion estimates
- Personalized social feed
- Open commenting
- Automated publication
- Comprehensive cross-platform coverage

---

## 15. Acceptance Criteria

The Conversation Map is launch-ready when:

- It clearly states that the sample is not representative public opinion.
- Argument clusters are distinct, understandable, and source-supported.
- Prevalence is calculated from a disclosed method.
- Examples are permitted, linked, contextualized, and reviewed.
- Claims are connected to evidence status.
- Deleted content can be reconciled.
- Toxic, graphic, and personal information protections pass testing.
- No single engagement metric determines prominence.
- The feature remains useful even if usernames and exact quotes are hidden.

---



<!-- END 03_Conversation_Map_PRD.md -->


---

<!-- BEGIN 04_Knowledge_Graph_Content_Platform_PRD.md -->



---

# Product Requirements Document: Knowledge Graph and Content Platform

**Product:** Civersify  
**Feature:** Shared civic knowledge graph, ingestion pipeline, and editorial authoring system  
**Status:** Draft  
**Related documents:** All expansion PRDs

---

## 1. Summary

Civersify requires a shared content platform so bills, Story Rooms, Theme Rooms, actors, claims, budget items, sources, reactions, and interactions can be reused instead of manually copied into separate pages.

The platform should behave like a versioned knowledge graph with editorial workflows, not merely a collection of generated articles.

---

## 2. Problem

Without a shared object model:

- The same fact is rewritten differently across pages
- Corrections do not propagate
- Bills and budgets cannot be consistently connected to stories
- Theme Rooms become expensive special projects
- AI synthesis becomes difficult to audit
- Search and personalization are shallow
- Mini-games require bespoke content preparation

---

## 3. Product Goal

Create a trustworthy, reusable civic-content system that ingests sources, extracts structured objects, supports editorial review, publishes connected experiences, and preserves provenance and revision history.

---

## 4. Core Object Model

### 4.1 Theme

A long-lived public issue.

Key fields:

- Canonical title
- Alternate titles
- Scope
- Geography
- Lifecycle status
- Sensitivity
- Monitoring cadence
- Attached objects

### 4.2 Event

A time-bounded occurrence.

Examples:

- Vote
- Ruling
- Speech
- Attack
- Negotiation
- Data release
- Regulatory action

### 4.3 Story

An editorial synthesis of one event or tightly connected set of events.

### 4.4 Knowledge item

A durable explainer.

Subtypes:

- Concept
- Institution
- Process
- Law
- Place
- Historical event
- Economic mechanism
- Vocabulary term

### 4.5 Actor

A person or organization.

Subtypes:

- Elected official
- Government body
- Court
- Agency
- Country
- International organization
- Company
- Advocacy group
- Community

### 4.6 Policy item

Subtypes:

- Bill
- Resolution
- Executive order
- Agency rule
- Court case
- Treaty
- Sanction
- State or local policy

### 4.7 Budget item

A financial object with a funding stage and source.

### 4.8 Claim

A specific, sourceable assertion with an evidence status.

### 4.9 Source

A primary document, government dataset, direct statement, reporting source, analysis, or public reaction source.

### 4.10 Reaction

A permitted excerpt or synthesis of an official, expert, or public response.

### 4.11 Prediction

A measurable future proposition with a resolution rule.

### 4.12 Interaction

A reusable game or learning object linked to structured content.

---

## 5. Relationship Model

Illustrative relationships:

```text
Theme CONTAINS Story
Story DESCRIBES Event
Story REFERENCES Knowledge Item
Actor PARTICIPATES_IN Event
Actor SPONSORS Bill
Bill RELATES_TO Theme
Budget Item FUNDS Policy Item
Claim ASSERTED_BY Actor
Claim SUPPORTED_BY Source
Claim CONTRADICTED_BY Source
Reaction RESPONDS_TO Event
Prediction ABOUT Theme
Interaction TEACHES Knowledge Item
Interaction USES Claim
```

Relationships require:

- Type
- Direction
- Confidence
- Source
- Valid-from date
- Valid-to date where relevant
- Revision history

---

## 6. Identity and Canonicalization

### 6.1 Canonical IDs

Every object receives a stable internal identifier independent of title or URL.

### 6.2 Alias support

Support alternate names, abbreviations, translations, old names, and disputed terminology.

### 6.3 Entity resolution

The system should propose merges when two records likely represent the same object. Human approval is required for high-impact merges.

### 6.4 Temporal validity

Roles and relationships change. The graph must support time-bounded facts such as:

- Person held office during a period
- Bill was in committee before advancing
- Claim status changed after new evidence

---

## 7. Provenance Model

Every published factual statement must connect to:

- Source ID
- Exact supporting passage or structured field where permitted
- Retrieval timestamp
- Transformation history
- Model or editor that created the derived statement
- Reviewer
- Revision ID

For generated summaries, provenance should be sentence-level or claim-level rather than page-level.

---

## 8. Ingestion Pipeline

```text
Source Discovery
  ↓
Retrieval and Metadata Capture
  ↓
Document Normalization
  ↓
Event, Entity, Claim, and Number Extraction
  ↓
Deduplication and Entity Resolution
  ↓
Candidate Relationships
  ↓
AI Draft Synthesis
  ↓
Editorial Review
  ↓
Publication
  ↓
Monitoring, Corrections, and Propagation
```

### 8.1 Source discovery

Sources may be discovered through:

- Official government feeds
- Legislative data
- Court publications
- Agency releases
- News feeds
- Approved social APIs
- Manual editorial entry

### 8.2 Normalization

Preserve:

- Original URL
- Title
- Author or issuing organization
- Publication and modification dates
- Source type
- Jurisdiction
- Language
- Full-text availability
- Rights and display constraints

### 8.3 Structured extraction

The system proposes:

- Events
- Actors
- Claims
- Dates
- Locations
- Monetary amounts
- Policy identifiers
- Quotes
- Relationships

### 8.4 Deduplication

Distinguish:

- Duplicate articles
- Syndicated reporting
- Updated versions
- Separate reports of the same event
- Repeated social content

---

## 9. Claim Ledger

Claims are first-class objects rather than strings embedded in articles.

Required fields:

```yaml
claim:
  id:
  normalized_text:
  claim_type:
  subject_ids: []
  predicate:
  object_value:
  time_scope:
  geography_scope:
  asserted_by_ids: []
  evidence_status:
  confidence:
  supporting_source_ids: []
  contradicting_source_ids: []
  first_seen_at:
  last_reviewed_at:
  reviewer:
  status_history: []
```

Allowed evidence statuses must be centrally defined and consistently displayed.

---

## 10. Editorial CMS

Editors need views for:

### 10.1 Candidate queue

- Potential new Story Rooms
- Significant Theme updates
- Conflicting claims
- New bill or budget links
- Prediction resolutions

### 10.2 Object editor

- Field-level provenance
- Relationship management
- Duplicate merge tools
- Revision comparison
- Source preview
- Sensitivity flags

### 10.3 Page composer

Editors assemble Story Rooms and Theme Rooms from existing objects and choose presentation modules.

### 10.4 Review workflow

Suggested states:

- Ingested
- Structured draft
- Editorial review
- Trust and safety review
- Approved
- Published
- Correction required
- Archived

### 10.5 Propagation dashboard

When a claim changes, show every dependent page and interaction requiring review.

---

## 11. Content APIs

Illustrative endpoints:

```text
GET /themes/{id}
GET /themes/{id}/updates?since_revision=
GET /stories/{id}
GET /claims/{id}
GET /actors/{id}
GET /policy-items/{id}
GET /budget-items/{id}
GET /predictions/{id}
GET /search?q=
POST /editorial/candidates
POST /editorial/revisions
```

The public API should return:

- Stable object IDs
- Current revision
- Display-ready content
- Provenance references
- Related objects
- User completion state when authenticated

---

## 12. Search and Discovery

Search should understand:

- Exact names and aliases
- Natural-language questions
- Bills and identifiers
- Actors and institutions
- Themes
- Claims
- Dates
- Geography

Results should separate:

- Current developments
- Durable explainers
- Bills and official actions
- Claims and evidence
- Predictions

---

## 13. Personalization and Knowledge State

The platform may maintain a user knowledge state containing:

- Viewed Story Rooms
- Completed interactions
- Knowledge items understood or revisited
- Followed themes
- Predictions made
- Preferred explanation depth

This state can help avoid repetitive explanations and recommend prerequisite concepts. It must not be used to create an ideological echo chamber.

---

## 14. Versioning and Corrections

### 14.1 Immutable revisions

Published revisions should remain auditable.

### 14.2 Correction propagation

When a fact, claim, number, or entity link changes:

1. Create a new object revision.
2. Flag dependent content.
3. Require editorial review where the change is material.
4. Publish updated pages.
5. Record a user-facing correction note when prior information was wrong.

### 14.3 Source withdrawal

If a source is removed, retracted, or becomes unavailable, the platform should identify affected claims and pages.

---

## 15. Security and Governance

- Role-based editorial permissions
- Audit logs for every published change
- Separation between ingestion data and published data
- Encrypted secrets for source connectors
- Rate and quota controls
- Retention policies by source type
- PII minimization
- Backup and restoration testing
- Model-version tracking

---

## 16. Observability

Track:

- Ingestion failures
- Source freshness
- Extraction confidence
- Duplicate rate
- Editorial queue age
- Claim conflicts
- Correction propagation time
- Broken source links
- Social deletion reconciliation
- Model drift in classifications

---

## 17. MVP Scope

The MVP platform must support:

- Themes
- Events
- Stories
- Knowledge items
- Actors
- Bills or policy items
- Budget items
- Claims
- Sources
- Predictions
- Interactions
- Revision history
- Editorial approval
- Basic relationship graph
- Full-text and entity search

The MVP may use human-assisted entity resolution and manual attachment of some relationships.

---

## 18. Acceptance Criteria

The platform is ready for the first public Theme Room when:

- One fact can be reused across multiple pages without duplication.
- A correction to a claim flags all affected pages.
- Every Story Room sentence can be traced to supporting sources.
- Bills and budget items can appear on both native pages and Theme Rooms.
- Editors can inspect AI-proposed relationships before publication.
- Object revisions are auditable.
- Search returns current stories and durable knowledge separately.
- Sensitive social content remains outside public pages until reviewed.

---



<!-- END 04_Knowledge_Graph_Content_Platform_PRD.md -->


---

<!-- BEGIN 05_Money_Trail_Budget_Intelligence_PRD.md -->



---

# Product Requirements Document: Money Trail and Budget Intelligence

**Product:** Civersify  
**Feature:** Money Trail  
**Status:** Draft  
**Related documents:** Theme Rooms, Story Rooms, Knowledge Graph, Interactive News Engagement

---

## 1. Summary

Money Trail explains the financial dimension of public policy and current events. It distinguishes announcements from actual funding and connects dollars to government process, recipients, time periods, outcomes, and uncertainty.

The feature can appear on:

- Bill pages
- Story Rooms
- Theme Rooms
- Agency or actor pages
- Standalone budget explainers
- Interactive allocation games

---

## 2. Problem

News coverage frequently presents large numbers without explaining what they mean. Users may incorrectly assume that:

- Requested money has already been approved
- Authorized money has been appropriated
- Appropriated money has already been spent
- A multi-year total is an annual amount
- A gross cost equals the net budget effect
- An estimate is a recorded expenditure
- Aid announced to a country is a direct cash transfer

These distinctions are important and highly teachable.

---

## 3. Product Goal

Enable users to understand where a public-policy number came from, what stage it is in, how it compares with relevant benchmarks, who controls it, who may receive it, and what remains uncertain.

---

## 4. Funding Stage Vocabulary

The product should use a consistent taxonomy.

### Requested

An executive branch, agency, legislator, or other actor has proposed funding.

### Authorized

Law permits or establishes a program or spending level, but may not provide spendable funds.

### Appropriated

Law provides budget authority for a defined purpose and period.

### Allocated or apportioned

Funds are divided among programs, agencies, recipients, or time periods.

### Obligated

The government has made a legally binding commitment to spend.

### Outlay or spent

Cash has been disbursed.

### Estimated

A projected amount, not a completed transaction.

### Economic effect

A modeled or observed indirect impact, such as price changes, lost output, or tax effects.

Definitions may vary by jurisdiction. Each item should preserve the terminology used by its authoritative source while mapping it to the Civersify taxonomy.

---

## 5. User Experience

### 5.1 Money summary card

Displays:

- Headline amount
- Funding stage
- Time period
- Responsible authority
- Source
- “What this number does not mean” note

### 5.2 Money ladder

A visual progression:

```text
Requested → Authorized → Appropriated → Obligated → Spent
```

The active stage is highlighted. Missing or unavailable stages are shown explicitly.

### 5.3 Composition view

Break down money by:

- Program
- Agency
- Recipient type
- Geography
- Fiscal year
- Purpose
- Direct versus indirect support

### 5.4 Comparison view

Offer contextual comparisons carefully:

- Per household or per person
- Percentage of relevant agency budget
- Percentage of total federal spending
- Comparison with prior years
- Comparison with another program

Avoid frivolous comparisons that trivialize human suffering or serious policy choices.

### 5.5 Source and uncertainty

Users can inspect:

- Primary budget source
- Estimate method
- Revision history
- Confidence or range
- Known exclusions
- Double-counting warning

### 5.6 Who decides?

Explain which actor controls the next step:

- President or executive branch
- Congress
- Committee
- Agency
- State government
- Court
- International institution

---

## 6. Theme Room Integration

For a conflict or major policy theme, Money Trail may include:

- Direct operations
- Foreign military assistance
- Humanitarian aid
- Equipment replacement
- Transportation and logistics
- Sanctions enforcement
- Long-term personnel or veteran costs
- Energy prices
- Shipping and insurance effects

The product must visually distinguish recorded government spending from broader modeled economic effects.

---

## 7. Bill Page Integration

Every bill with meaningful budget implications should show:

- Official cost estimate when available
- Time horizon
- Spending versus revenue effects
- Mandatory versus discretionary components
- Major assumptions
- Related appropriations
- Current legislative status

The absence of an official estimate should be stated rather than filled with an unsupported estimate.

---

## 8. Data Model

```yaml
budget_item:
  id:
  title:
  jurisdiction:
  source_program_name:
  civersify_category:
  stage:
  amount:
  currency:
  amount_min:
  amount_max:
  fiscal_year_start:
  fiscal_year_end:
  one_time_or_recurring:
  gross_or_net:
  mandatory_or_discretionary:
  direct_or_indirect:
  requesting_actor_ids: []
  authorizing_policy_ids: []
  appropriating_policy_ids: []
  implementing_agency_ids: []
  recipient_ids: []
  geography_ids: []
  purpose_tags: []
  source_ids: []
  estimate_method:
  confidence:
  exclusions: []
  related_budget_item_ids: []
  last_reviewed_at:
  revision_history: []
```

---

## 9. Number Integrity Requirements

### 9.1 Unit normalization

Store raw value and normalized value. Preserve whether the source uses nominal or inflation-adjusted dollars.

### 9.2 Time horizon

Every amount requires a clear period. A ten-year score must never be shown as though it were a one-year amount.

### 9.3 Avoid double counting

The platform should flag overlapping appropriations, reprogrammed funds, and nested totals.

### 9.4 Ranges and uncertainty

Use ranges when sources do. Do not collapse an uncertain estimate into a false point value.

### 9.5 Revisions

Budget estimates and reported outlays can change. Store revision history and the source date.

---

## 10. Interactive Experiences

### 10.1 Budget allocator

The user divides a fixed amount across categories and then compares the result with an actual proposal.

Rules:

- Explain constraints
- Show tradeoffs
- Avoid implying the user has recreated a full government budget
- Do not score a politically preferred allocation as correct

### 10.2 Guess the stage

Show a headline and ask whether the money was requested, authorized, appropriated, obligated, or spent.

### 10.3 Who pays, who receives?

Users map funding sources and recipients.

### 10.4 Find the misleading number

Users identify a timeframe, inflation, denominator, or double-counting problem.

### 10.5 Cost versus effect

Users distinguish a government outlay from an estimated economic consequence.

---

## 11. Editorial Workflow

1. Ingestion extracts monetary references.
2. System proposes a budget item and funding stage.
3. Editor identifies the authoritative source.
4. Editor verifies amount, period, stage, and relationship to policy.
5. System checks for potential duplicates or nested totals.
6. Item publishes and attaches to related pages.
7. Later reports update obligation or outlay stages.

High-impact totals require secondary review.

---

## 12. Metrics

### Understanding

- Percentage correctly distinguishing funding stages
- Reduction in misinterpretation of multi-year totals
- Completion of Money Trail interactions

### Product value

- Money module expansion rate
- Source open rate
- Cross-navigation between bill, Story Room, and Theme Room

### Quality

- Number correction rate
- Double-counting incidents
- Percentage of amounts with clear timeframe and source
- Time to incorporate revised official estimates

---

## 13. MVP Scope

For the first pilot:

- Funding-stage taxonomy
- Money summary cards
- Money ladder
- Basic category breakdown
- Authoritative source links
- One budget allocator
- One “guess the stage” interaction
- Integration with one bill and one Theme Room

Not in MVP:

- Full federal ledger
- Real-time expenditure tracking
- Comprehensive state and local budgets
- Automated economic modeling
- Personalized tax estimates

---

## 14. Acceptance Criteria

A Money Trail item is publishable when:

- Amount, currency, and period are explicit.
- Funding stage is correct and source-supported.
- Request, authorization, appropriation, obligation, and spending are not conflated.
- The source and revision date are visible.
- Estimates are labeled as estimates.
- Known exclusions and ranges are shown.
- Related policy and responsible actors are linked.
- Any comparison is mathematically and contextually defensible.

---



<!-- END 05_Money_Trail_Budget_Intelligence_PRD.md -->


---

<!-- BEGIN 06_Interactive_News_Engagement_PRD.md -->



---

# Product Requirements Document: Interactive News Engagement

**Product:** Civersify  
**Feature:** Reusable games, predictions, Civic Sprint, and learning mechanics  
**Status:** Draft  
**Related documents:** Story Rooms, Theme Rooms, Knowledge Graph, Editorial Trust & Safety

---

## 1. Summary

Interactive News Engagement turns passive news consumption into active reasoning. The system provides reusable interaction types that can be attached to Story Rooms, Theme Rooms, bills, budget items, knowledge items, and social reaction clusters.

The product should gamify:

- Curiosity
- Evidence seeking
- Source evaluation
- Institutional understanding
- Tradeoffs
- Perspective taking
- Calibrated prediction
- Willingness to update a view

It should not gamify partisan victory, human suffering, or outrage.

---

## 2. Problem

Traditional article reading produces weak recall and little evidence that the user understood:

- Which statements were factual
- Which mechanisms caused an outcome
- Which institutions have authority
- What remains uncertain
- What tradeoffs exist

Many civics games are disconnected from current events, while news quizzes often reward trivia rather than understanding.

---

## 3. Product Goal

Create short, repeatable interactions that deepen understanding of live issues and generate reasons to return without creating addictive or ideologically manipulative engagement loops.

---

## 4. Design Principles

### 4.1 Match mechanics to learning goal

A map interaction should teach geography. A budget allocator should teach constrained tradeoffs. A prediction should teach uncertainty.

### 4.2 Explain every result

Correctness without explanation is insufficient. Each response should show:

- Why the answer is supported
- Which source supports it
- Why common alternatives are tempting
- What remains uncertain

### 4.3 No ideological answer key

Tradeoff and values exercises can reveal consequences, but should not label one reasonable policy preference as objectively correct.

### 4.4 Finite sessions

Interactions should have a clear completion state. Avoid endless randomized loops.

### 4.5 Safe treatment of serious events

Do not turn deaths, casualties, graphic violence, or personal trauma into points, streaks, or playful spectacle.

---

## 5. Interaction Catalog

### 5.1 Before You Know

Ask a meaningful question before revealing the synthesis.

Learning goal: expose assumptions and create curiosity.

### 5.2 Fact, Opinion, Interpretation, or Prediction

User classifies a statement.

Learning goal: distinguish epistemic categories.

### 5.3 Headline or Hype

User chooses the headline best supported by underlying facts.

Learning goal: identify exaggeration and missing context.

### 5.4 Source Trail

User traces a claim from social post to article, press release, report, dataset, or primary document.

Learning goal: understand provenance.

### 5.5 What Is Missing?

User identifies omitted context from a technically factual statement.

Learning goal: recognize selection and framing effects.

### 5.6 Chart Trap

User identifies truncated axes, timeframe manipulation, denominator confusion, or correlation claims.

Learning goal: data literacy.

### 5.7 Timeline Builder

User orders events and then sees what was known at each point.

Learning goal: chronology and hindsight-bias reduction.

### 5.8 Power Match

User matches an actor or institution to its actual legal or practical power.

Learning goal: institutional literacy.

### 5.9 Map Challenge

User locates relevant places, routes, districts, or jurisdictions.

Learning goal: geographic context.

### 5.10 Consequence Tree

User chooses likely downstream effects and sees evidence and uncertainty.

Learning goal: causal reasoning.

### 5.11 Vote Before Reading

User records an initial view, reviews provisions and arguments, and votes again.

Learning goal: observe whether evidence changes a position.

### 5.12 Build an Amendment

User chooses policy modifications and sees stakeholder or legislative effects.

Learning goal: policy design and tradeoffs.

### 5.13 Coalition Builder

User assembles support among actors with different constraints.

Learning goal: negotiation and institutional incentives.

### 5.14 Budget Allocator

User allocates finite resources and compares with an actual proposal.

Learning goal: opportunity cost and budget structure.

### 5.15 Calibrated Prediction

User assigns a probability to a measurable outcome.

Learning goal: uncertainty and calibration.

---

## 6. Daily Civic Sprint

The Civic Sprint is a three-to-five-minute bounded session.

### Format

#### Story 1: Know It

One major current development and a factual or predictive prompt.

#### Story 2: Understand It

A durable concept, institution, timeline, or map connected to current news.

#### Story 3: Question It

A claim, chart, headline, source, or reaction to evaluate.

### Completion screen

- Three things learned
- One item that remains uncertain
- One prediction or question
- Optional share card
- Optional follow action

### Streak policy

Streaks represent civic learning sessions, not ideological agreement. Missed days should not use shame, loss aversion, or aggressive notifications.

---

## 7. Prediction System

### 7.1 Prediction requirements

Every question must have:

- Clear proposition
- Close date or resolution condition
- Objective resolution source
- Allowed outcomes
- Cancellation policy
- Editorial owner

### 7.2 Probability input

Users enter a probability rather than only yes or no.

### 7.3 Resolution

When resolved, show:

- Outcome
- Evidence
- User’s prior probability
- Aggregate distribution
- Calibration insight
- Related Story Room

### 7.4 Scoring

Use a proper scoring method suitable for probabilities. The product may simplify the presentation, but it must not reward unjustified certainty.

### 7.5 Calibration profile

Over time, users may see:

- Accuracy by confidence band
- Topics with overconfidence
- Topics with underconfidence
- Improvement over time

Do not turn geopolitical tragedy or personal harm into competitive leaderboards.

---

## 8. Values and Tradeoff Integration

Civersify’s values profile should emerge from constrained decisions rather than labels such as progressive or conservative.

Potential dimensions:

- Speed versus procedural safeguards
- National consistency versus local flexibility
- Privacy versus enforcement
- Broad modest support versus narrow intensive support
- Short-term cost versus long-term benefit
- Individual choice versus collective coordination
- Prevention versus response

After a choice, explain the policy consequences and affected stakeholders. Do not tell the user what political identity they “really are.”

---

## 9. Game Engine Requirements

### 9.1 Reusable schema

```yaml
interaction:
  id:
  type:
  title:
  learning_objective:
  prompt:
  content_object_ids: []
  options: []
  answer_model:
  explanation:
  source_ids: []
  sensitivity:
  age_guidance:
  scoring_mode:
  completion_event:
  revision_id:
```

### 9.2 Authoring tools

Editors should be able to:

- Select a template
- Attach claims, actors, sources, or budget items
- Preview answer explanations
- Set sensitivity and age guidance
- Test screen-reader behavior
- Review analytics

### 9.3 Versioning

When a linked claim or fact changes, affected interactions must be flagged for revalidation.

### 9.4 Offline answer integrity

Correct answers and explanations should not depend on a live model response at play time. They should be generated, reviewed, and versioned before publication for high-impact interactions.

---

## 10. Rewards and Progress

Recommended rewards:

- Knowledge-path completion
- Source explorer badge
- Calibration improvement
- Thoughtful revision badge
- Strong question recognition
- Theme mastery progress

Avoid:

- Partisan leaderboards
- Public shaming for wrong answers
- Rewards for posting frequently
- Random variable rewards
- Artificial urgency unrelated to real events

---

## 11. Social and Classroom Use

### Shareable artifacts

- Three things I learned
- My prediction
- The tradeoff I found hardest
- One fact that surprised me
- What both sides agree on
- A completed consequence tree

### Classroom mode

- Assign a fixed sequence
- Hide aggregate predictions until submission
- Provide discussion prompts
- Do not expose student political views publicly
- Allow teacher-controlled pacing

---

## 12. Metrics

### Learning

- Pre/post comprehension change
- Classification accuracy
- Source-selection accuracy
- Calibration improvement
- Retention after several days

### Engagement quality

- Completion rate
- Share rate for learning artifacts
- Follow-through to source or knowledge item
- Return on prediction resolution

### Guardrails

- User distress reports
- Ideological unfairness reports
- Mis-scored interaction incidents
- Sensitive-content game violations
- Notification opt-out rate

---

## 13. MVP Scope

Implement four reusable interaction types:

1. Before You Know
2. Fact, Opinion, Interpretation, or Prediction
3. Timeline Builder
4. Calibrated Prediction

Add one specialized bill interaction:

5. Vote Before Reading

Pilot a three-item Civic Sprint using Story Rooms and existing mini-game infrastructure.

---

## 14. Acceptance Criteria

An interaction is publishable when:

- It has one clear learning objective.
- The answer and explanation are source-grounded.
- It does not require ideological agreement.
- It is age-appropriate for its labeled audience.
- It is keyboard and screen-reader accessible.
- It handles uncertainty explicitly.
- It remains correct for the attached content revision.
- Serious human harm is not trivialized.
- Completion analytics can be measured without collecting unnecessary sensitive data.

---



<!-- END 06_Interactive_News_Engagement_PRD.md -->


---

<!-- BEGIN 07_Editorial_Trust_Safety_PRD.md -->



---

# Product Requirements Document: Editorial Trust and Safety

**Product:** Civersify  
**Feature:** Editorial standards, provenance, corrections, moderation, and youth safety  
**Status:** Draft  
**Related documents:** All expansion PRDs

---

## 1. Summary

Civersify’s product value depends on trust. Interactive design increases attention and memorability, which also increases the harm of an error, misleading frame, unsafe social example, or falsely confident synthesis.

This PRD defines the standards and systems required for source quality, uncertainty, neutrality, corrections, conflict coverage, social content, minors, AI-assisted publishing, and auditability.

---

## 2. Product Goal

Ensure that users can distinguish facts from interpretations, understand source quality and uncertainty, inspect evidence, encounter good-faith disagreements, and trust that corrections will be visible and propagated.

---

## 3. Editorial Principles

### 3.1 Accuracy before speed

Civersify should not compete to be the first source for an unverified breaking claim.

### 3.2 Clear epistemic labeling

Every meaningful assertion should be presented as one of:

- Fact
- Interpretation
- Opinion
- Forecast
- Unverified report
- Disputed claim
- Unknown

### 3.3 Strongest good-faith arguments

When presenting disagreement, describe the strongest evidence-based version rather than selecting weak or inflammatory examples.

### 3.4 Neutrality is not false equivalence

Do not force equal weight where evidence is not equal. Explain differences in evidence quality directly.

### 3.5 Terminology matters

Names such as “war,” “invasion,” “riot,” “terrorist,” “genocide,” “recession,” or “constitutional crisis” may carry legal or contested meanings. Use descriptive language, attribute disputed labels, and include terminology notes when necessary.

### 3.6 Corrections are product features

A visible correction increases credibility. Silent edits are insufficient for material errors.

---

## 4. Source Hierarchy

Source type must be displayed separately from trust assessment.

### Primary sources

- Statutes and bills
- Court opinions
- Official datasets
- Government reports
- Recorded votes
- Direct transcripts
- Original research

### Direct statements

- Press releases
- Speeches
- Social posts from identified actors

Direct statements establish what an actor said, not whether the statement is true.

### Reporting

- Original reporting
- Wire services
- Local reporting
- Specialized reporting

### Analysis

- Academic analysis
- Think tanks
- Expert commentary
- Opinion journalism

### Public reactions

- Social posts
- Forum comments
- User-generated media

Public reactions may demonstrate that a view exists; they generally do not verify factual claims.

---

## 5. Source Selection Requirements

For high-impact Story Rooms:

- Prefer primary sources for official actions and quantitative facts.
- Use multiple independent reports for contested breaking events when possible.
- Avoid circular sourcing where outlets repeat one unverified origin.
- Record publication and update dates.
- Note when a source has a direct interest in the claim.
- Preserve conflicting authoritative evidence rather than silently resolving it.

---

## 6. Claim Review

### 6.1 Claim statuses

- Confirmed
- Strongly supported
- Plausible but unresolved
- Disputed
- Unsupported
- False
- Outdated
- Prediction

### 6.2 Review requirements

A claim record must show:

- Exact claim
- Who asserted it
- Evidence for it
- Evidence against it
- Geographic and time scope
- Reviewer
- Last-reviewed date

### 6.3 Status changes

Status history must remain visible internally. Material public changes require a changelog or correction note.

---

## 7. AI-Assisted Publishing Rules

AI may:

- Extract candidate facts and claims
- Propose summaries
- Suggest object relationships
- Cluster arguments
- Draft interaction explanations

AI may not autonomously publish high-impact civic content.

Required controls:

- Approved source corpus
- Prompt and model version logging
- Field-level provenance
- Confidence thresholds
- Human approval
- Regression tests
- Hallucination audits
- Propagation review after model changes

The system should prefer “insufficient evidence” over filling gaps.

---

## 8. Breaking-News Protocol

### 8.1 Initial state

When facts are incomplete, publish a limited update containing:

- What is confirmed
- What is being reported but not confirmed
- What is unknown
- When the page was last reviewed

### 8.2 Escalation thresholds

Require senior review for:

- Casualty claims
- Election outcomes
- Military attribution
- Criminal accusations
- Public-health emergencies
- Market-sensitive government actions
- Claims involving minors

### 8.3 Update cadence

The page should update when evidence changes, not on an arbitrary minute-by-minute schedule.

---

## 9. Conflict, Violence, and Humanitarian Content

- Use restrained imagery.
- Do not display graphic content by default.
- Provide content notes.
- Do not turn casualty numbers into competitive or playful mechanics.
- Distinguish official claims from independent verification.
- Avoid exposing precise personal locations of vulnerable people.
- Include humanitarian effects without using suffering as engagement bait.
- Provide historical and legal context without implying legal conclusions not established by authoritative processes.

---

## 10. Political Fairness

### Required practices

- Attribute positions accurately.
- Separate policy mechanism from political messaging.
- Include material good-faith objections.
- Avoid loaded headlines.
- Use symmetrical standards for equivalent claims.
- Do not infer an actor’s motive as fact without evidence.
- Distinguish a group’s official position from the views of all members.

### Fairness review prompts

- Would the headline be acceptable if the parties were reversed?
- Is evidence quality being confused with ideological balance?
- Is a fringe statement presented as representative?
- Is uncertainty expressed consistently?
- Are stakeholders missing?

---

## 11. Social Content Safety

- No unmoderated public feed
- Minimal retention
- Platform-compliant use
- Deletion reconciliation
- PII removal
- Doxxing detection
- Hate and harassment filters
- Graphic-content filters
- Human review for featured examples
- Clear sample methodology
- No claim of representative public opinion

Ordinary users should generally be anonymized or summarized unless identity is essential and display is permitted.

---

## 12. Youth and Family Safety

Civersify should assume some users are minors even if the general product is not exclusively child-directed.

Requirements:

- Age-appropriate default language
- No targeted political advertising based on inferred views
- No public display of a minor’s political profile
- No unnecessary collection of precise location, school, or contact details
- Strong controls for classroom accounts
- Parent and teacher guidance for sensitive themes
- Clear content warnings
- Easy exit from distressing material
- No manipulative streak or notification mechanics

Any child-directed features require dedicated legal and privacy review before launch.

---

## 13. Corrections and Transparency

### 13.1 Correction types

- Typographical
- Clarification
- Factual correction
- Source correction
- Retraction
- Material framing revision

### 13.2 User-facing display

Material corrections should include:

- What was wrong
- What changed
- When it changed
- Why it changed

### 13.3 Propagation

The platform must identify every dependent page, interaction, share card, and prediction affected by the correction.

---

## 14. Auditability

Maintain internal records for:

- Source ingestion
- AI model and prompt version
- Editor and reviewer actions
- Claim-status changes
- Page revisions
- Social example selection
- Moderation decisions
- Corrections
- User reports

---

## 15. User Reporting

Allow users to report:

- Factual error
- Missing context
- Misleading headline
- Partisan framing
- Broken source
- Unsafe or graphic content
- Privacy concern
- Social-content misuse

Reports should enter a triaged editorial queue with severity and response tracking.

---

## 16. Quality Assurance

### Pre-publication checks

- Provenance completeness
- Claim-status consistency
- Source diversity
- Number and date validation
- Terminology review
- Accessibility
- Youth-safety review
- Interaction answer validation

### Periodic audits

- Sample of published pages
- Model extraction quality
- Political framing consistency
- Social clustering bias
- Correction propagation
- Stale Theme Rooms

---

## 17. Metrics

- Percentage of factual statements with complete provenance
- Material correction rate
- Median correction propagation time
- User-rated fairness
- User-rated clarity
- Trust and safety report rate
- Social deletion reconciliation time
- Percentage of sensitive content receiving required review
- Stale-content rate

---

## 18. Acceptance Criteria

A Civersify expansion feature is launch-ready only when:

- Facts, interpretations, opinions, and predictions are distinguishable.
- Material assertions have source-level provenance.
- Uncertainty language follows a shared standard.
- Corrections can propagate across reused content.
- Social samples include methodology and safety review.
- Youth safeguards are implemented.
- High-risk content has escalation and review procedures.
- Editors can audit what the AI proposed and what humans approved.

---



<!-- END 07_Editorial_Trust_Safety_PRD.md -->


---

<!-- BEGIN 08_MVP_Rollout_and_Operating_Plan.md -->



---

# Civersify Theme and Story Expansion: MVP Rollout and Operating Plan

**Product:** Civersify  
**Status:** Draft execution plan  
**Purpose:** Sequence the PRDs into a testable launch without building a full-scale news organization or social-data platform first.

---

## 1. Recommended Pilot

Build one high-quality Theme Room around a major evolving issue with enough depth to exercise the platform.

A possible pilot is a carefully titled room such as:

> **U.S.–Iran Conflict and Regional Escalation**

The title is a placeholder and must be reviewed against the facts and terminology at launch. The pilot is useful because it can connect:

- Multiple Story Rooms
- Geography
- Historical knowledge
- Government powers
- Congressional bills or resolutions
- Budget and economic effects
- Claims and uncertainty
- Official and public reactions
- Predictions

A less sensitive domestic policy theme may be run in parallel for comparison if resources permit.

---

## 2. MVP Product Package

### Theme Room

- Overview and current status
- “What changed” view
- Five to ten Story Rooms
- Timeline
- Ten to twenty knowledge items
- Actor and institution map
- Government and law section
- Money Trail
- Claim ledger
- Three predictions
- Conversation Map
- Sources and methodology

### Story Rooms

- At least one bill story
- One executive, agency, or court story
- Two or more general development stories
- One economic or budget story

### Interactions

- Before You Know
- Fact, Opinion, Interpretation, or Prediction
- Timeline Builder
- Calibrated Prediction
- Vote Before Reading for a bill

### Conversation Map

- One approved public source or tightly controlled set of sources
- Five to eight argument clusters
- Twelve to twenty examples
- Methodology disclosure
- Manual review

---

## 3. Workstreams

### 3.1 Content model and platform

Deliver:

- Core object schemas
- Revision history
- Provenance
- Editorial CMS basics
- Relationship graph
- Public content API

### 3.2 Story Room template

Deliver:

- General story template
- Bill-specific template
- Claim ledger
- Source panel
- Interaction slot
- Changelog

### 3.3 Theme Room template

Deliver:

- Overview
- Latest and delta view
- Story collections
- Understand section
- Actors
- Government
- Money
- Claims
- Predictions
- Conversation

### 3.4 Money Trail

Deliver:

- Funding-stage taxonomy
- Money card
- Money ladder
- Basic breakdown
- One budget interaction

### 3.5 Conversation Map

Deliver:

- One compliant connector
- Normalization
- Relevance and safety filters
- Argument clustering
- Editorial review UI
- Methodology page

### 3.6 Interactive engine

Deliver:

- Four reusable templates
- One bill-specific template
- Completion and learning events
- Prediction resolution

### 3.7 Editorial and trust

Deliver:

- Source standards
- Claim-status definitions
- Review checklists
- Correction workflow
- Sensitive-content escalation
- Social-content moderation

---

## 4. Suggested Build Phases

### Phase 0: Editorial prototype

Purpose: Validate information architecture before automation.

- Manually assemble a Theme Room in a design prototype or static data file.
- Use real Civersify bill objects where possible.
- Test the “Catch Up → Understand → Explore → Predict” flow.
- Conduct five to ten user sessions across adults, young adults, and educators.

Exit criteria:

- Users can explain what changed and why it matters.
- The room does not feel like an overwhelming encyclopedia.
- Terminology and uncertainty labels are understood.

### Phase 1: Structured Story Rooms

Purpose: Establish the atomic content unit.

- Implement Story Bundle schema.
- Upgrade one existing bill page into a Story Room.
- Add provenance, claim status, and one interaction.
- Add editorial review and revision history.

Exit criteria:

- A correction propagates to the page and its attached interaction.
- Users can distinguish facts from unresolved claims.

### Phase 2: Theme Room pilot

Purpose: Prove connected synthesis.

- Build the pilot Theme Room.
- Add actors, timeline, knowledge items, bills, and Money Trail.
- Add follow and “since last visit.”

Exit criteria:

- Returning users understand what changed without rereading the room.
- Cross-navigation between stories, bills, and knowledge items is used.

### Phase 3: Predictions and Civic Sprint

Purpose: Create a healthy return loop.

- Add measurable predictions.
- Implement resolution workflow.
- Create a three-item Civic Sprint using pilot content.

Exit criteria:

- Users return to resolved predictions.
- The Sprint produces measurable learning without relying on trivia.

### Phase 4: Conversation Map beta

Purpose: Test whether social discourse adds understanding.

- Add one compliant source.
- Cluster arguments and questions.
- Publish sampling methodology.
- Manually review every featured item.

Exit criteria:

- Users understand major disagreements better.
- Users do not interpret the sample as representative polling.
- Safety and takedown workflows function.

### Phase 5: Scale and automation

Purpose: Increase coverage after quality is proven.

- Add source connectors.
- Improve candidate detection.
- Expand interaction templates.
- Add more Theme Rooms.
- Add teacher and family modes.

---

## 5. Minimum Team Functions

These may be combined across individuals in an early-stage implementation.

- Product owner
- Product designer
- Front-end engineer
- Back-end or platform engineer
- Data or ML engineer
- Editorial lead
- Researcher or fact checker
- Trust and safety owner
- Legal or privacy reviewer on an as-needed basis

The product should not scale social ingestion faster than editorial and safety review capacity.

---

## 6. Content Operating Rhythm

### Daily

- Review candidate meaningful updates
- Check active claims and corrections
- Resolve predictions when conditions are met
- Review social safety queue

### Weekly

- Audit Theme Room freshness
- Refresh Conversation Map sample
- Review repeated user questions
- Convert high-value questions into knowledge items
- Review interaction performance

### Monthly

- Source-quality audit
- Political-framing audit
- Social-clustering bias review
- Correction propagation test
- Youth-safety review
- Archive or move dormant themes to monitoring state

---

## 7. Experiment Plan

### Experiment A: Theme Room versus article list

Compare:

- Comprehension
- Confidence calibration
- Return rate
- Source opens
- Perceived overwhelm

### Experiment B: Pre-question versus no pre-question

Measure whether “Before You Know” improves retention without creating frustration.

### Experiment C: Social Conversation Map

Compare the same Theme Room with and without the Conversation Map.

Measure:

- Understanding of disagreement
- Perceived fairness
- Misinterpretation as public opinion
- Safety reports

### Experiment D: Prediction return loop

Measure return rate when a prediction resolves compared with a standard follow notification.

### Experiment E: Money Trail

Measure whether users correctly distinguish requested, appropriated, and spent amounts.

---

## 8. Launch Metrics

### Required launch indicators

- Strong completion of the Catch Up path
- Measurable comprehension improvement
- High provenance completeness
- Low material-error rate
- Successful correction propagation
- Acceptable user-rated fairness
- No critical youth-safety failures

### Secondary indicators

- Theme follows
- Prediction participation
- Source opens
- Share-card creation
- Story-to-knowledge navigation
- Civic Sprint completion

Avoid making raw session length the primary success metric.

---

## 9. Go/No-Go Gates

### Gate 1: Story quality

Do not launch Theme Rooms until Story Rooms have reliable provenance and corrections.

### Gate 2: Data reuse

Do not scale Theme Rooms if content is still manually copied rather than attached as shared objects.

### Gate 3: Social safety

Do not launch public-reaction ingestion without platform compliance, deletion handling, methodology disclosure, and human review.

### Gate 4: Healthy engagement

Do not add competitive leaderboards or aggressive notifications unless research shows they improve learning without harmful behavior.

### Gate 5: Editorial capacity

Do not increase active Theme Room count beyond the team’s ability to keep them accurate and current.

---

## 10. Initial Backlog

### P0

- Object schemas
- Story Room template
- Theme Room overview and Latest
- Provenance and claims
- Bill integration
- Timeline
- Editorial review
- Correction workflow

### P1

- Actors and power map
- Money Trail
- Predictions
- “Since your last visit”
- Civic Sprint
- Share cards

### P2

- Conversation Map
- Community comparison
- Teacher mode
- Advanced simulations
- Additional social connectors
- Personalized prerequisite explanations

---

## 11. Open Product Decisions

1. Should Theme Rooms have a dedicated route such as `/themes/{slug}` or a broader `/rooms/{slug}` taxonomy?
2. Which issue should serve as the pilot, balancing topical relevance with editorial risk?
3. Should the default home experience prioritize Theme Rooms, the Daily Civic Sprint, or both?
4. How much content requires human editorial approval at launch?
5. Which social source offers the best first combination of value, permissions, context, and moderation?
6. Should anonymous users be allowed to make locally stored predictions?
7. How should reading-level adaptation be controlled without oversimplifying?
8. What constitutes a meaningful update worthy of notification?
9. Which Civersify bill-page components can be reused immediately?
10. What editorial terminology guide should govern contested labels?

---

## 12. Recommended First Product Decision

Start by making the current bill experience a first-class Story Room and use it as one object inside a manually curated pilot Theme Room. This creates visible user value while forcing the shared data model, cross-linking, provenance, interactions, and update workflow to work together.

---



<!-- END 08_MVP_Rollout_and_Operating_Plan.md -->

