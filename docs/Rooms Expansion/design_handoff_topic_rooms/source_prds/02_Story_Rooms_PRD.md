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
