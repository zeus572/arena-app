# Frontend Prototype Spec — Youth-First Civic Sensemaking Platform

## Objective
Build a frontend-only prototype for a youth-first civic learning and sensemaking platform.

The prototype should help evaluate:
- visual direction,
- UX flow,
- page structure,
- content density,
- component model,
- mocked API shape,
- mobile responsiveness,
- and how the product feels with realistic sample content.

## Product Wedge
**Current events that teach young people how power works.**

## Product Mission
Help teens and young adults understand current events, civic institutions, public decisions, and their own developing beliefs without dumbing public life down.

## Tech Stack
Recommended:
- Vite
- React
- TypeScript
- Tailwind CSS
- shadcn/ui
- lucide-react
- React Router
- mocked API layer using local TypeScript data modules

Optional:
- Framer Motion for light transitions
- Recharts only if adding progress or analytics mockups

## Scope
This is a frontend-only prototype.

Do not implement:
- real authentication
- real backend
- database
- production CMS
- real API integrations
- payments
- user accounts
- moderation
- real social publishing

Use mocked data only.

## Required Pages

### 1. Homepage
Purpose: Explain the product quickly and route users into briefings, concepts, quizzes, and Think Deeper flows.

Required sections:
- Hero
- Today’s Civic Briefings
- Concept of the Day
- Take a 3-question quiz
- Think Deeper prompt
- Follow an issue preview
- Teacher/parent trust section
- Social carousel preview

Suggested hero copy:
**Current events that teach you how power works.**

Subcopy:
Understand what happened, who acted, why it matters, and what values are in conflict.

Primary CTA:
Explore today’s briefings

Secondary CTA:
Take a 3-question civic quiz

---

### 2. Latest Briefings Page
Purpose: Browse civic briefings.

Features:
- filter by institution: Congress, Supreme Court, Executive, States
- filter by topic: Rights, Schools, Technology, Elections, Privacy
- card layout
- search mockup
- age/depth tag optional

---

### 3. Civic Briefing Detail Page
Purpose: Deep current-event explainer.

Required sections:
- headline
- institution tag
- status
- 30-second summary
- 3-minute version
- who acted?
- what changed?
- why this matters
- words you need to know
- what people disagree about
- strongest argument for
- strongest argument against
- values in conflict
- think deeper
- where to go next
- source transparency block
- social share preview

Suggested interaction:
Use tabs or segmented controls for:
- 30 seconds
- 3 minutes
- 10 minutes

---

### 4. Concept Library Page
Purpose: Browse reusable civic concepts.

Features:
- concept cards
- categories
- search
- “popular this week”
- “learn the system” pathway

Categories:
- Congress
- Supreme Court
- Executive Branch
- Rights
- Elections
- Media Literacy
- Values & Tradeoffs

---

### 5. Concept Detail Page
Required sections:
- plain-language definition
- why it matters
- where you see it
- current example
- common misunderstanding
- related concepts
- try it quiz

---

### 6. Think Deeper Page
Purpose: Help users reflect on their own reasoning.

Required sections:
- issue intro
- first reaction selector
- values involved
- strongest argument A
- strongest argument B
- what each side may miss
- what would change your mind?
- can both things be true?
- build your view

Interaction ideas:
- selectable values chips
- short text input mockup
- “save reflection” disabled/mock button
- shareable summary card

---

### 7. Quiz Page
Purpose: Active learning.

Required:
- question card
- multiple choice
- explanation after answer
- progress indicator
- related concept link
- final result screen

Quiz types:
- Which branch acted?
- Is it law yet?
- Fact / interpretation / prediction / value
- Which concept explains this?
- Values in conflict

---

### 8. Timeline Page
Purpose: Explain process over time.

Required:
- timeline items
- current status marker
- “what happens next”
- related concept cards

Example timeline:
A bill moves from introduction → committee → House vote → Senate vote → president.

---

### 9. Teacher / Parent Landing Page
Purpose: Build adult trust and support school/home use.

Required sections:
- why this exists
- how content is sourced
- how perspectives are handled
- classroom discussion prompts
- parent conversation starters
- age-level guidance
- printable guide preview
- weekly briefing signup mock

---

## Core Components

Create reusable components:

### Content Components
- `CivicBriefingCard`
- `CivicBriefingHero`
- `SummaryTabs`
- `InstitutionTag`
- `ConceptCard`
- `ConceptDefinitionBlock`
- `ValuesInConflictPanel`
- `ThinkDeeperPrompt`
- `ArgumentComparison`
- `SourceTransparencyBlock`
- `TimelinePreview`
- `Timeline`
- `QuizCard`
- `SocialCarouselPreview`
- `TeacherGuidePreview`

### Layout Components
- `AppShell`
- `Header`
- `Footer`
- `MobileNav`
- `SectionHeader`
- `PageContainer`
- `FilterBar`

### Utility Components
- `Tag`
- `ValueChip`
- `ProgressPill`
- `Callout`
- `EmptyState`
- `MockSignupForm`

---

## Mock Data Model

### CivicBriefing

```ts
export type Institution = "Congress" | "Supreme Court" | "Executive" | "State Government";

export type CivicBriefing = {
  id: string;
  slug: string;
  headline: string;
  institution: Institution;
  branch: "Legislative" | "Judicial" | "Executive" | "State";
  status: string;
  audienceLevel: "Middle School" | "High School" | "College" | "Young Adult";
  keyConcept: string;
  tags: string[];
  summary30: string;
  summary3Min: string;
  whoActed: string;
  whatChanged: string;
  whyItMatters: string;
  wordsToKnow: {
    term: string;
    definition: string;
  }[];
  disagreement: string;
  strongestArgumentFor: string;
  strongestArgumentAgainst: string;
  valuesInConflict: string[];
  thinkDeeperQuestion: string;
  relatedConcepts: string[];
};
```

### Concept

```ts
export type Concept = {
  id: string;
  slug: string;
  title: string;
  category: string;
  plainDefinition: string;
  whyItMatters: string;
  whereYouSeeIt: string[];
  currentExample: string;
  commonMisunderstanding: string;
  relatedConcepts: string[];
  tryItQuestion: string;
};
```

### ThinkDeeper

```ts
export type ThinkDeeper = {
  id: string;
  slug: string;
  issue: string;
  firstReactionPrompt: string;
  values: string[];
  strongestArgumentA: string;
  strongestArgumentB: string;
  whatSideAMayMiss: string;
  whatSideBMayMiss: string;
  whatWouldChangeYourMind: string[];
  canBothBeTrue: string;
  buildYourViewPrompt: string;
};
```

### QuizQuestion

```ts
export type QuizQuestion = {
  id: string;
  question: string;
  options: string[];
  correctAnswerIndex: number;
  explanation: string;
  relatedConceptSlug?: string;
};
```

---

## Mocked API Layer

Implement a simple local API layer.

Example:

```ts
export async function getBriefings(): Promise<CivicBriefing[]> {
  return briefings;
}

export async function getBriefingBySlug(slug: string): Promise<CivicBriefing | undefined> {
  return briefings.find((briefing) => briefing.slug === slug);
}

export async function getConcepts(): Promise<Concept[]> {
  return concepts;
}

export async function getConceptBySlug(slug: string): Promise<Concept | undefined> {
  return concepts.find((concept) => concept.slug === slug);
}
```

Use async functions even though the data is local so the app is easy to connect to a real backend later.

---

## Suggested Routes

```txt
/
/latest
/briefings/:slug
/learn
/concepts/:slug
/think-deeper
/think-deeper/:slug
/quizzes
/timelines/:slug
/teachers
/parents
```

---

## Visual Direction Options

Build the prototype in a way that can support theming.

Recommended theme variants:
1. Bright Social-Native
2. Civic Intelligence Dashboard
3. Classroom-Trusted
4. How Power Works / Diagrammatic
5. Editorial Youth Magazine

At minimum, implement one default visual direction and keep the CSS/theme structure easy to adjust.

---

## Design Requirements

### Mobile-first
Assume many users arrive from social links on mobile.

Mobile priorities:
- immediate summary
- readable cards
- sticky “go deeper” action
- quiz interaction
- carousel preview
- clear institution tags
- fast path to Think Deeper

### Accessibility
- readable font sizes
- strong contrast
- keyboard navigability
- semantic headings
- accessible buttons and forms
- avoid relying on color alone

### Content Density
Avoid walls of text.
Use:
- cards
- tabs
- accordions
- chips
- progressive disclosure
- short sections
- visual separators

### Trust
Every briefing detail should include a source transparency block, even if sources are mocked.

Suggested copy:
“This prototype uses mocked sample content. In production, briefings should link to primary sources such as official government pages, court documents, agency notices, or legislative records.”

---

## Sample Data to Include

Use sample content from `SAMPLE_CONTENT.md`, especially:
- Congress Advances a Student Data Privacy Bill
- Supreme Court Hears a Case About Online Speech
- Federal Agency Proposes New Rules for AI in Hiring
- State Passes a Law Limiting Phone Use in Schools

Concepts:
- Committee Hearing
- Oral Argument
- Agency Rulemaking

Quizzes:
- Which branch acted?
- Is it law yet?
- Fact / interpretation / prediction / value
- Values in conflict

---

## Prototype Success Criteria

The prototype is successful if it helps answer:
- Does this feel like a product, not just a blog?
- Does it feel youth-friendly but not childish?
- Is the civic value obvious in the first 10 seconds?
- Can users quickly understand “what happened” and “who has power here”?
- Do the Think Deeper prompts feel natural rather than preachy?
- Would this be credible to teachers and parents?
- Would a teen or young adult actually click through from social?
- Does the design support both quick summaries and deeper learning?

---

## Implementation Instructions for Claude/Codex

Build a clean, maintainable React prototype.

Prioritize:
1. page structure
2. reusable components
3. realistic mocked data
4. responsive UI
5. visual polish
6. easy iteration

Do not overbuild:
- auth
- backend
- complex state
- real CMS
- production deployment
- real data ingestion

Use comments sparingly and keep components readable.

At the end, provide:
- setup instructions
- project structure overview
- how to add new mock briefings
- how to switch visual direction/theme if implemented
