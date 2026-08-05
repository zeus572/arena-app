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
