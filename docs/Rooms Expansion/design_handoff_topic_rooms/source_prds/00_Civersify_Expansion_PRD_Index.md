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
