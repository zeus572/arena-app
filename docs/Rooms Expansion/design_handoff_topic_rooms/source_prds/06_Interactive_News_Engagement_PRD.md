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
