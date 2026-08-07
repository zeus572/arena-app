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
