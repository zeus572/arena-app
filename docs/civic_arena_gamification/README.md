# Civic Arena — Daily Governance Game: Design & Build Bundle

This bundle captures the full design conversation for the Duolingo-style civic coalition game, plus the implementation plan and Claude Code kickoff prompts.

## Read in this order

1. **00_OVERVIEW.md** — index, one-line concept, the core reframing, locked decisions
2. **01_PHILOSOPHY_AND_WIN_CONDITION.md** — legislature (not debate) model; governance-vs-culture thesis; design traps; safety posture
3. **02_ACTS_AND_INTERACTIONS.md** — story→provision shift; the micro→macro acts ladder
4. **03_PROVISIONS_AND_LIFECYCLE.md** — provisions as the core unit; ~1-week lifecycle; what "passing" means
5. **04_SCORING_PROGRESSION_AND_LEAGUES.md** — currencies; three macro milestones; difficulty laddering; leagues; streak fork
6. **05_OPEN_QUESTIONS.md** — unresolved decisions and leanings
7. **06_DISTANCE_SIGNAL.md** — the acceptance-set model (distance lives in provision-clause space, not Values geometry)
8. **07_IMPLEMENTATION_PLAN.md** — the build: principles + phased 0→1→2→3 with Build/Test/Gate triples

## Build artifacts

- **PHASE_0_KICKOFF_PROMPT.md** — paste into Claude Code to run Layer 0 (Phases 0.1→0.2→0.3) unattended, with a mandatory human stop at extraction fidelity (0.3).
- **REUSABLE_PHASE_KICKOFF_TEMPLATE.md** — copy/fill to launch later batches; includes suggested batch boundaries and where to place human-review stops.
- **Distance_Signal.pptx** *(separate from this folder if you exported it)* — the 14-slide concept deck for a 15-minute review.

## The core idea in three sentences

Most political gamification rewards winning, which is zero-sum and reproduces tribalism. This game makes the win condition **building a bipartisan coalition that can govern**, so partisanship becomes a cost and bridging becomes the optimal play. The central computation — the **distance signal** — measures not how far apart people are, but how close a divided group is to a specific, costly, spectrum-spanning position they'd actually sign.

## Key architectural commitments (so a fresh reader / Claude Code doesn't re-litigate them)

- **Free-form on the surface, structured underneath.** Players write natural language; an **extraction** step maps text → positions on emergent **sub-questions** once at write time, so the continuous distance geometry stays cheap (no LLM in the hot loop).
- **One state machine, two player types.** Agents and humans produce the same acts; build the loop once, swap input source.
- **Agent self-play is the seed AND the validation harness** — the vertical slice (Phase 2.4) is an autonomous agent-played coalition.
- **Extraction fidelity is the top risk** — a misread version produces confidently-wrong distances invisibly. Over-invest in its test corpus.
- **Three LLM tiers:** extraction (write-time), continuous geometry (no LLM), discrete integrity gates (rare, near-coalition only).

## How to run the build (your stated workflow: unattended batches, tweak between)

1. Start a Claude Code session in the Civic Arena repo; attach `07_IMPLEMENTATION_PLAN.md` (and other docs for context).
2. Paste `PHASE_0_KICKOFF_PROMPT.md`. Let it run 0.1→0.2→0.3 unattended; it self-halts at 0.3 (or earlier on a gate failure).
3. Review `BUILD_LOG.md` (the agent's audit trail) + the extraction-fidelity results by hand. Tweak.
4. For the next batch, copy `REUSABLE_PHASE_KICKOFF_TEMPLATE.md`, fill in the batch's phases + stop condition (see its "suggested batch boundaries"), paste, repeat.

**Why the stops exist:** the gates are the safety mechanism of the whole plan. Unattended, a gate test must hard-fail and halt the chain rather than be papered over. Judgment-based gates (neutral? genuine? has-teeth?) are placed at batch boundaries because the agent self-grades those least reliably and they sit upstream of expensive work.
