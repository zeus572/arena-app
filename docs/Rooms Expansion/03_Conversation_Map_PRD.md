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
