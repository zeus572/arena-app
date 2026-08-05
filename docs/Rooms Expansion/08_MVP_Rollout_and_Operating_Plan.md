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
