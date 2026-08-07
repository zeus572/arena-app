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
