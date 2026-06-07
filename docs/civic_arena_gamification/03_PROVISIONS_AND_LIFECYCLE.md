# 03 — Provisions & Lifecycle

The **provision** is the core content unit and the connective tissue of the whole game: it's where daily micro-acts (positions, amendments, steelmans) flow upward into campaign-long milestones. The full architecture: **day → provision (≈1 week) → campaign.**

## Why ~1 week

- **Long enough to bridge** — cross-spectrum agreement is slow; people must encounter the provision, react, see amendments, reconsider.
- **Short enough** to stay synced to the news cycle and keep the daily turn fresh.
- Creates **overlapping cohorts** — at any moment there are provisions on day 1, day 3, day 6, so every daily visit has things being born, heating up, and about to resolve. This staggering is what makes the feed feel alive (the "garden" you tend daily).
- A provision does **not** have to complete in a day; ~1 week is the target order of magnitude.

## Lifecycle stages

### 1. Birth
Two entry paths:
- **System-extracted** from a briefing (pipeline frames the neutral, real-tradeoff proposition). Favor this early for quality control.
- **User-authored** from a story. A user provision should clear a small threshold of initial positions before getting full lifecycle status — prevents flooding with junk/duplicates.

The extraction is the craft: neutral surface, real tradeoff underneath, specific enough to position on.

### 2. Open / position-gathering
The wide top of the funnel — cheap, high-volume acts (positions w/ intensity, culture-vs-governance tags, steelmans). The system quietly reads *who* is positioning and where they sit on the relevant Values axis, building the map of where agreement might be possible. Most daily-floor activity lives here.

### 3. Contested / amendment
Once there are enough positions to show real disagreement, amendments begin — the clause that would let someone sign. This is where a provision either **converges** (amendments pull opposing Values clusters toward a signable middle) or **fragments** (amendments restate the existing camps). The system detects which is happening: are amendments *narrowing the distance* between clusters or entrenching them?

### 4. Forking (a feature, not a failure)
Sometimes a provision *shouldn't* converge to one thing — it should split into coherent variants different coalitions form around. Data-center fee forks into "marginal cost, all facilities" vs. "marginal cost, large facilities only." Both legitimate. **A forked provision where each fork assembles a cross-spectrum coalition is richer than one mushy consensus** — it shows there were two governable answers, not one.

### 5. Resolution — what "passing" means

**Passing is by quality of coalition, NOT by vote threshold.** A pure headcount threshold just rewards whichever side has more grinders — reproducing majoritarian tribalism, the exact thing we're escaping.

A provision (or one of its forks) passes when it reaches a coalition that is:
- **Broad across the spectrum** — signatories span a defined width of the Values axes (*breadth, not headcount*),
- **Costly to its signatories** — people gave up maximalist positions to sign (the anti-mush guard; a coalition that gave nothing up just found its caucus),
- **Specific enough to have teeth** — concrete enough to constrain a real institution's behavior,
- **Confirmed by the LLM judge** that signatories actually *moved* from where they started rather than signing something that already matched them.

→ Pass criteria = **breadth · cost · specificity · movement.**

### 6. Death
Most provisions won't pass — and that must be fine, framed honestly, not as failure:
- "This one stayed in the culture layer" / "the spectrum was genuinely too far apart this week" / "it stalled."
- Some briefings *should* fail to bridge; the game recognizing that honestly is part of its credibility.
- A dead provision still paid out the daily reasoning points its participants earned.
- A dead provision may leave a small artifact — "this was a no-bridge issue this week" — itself a true and interesting civic data point.

## The convergence / distance signal (heart of the daily turn)

Each provision has a visible **distance-to-coalition**: how far apart the engaged positions still are, how much breadth it has gathered. The daily act becomes *tending the provisions closest to resolution* — the one at day 5 with a cross-spectrum coalition *almost* assembled, needing one more amendment or one bridge from the spectrum-corner that hasn't signed.

That "almost there, and the clock is ticking" state is the organic return hook (no manufactured streak needed) — **the provision's own deadline is the pull to come back tomorrow.**

This single signal ends up with **three stacked jobs** (see doc 04): daily return hook, anti-majoritarian pass criterion, and difficulty knob. It must therefore be *measurable, sortable, and surfaceable.* Defining it precisely is the next thing to nail down (see doc 05).

## Two unresolved lifecycle tensions

### Who holds the pen / when does text lock?
A converging provision needs someone to hold the final clause or it's endless amendment with no closure. Options:
- Original author stewards it,
- Coalition co-authors a final step,
- **System drafts a synthesis from live amendments; signatories approve (leaning toward this).**

The synthesis-then-approve route is async-native, prevents single-author hijack, and preserves broadcast-only safety (synthesis is public, approval is a public act). **Cost caveat:** synthesis costs LLM calls — against the precomputed-choices philosophy, reserve full synthesis for provisions that actually reach *near-coalition*, not every provision.

### Cost discipline on convergence detection
Running an LLM on every provision every day to judge whether amendments narrow the Values distance is expensive and cuts against precomputed-choices. The split:
- **Cheap structured scoring (no LLM):** distance = how spread signatories are across the Values axes. Pure scoring on profile axes. Use for the continuous, every-provision, every-day signals.
- **LLM only for irreducible discrete judgments:** is this amendment substantive or a restatement? Does the final text have teeth? Did signatories actually move? Spend LLM budget only at the few moments per lifecycle where judgment is irreducible.

→ **This split — structured scoring for continuous signals, LLM for discrete judgments — is the cost spine of the feature.**
