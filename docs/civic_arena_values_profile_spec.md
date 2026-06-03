# Civic Arena Values Profile System — Product Spec for Claude

> **📘 Spec — implemented.** This is the design intent for the Civic Compass.
> It is built and live (rule-based scoring: confidence × intensity weighting →
> per-axis aggregation → cosine similarity to archetype vectors → softmax blend,
> plus contradiction detection and the Values Receipt). For the as-built
> details and where it lives in code, see
> **[docs/civic-app/ARCHITECTURE.md](./civic-app/ARCHITECTURE.md)** §7.

## 1. Overview

Civic Arena should help users build a nuanced civic values profile by making choices, resolving tradeoffs, and reflecting on why they believe what they believe.

The core idea is to avoid simplistic ideological labels such as “progressive,” “conservative,” “moderate,” or “libertarian.” Instead, Civic Arena should reveal how users think across multiple civic dimensions.

The product should ask users to make both straightforward and difficult choices, then use those choices to build a living values profile that can power debate recommendations, personalized civic explainers, reflection prompts, and user self-understanding.

The product should not ask, “Which political tribe are you in?”

It should ask:

> What do you value when values collide?

---

## 2. Product Goals

### Primary goals

1. Help users understand their own civic values in a nuanced way.
2. Avoid partisan labeling as the primary identity layer.
3. Use tradeoffs, dilemmas, and reflection to build a more accurate civic profile.
4. Feed the profile into Civic Arena debates, recommendations, explainers, and challenges.
5. Make civic self-discovery engaging, especially for kids, young adults, classrooms, and curious citizens.

### Secondary goals

1. Make politics feel less tribal and more reflective.
2. Encourage users to see tension within their own beliefs.
3. Help users understand the strongest arguments against their current views.
4. Create a profile that evolves over time as the user encounters new issues.
5. Provide shareable but privacy-conscious outputs such as a “Values Receipt” or “Civic Compass.”

---

## 3. Core Design Principle

Civic Arena should not categorize people first by ideology.

Instead, it should model civic values across dimensions such as:

- Fiscal priorities
- Personal liberty vs. public safety
- Institutional trust
- Local vs. federal power
- Equality of opportunity vs. equality of outcome
- Environmental urgency vs. economic disruption
- Free speech vs. harm reduction
- National security vs. civil liberties
- Growth vs. preservation
- Individual responsibility vs. systemic responsibility
- Technocratic expertise vs. democratic control
- Short-term relief vs. long-term sustainability

The user profile should feel like a civic fingerprint, not a political box.

---

## 4. Values Onboarding: “Choose Your Civic Starting Point”

### Feature summary

When a user joins Civic Arena, they should complete a short onboarding flow that introduces them to the values system.

The onboarding flow should use accessible, nonpartisan choices that gradually become more nuanced.

### Example introductory copy

> Let’s build your civic profile. No party labels. Just choices.
>
> You’ll answer a few questions about tradeoffs, priorities, and what matters most to you when public decisions get difficult.

### Straightforward choice examples

#### Question 1

**Which matters more to you in a healthy society?**

A. People have freedom to make their own choices  
B. People have strong support when life goes wrong

#### Question 2

**Which problem worries you more?**

A. Government doing too much  
B. Government failing to solve major problems

#### Question 3

**Which outcome feels more important?**

A. Keeping taxes low  
B. Funding strong public services

### Tough tradeoff examples

#### Budget surplus scenario

**Your city has a $100M surplus. Choose one:**

A. Expand mental health, housing, and addiction services  
B. Reduce local taxes and fees for residents and small businesses

#### Federal budget scenario

**The federal budget must cut one area to protect long-term solvency. What should be cut first?**

A. Defense growth  
B. Medicare growth  
C. Infrastructure spending  
D. Tax credits and subsidies  
E. None — raise taxes instead

#### Platform speech scenario

**A social media platform hosts legal but harmful misinformation. What should happen?**

A. Government should regulate platform behavior  
B. Platforms should self-regulate  
C. Users should decide what to trust  
D. Independent civic institutions should label and contextualize content

### Product requirement

Each answer should update one or more profile dimensions. The profile should not immediately assign an ideological label. It should instead infer civic tendencies and confidence levels.

---

## 5. Budget Priority Simulator

### Feature summary

The Budget Priority Simulator should be one of the strongest ways to build the values profile.

Users should distribute a limited number of points across competing public priorities.

### Example: “Build Your Federal Budget”

Users distribute 100 points across categories such as:

| Category | Description |
|---|---|
| Defense & national security | Military, cybersecurity, intelligence |
| Healthcare | Medicare, Medicaid, public health |
| Social safety net | Food aid, unemployment support, disability support |
| Education & workforce | Schools, college aid, apprenticeships |
| Infrastructure | Roads, bridges, transit, broadband |
| Climate & energy | Clean energy, disaster resilience, conservation |
| Debt reduction | Lowering future interest burden |
| Tax relief | Returning money to households and businesses |
| Immigration & border systems | Courts, enforcement, processing, integration |
| Science & innovation | Research, space, AI, biotech |

### Example output

If the user prioritizes healthcare, infrastructure, and debt reduction, the system might say:

> You prioritized healthcare, infrastructure, and debt reduction. That suggests you may value practical state capacity, intergenerational responsibility, and economic stability.

The system should avoid saying:

> You are center-left.

### Constraint mechanics

The simulator should introduce constraints to make the choices meaningful.

Examples:

- “You are over budget by 18 points. Choose what to cut.”
- “You added tax relief and debt reduction. Which spending area should shrink?”
- “You funded climate adaptation. Do you want to pay through taxes, debt, or cuts elsewhere?”

### Product requirement

The simulator should capture not only the final allocation but also the user’s sequence of tradeoffs, cuts, and funding mechanisms.

This creates richer data than a simple issue preference survey.

---

## 6. Values Pairing Questions

### Feature summary

Values Pairing Questions are fast A/B prompts that reveal civic instincts.

They should be easy to answer but meaningful in aggregate.

### Example pairings

#### Risk and protection

A. A society should reward people who take risks.  
B. A society should protect people from catastrophic failure.

#### Expertise and democracy

A. Experts should have more influence in complex policy areas.  
B. Ordinary citizens should have more direct control, even when issues are complex.

#### Rule consistency and flexibility

A. The law should be applied consistently, even when outcomes feel harsh.  
B. The law should allow flexibility for individual circumstances.

#### Growth and stability

A. Rapid economic growth is worth some disruption.  
B. Stability and community continuity matter more than speed.

#### Speech and harm

A. It is more dangerous when speech is restricted.  
B. It is more dangerous when harmful speech spreads unchecked.

### Product requirement

These questions should update profile dimensions gradually. They should also be mixed into the experience over time, not only during onboarding.

---

## 7. Pressure Test Scenarios

### Feature summary

Pressure Test Scenarios should test whether a user applies a value consistently across different contexts.

This allows Civic Arena to detect nuance rather than assume absolutism.

### Example

Earlier, a user says they strongly value free speech.

Later, they receive this scenario:

> A public university invites a controversial speaker. Many students say the speaker’s views make them feel unsafe. What should the university do?

Options:

A. Allow the event with security  
B. Cancel the event  
C. Move it off campus  
D. Allow it, but require a moderated counter-event  
E. Let students vote

### Example interpretation

> You generally prioritize free speech, but you support institutional safeguards when speech affects community safety. Your profile is not absolutist; it leans toward protected speech with civic guardrails.

### Product requirement

The system should distinguish between:

- Absolute value commitments
- Conditional commitments
- Context-dependent exceptions
- Low-confidence answers
- High-intensity but narrow views

---

## 8. Forced Tradeoff Civic Dilemmas

### Feature summary

Forced Tradeoff Dilemmas should make users choose between competing goods.

The goal is not to trap the user. The goal is to reveal which values dominate when everything cannot be optimized at once.

### Example dilemmas

#### Budget dilemma

**You can fully fund only two of these three.**

A. Universal school lunch  
B. Public transit expansion  
C. Police recruitment and training

#### Rights dilemma

**Which mistake is worse?**

A. Letting some dangerous people avoid surveillance  
B. Giving government too much surveillance power

#### Climate dilemma

**Which path would you choose?**

A. Faster clean energy transition with higher short-term energy costs  
B. Slower transition with lower short-term costs  
C. Heavy investment in nuclear and grid modernization  
D. Market incentives, but no mandates

#### Immigration dilemma

**Which goal should come first?**

A. Faster legal immigration pathways  
B. Stronger border enforcement  
C. Faster asylum processing  
D. Better integration support for immigrants already here

### Product requirement

The user should be able to say, “This was hard,” or “I dislike all of these,” but the system should still ask them to choose when appropriate. Forced choice is part of the product value.

---

## 9. Confidence and Intensity Sliders

### Feature summary

Every meaningful answer should allow the user to express both confidence and intensity.

### Confidence prompt

**How sure are you?**

- Not sure
- Somewhat sure
- Very sure

### Intensity prompt

**How important is this to you?**

- Low
- Medium
- High
- Non-negotiable

### Why this matters

Two users may choose the same answer but mean very different things.

Example:

- User A supports low taxes but marks the issue as low intensity.
- User B supports low taxes and marks the issue as non-negotiable.

These should produce meaningfully different profiles.

### Product requirement

The profile engine should store answer, confidence, and intensity as separate signals.

---

## 10. “Why Did You Choose That?” Reflection Prompts

### Feature summary

After certain answers, Civic Arena should ask optional reflection questions that reveal the user’s moral or practical reasoning.

### Example prompt

**What is the main reason you chose that?**

A. It seems more fair  
B. It seems more practical  
C. It protects freedom  
D. It protects vulnerable people  
E. It reduces long-term risk  
F. It matches my lived experience  
G. I’m not sure, it just feels right

### Why this matters

Two users may support the same policy for very different reasons.

For example, a user may support healthcare expansion because of:

- Compassion
- Economic efficiency
- Public health resilience
- Personal experience
- Religious or moral belief
- Concern about family stability

### Product requirement

The reasoning signal should be stored separately from the policy preference.

The user’s profile should be able to say:

> You often justify policy choices through long-term risk reduction and practical effectiveness, rather than purely ideological reasoning.

---

## 11. Civic Values Map

### Feature summary

The profile output should be a visual and textual map of civic tendencies.

It should not be a party label.

### Example profile dimensions

| Dimension | User tendency |
|---|---|
| Economic approach | Public investment with budget discipline |
| Social approach | Liberty-oriented with harm-reduction guardrails |
| Institutional trust | Skeptical of politicians, moderate trust in experts |
| Federalism | Prefers local control unless rights are at stake |
| Justice orientation | Strong due process, moderate restorative justice |
| Speech orientation | Broad speech protection with context tools |
| Risk orientation | Willing to accept short-term cost for long-term resilience |
| Change orientation | Reformist, not revolutionary |
| Civic temperament | Pragmatic, pluralistic, evidence-seeking |

### Example user-facing summary

> Your profile suggests you value practical problem-solving, personal freedom, and long-term stability. You are willing to fund public programs when they show measurable results, but you are skeptical of open-ended government expansion. You tend to prefer civic guardrails over bans, and you favor reform over disruption.

### Product requirement

Every profile statement should be traceable to user answers. The product should let the user understand why the system inferred a particular tendency.

---

## 12. Values Archetypes

### Feature summary

Civic Arena may generate custom archetypes, but they should be descriptive rather than partisan.

These archetypes should feel like civic styles, not political tribes.

### Example archetypes

| Archetype | Description |
|---|---|
| The Institution Reformer | Believes institutions matter, but need modernization and accountability |
| The Liberty Guardian | Prioritizes individual rights and limits on centralized power |
| The Public Builder | Believes strong public systems create opportunity and stability |
| The Local Steward | Values community, place, continuity, and local decision-making |
| The Risk Balancer | Weighs safety, freedom, innovation, and unintended consequences |
| The Fairness Advocate | Focuses on dignity, access, and reducing structural disadvantage |
| The Order & Trust Voter | Prioritizes safety, predictability, and social cohesion |
| The Future Investor | Prioritizes climate, technology, education, and long-term resilience |
| The Civic Pluralist | Values debate, compromise, and institutional legitimacy |

### Example blended output

> You are 34% Public Builder, 26% Institution Reformer, 21% Future Investor, and 19% Liberty Guardian.

### Product requirement

The product must make clear that archetypes are tendencies, not identities.

Avoid copy that sounds like the system is diagnosing or labeling the user.

---

## 13. “You Might Disagree With Yourself Here”

### Feature summary

The system should detect tensions or apparent contradictions in the user’s answers.

This should be framed gently and constructively.

### Example

The user says:

- Government should stay out of personal decisions.
- Government should ban certain types of online content.
- Local governments should control education.
- Federal government should protect individual rights when states go too far.

Civic Arena might say:

> You seem to prefer limited government in personal life, but stronger government action when harm spreads through large systems. That is not necessarily inconsistent, but it suggests your real value may be harm prevention rather than small government alone.

### Product requirement

Contradiction detection should avoid shaming the user. It should present tensions as opportunities for self-understanding.

---

## 14. “Show Me the Other Side” Profile Challenges

### Feature summary

Once a user has a profile, Civic Arena should generate thoughtful counterarguments.

### Examples

If the user prioritizes reducing debt:

> You prioritized reducing debt. Here is the strongest argument against making debt reduction a top priority right now.

If the user favors free speech protections:

> You favored free speech protections. Here is the strongest argument from someone who worries more about social harm.

### Response options

After reading the challenge, the user can respond:

- That changed my mind
- I partly agree
- I disagree, but understand it better
- This made my original view stronger

### Product requirement

Responses to challenges should update the user profile. The system should learn whether the user is persuadable, reflective, resistant, or more confident after challenge.

---

## 15. Issue-Based Profile Growth

### Feature summary

The values profile should evolve as users interact with real issues.

Examples:

- A Supreme Court case
- A congressional budget bill
- A state education proposal
- A local zoning dispute
- A climate regulation
- A speech/censorship controversy

### Example prompt after an explainer

> Based on your values profile, you may lean toward Option B. Does that feel right?

Options:

A. Yes, that matches me  
B. Mostly, but I have concerns  
C. No, I think differently here  
D. I need more context

### Product requirement

The profile should support issue-specific exceptions.

A user may generally prefer local control, but support federal action on civil rights. That nuance should be preserved.

---

## 16. Profile-Based Debate Matching

### Feature summary

Civic Arena should use the values profile to recommend debates that are personally relevant.

### Recommendation types

- A debate that challenges your strongest value
- A debate where both sides share your goals but disagree on methods
- A debate between two archetypes you partially match
- A debate where your answer is likely conflicted
- A debate where your local/federal instincts matter

### Example

> You may find this debate interesting: Should cities prioritize affordable housing density over neighborhood preservation?

### Product requirement

Recommendations should avoid creating an echo chamber. The system should intentionally include:

- Alignment debates
- Challenge debates
- Mixed-value debates
- Debates with unusual coalitions
- Debates where the user might update their view

---

## 17. Civic Compass, Not Political Compass

### Feature summary

Avoid the traditional two-axis political compass. It is too reductive for this product.

Instead, use a multi-axis Civic Compass.

### Possible axes

| Axis | Low end | High end |
|---|---|---|
| Government role | Minimal state | Active public builder |
| Liberty/safety | Liberty-first | Safety-first |
| Change speed | Gradualist | Transformational |
| Economic fairness | Market outcome | Redistributive correction |
| Authority | Decentralized | Centralized |
| Expertise | Populist judgment | Expert-guided |
| Speech | Open expression | Harm-aware moderation |
| Risk | Precautionary | Innovation-tolerant |
| Community | Individual-first | Community-first |
| Time horizon | Present relief | Future resilience |

### Product requirement

Each axis must be framed respectfully. Neither end should be implicitly labeled good or bad.

For example, avoid:

- Enlightened vs. ignorant
- Compassionate vs. selfish
- Free vs. authoritarian

Use balanced language instead.

---

## 18. Family and Classroom Mode

### Feature summary

Because Civic Arena may target kids and young adults, it should include a privacy-conscious comparison mode for classrooms, families, debate clubs, and youth civics groups.

### Compare Profiles Mode

Users can compare profiles without seeing party labels.

Example:

> You and Maya both care about fairness, but you differ on whether fairness is best achieved through equal rules or targeted support.

Example classroom insight:

> Your class strongly agrees on climate urgency but disagrees on whether mandates or incentives are better.

### Product requirement

For minors, the product should:

- Avoid sensitive political labeling
- Use privacy-first defaults
- Avoid public profile sharing by default
- Avoid targeting or persuasion based on political identity
- Prefer educational framing over ideological categorization

---

## 19. “Build a Candidate From Your Values”

### Feature summary

This is an entertaining and educational feature where Civic Arena generates a fictional candidate based on the user’s values profile.

### Example output

> Your candidate is a pragmatic infrastructure reformer. They support clean energy, budget transparency, broad speech protections, and local control with federal civil-rights guardrails.

The user can then see:

- Where this candidate would clash with Democrats
- Where this candidate would clash with Republicans
- Where this candidate would attract unusual coalitions
- What criticism this candidate would face
- Which voters or archetypes might support them

### Product requirement

The candidate must be fictional and clearly labeled as a simulation.

This feature should be framed as civic imagination, not as an endorsement tool.

---

## 20. Values Receipt

### Feature summary

After each session, users should receive a short “Values Receipt” summarizing what Civic Arena learned.

### Example

> Today we learned:
>
> - You prioritize long-term debt reduction more than immediate tax relief.
> - You support public investment when outcomes are measurable.
> - You are cautious about government speech regulation.
> - You prefer local control, except when fundamental rights are at stake.
> - You are currently unsure about immigration enforcement vs. legal pathway expansion.

### Product requirement

The Values Receipt should be:

- Short
- Transparent
- User-editable or correctable
- Shareable only if the user explicitly chooses to share
- Traceable to recent answers

---

## 21. Example End-to-End User Flow

1. User signs up.
2. Civic Arena says:
   > Let’s build your civic profile. No party labels. Just choices.
3. User answers 10 quick A/B questions.
4. User completes one budget simulator.
5. User answers 3 tough dilemmas.
6. Civic Arena generates a profile:
   > You are a Future Investor / Institution Reformer blend.
7. User sees their Civic Compass.
8. Civic Arena recommends:
   > You may find this debate interesting: Should cities prioritize affordable housing density over neighborhood preservation?
9. After the debate, user is asked:
   > Did either side change how you think?
10. The user profile becomes more nuanced over time.

---

## 22. Suggested MVP Scope

### MVP 1: Values Onboarding

Build a 20–30 question onboarding flow with:

- Easy A/B choices
- Forced tradeoff dilemmas
- Confidence and intensity sliders
- Optional reasoning prompts

### MVP 2: Civic Compass Profile

Generate a multi-axis values profile with plain-English explanations.

The first version can be rule-based rather than fully AI-generated.

### MVP 3: Budget Simulator

Create a simple 100-point allocation exercise across 8–10 public priorities.

Add at least one constraint step where the user must cut, fund, or rebalance.

### MVP 4: Debate Recommendations

Use the values profile to recommend debates that:

- Match the user’s values
- Challenge the user’s values
- Create productive tension

### MVP 5: Profile Reflection

Let users respond to their generated profile:

- This sounds right
- This sounds wrong
- This is partly right
- I changed my mind

These corrections should feed back into the profile.

---

## 23. Data Model Suggestions

### UserProfile

Stores the overall values profile for a user.

Suggested fields:

- userId
- profileVersion
- createdAt
- updatedAt
- archetypeBlend
- axisScores
- confidenceScores
- issueExceptions
- reflectionSummary

### ProfileAxisScore

Represents a user’s position on a civic dimension.

Suggested fields:

- axisId
- axisName
- score
- confidence
- intensity
- explanation
- supportingAnswerIds

### CivicQuestion

Represents a question shown to users.

Suggested fields:

- questionId
- type
- prompt
- choices
- mappedAxes
- difficulty
- topic
- ageAppropriateness
- createdAt

Question types:

- simple_pairing
- forced_tradeoff
- budget_allocation
- pressure_test
- reflection
- issue_specific

### CivicAnswer

Represents a user answer.

Suggested fields:

- answerId
- userId
- questionId
- selectedChoice
- confidence
- intensity
- reasoningChoice
- freeTextReasoning
- createdAt

### ValuesReceipt

Represents a session summary.

Suggested fields:

- receiptId
- userId
- sessionId
- learnedInsights
- changedAxes
- uncertainAreas
- recommendedDebates
- createdAt

---

## 24. AI/LLM Usage Guidance

Claude or another LLM can be used for:

- Generating balanced question wording
- Explaining profile results in natural language
- Creating “show me the other side” arguments
- Producing Values Receipts
- Creating fictional candidate simulations
- Mapping debate content to values dimensions

However, the product should avoid using an LLM as an opaque ideology classifier.

Recommended approach:

1. Use structured questions and rule-based scoring for core profile data.
2. Use the LLM to explain, summarize, and personalize the result.
3. Store all profile inferences with traceability back to user choices.
4. Let users correct the system when it mischaracterizes them.

---

## 25. UX Tone Guidelines

The tone should be:

- Curious
- Respectful
- Nonjudgmental
- Balanced
- Youth-friendly but not childish
- Clear about tradeoffs
- Comfortable with uncertainty

Avoid language like:

- “You are wrong”
- “You are inconsistent”
- “Your ideology is...”
- “People like you believe...”

Prefer language like:

- “This suggests...”
- “You may be weighing...”
- “You seem to prioritize...”
- “This could mean...”
- “Here is a tension worth exploring...”

---

## 26. Product Differentiation

This system differentiates Civic Arena from traditional political quizzes, news sites, and debate platforms.

Most products ask:

> What do you believe?

Civic Arena asks:

> What do you value when values collide?

That distinction is the product.

A user may be:

- Liberty-first on speech
- Public-investment-oriented on infrastructure
- Skeptical of institutions on surveillance
- Expert-trusting on climate
- Localist on schools
- Federalist on civil rights
- Market-friendly on innovation
- Safety-net-friendly on healthcare

That complexity should be preserved and celebrated.

Civic Arena should help users discover that their civic identity is richer than a party label.

---

## 27. Implementation Prompt for Claude

Use the following prompt when asking Claude to implement or prototype this feature:

```text
You are helping implement Civic Arena, a civic debate and sensemaking platform. Build a Values Profile system that helps users discover their civic values through choices, dilemmas, budget tradeoffs, and reflection.

Do not use simplistic political labels like progressive, conservative, moderate, or libertarian as the primary output. Instead, create a multi-dimensional civic profile across axes such as government role, liberty vs. safety, institutional trust, federal vs. local authority, fairness, expertise, speech, risk tolerance, and time horizon.

Design the system around:
- A values onboarding flow
- A/B civic choice prompts
- Forced tradeoff dilemmas
- A budget allocation simulator
- Confidence and intensity sliders
- Optional “why did you choose that?” reflection prompts
- A Civic Compass profile output
- Values archetypes that are descriptive, not partisan
- Contradiction/tension detection
- “Show me the other side” challenges
- Debate recommendations based on the profile
- A Values Receipt after each session

Produce implementation-ready frontend and backend designs. Include data models, API routes, scoring logic, UX components, and example seed questions. Prioritize an MVP that can be built quickly but expanded over time.

The tone should be curious, balanced, nonjudgmental, and youth-friendly. The system should help users understand themselves, not sort them into political tribes.
```
