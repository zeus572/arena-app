# Civic Arena — Daily Governance Game: Design Overview

**Status:** Active design exploration (sounding-board phase, pre-spec)
**Format:** Working design docs, to be hardened into PRD-style specs as decisions lock
**Last updated:** This captures an in-progress design conversation. Sections marked OPEN are unresolved.

---

## One-line concept

A Duolingo-style daily civic game where the win condition is **building bipartisan coalitions that can govern** — not winning debates. News stories spawn neutral, governance-level **provisions** that users take positions on, amend, and build cross-spectrum coalitions around over roughly week-long lifecycles, accumulating toward campaign-long milestones synced to the real election calendar.

## The core reframing (why this is different)

Most political gamification rewards **persuasion or winning**, which is zero-sum and structurally reproduces tribalism — you can't gamify out of tribalism with a mechanic that requires a loser. This design instead uses a **legislature model**: the win condition is non-zero-sum (a coalition governs together or nothing passes). Partisanship becomes a *cost*, not a strategy — the more rigid your candidate, the smaller the coalition you can assemble, the less you can pass. The fun-maximizing play and the civically-desirable play are made into the same play.

## The governance-vs-culture thesis

Politics has become personal and cultural rather than about governance. Cultural-values conflicts are often genuinely irreconcilable (they bottom out in identity). But **governance questions frequently have available agreement that the cultural framing obscures.** The game's central skill is teaching players to *descend from the values fight to the governable question underneath it* — e.g., immigration as cultural identity (irreconcilable) vs. "asylum cases shouldn't take six years" (bridgeable). This descent is learnable and scorable, and almost nothing in the current media environment teaches it.

## Locked answers (from this conversation)

| Question | Answer |
|---|---|
| Primary audience | **Mixed-age public** (not classroom-first) — no teacher safety/motivation layer; gamification must carry return motivation alone; safety must be structural |
| What the leaderboard most rewards | **Finding cross-spectrum common ground** |
| Real-time model | **Async** (post & come back, Duolingo-style) |
| Round structure | **No hard gating.** Every day is a new turn; daily engagement earns points that accrue toward macro campaign milestones |
| Provision lifecycle length | **~1 week** (long enough to bridge, short enough to stay news-synced; creates overlapping cohorts) |
| Difficulty model | **Laddered by gap width.** New leagues/groups start on small-distance (bridgeable) provisions to hook; widen as the group demonstrates bridging skill |

## Document index

- `00_OVERVIEW.md` — this file
- `01_PHILOSOPHY_AND_WIN_CONDITION.md` — the legislature model, governance-vs-culture, design traps to avoid
- `02_ACTS_AND_INTERACTIONS.md` — the micro→macro ladder of things users do, and what each earns
- `03_PROVISIONS_AND_LIFECYCLE.md` — provisions as the core content unit; the week-long lifecycle and what "passing" means
- `04_SCORING_PROGRESSION_AND_LEAGUES.md` — currencies, milestones, difficulty laddering, league composition, distance signal
- `05_OPEN_QUESTIONS.md` — unresolved decisions and the next thing to nail down
- `06_DISTANCE_SIGNAL.md` — the acceptance-set model: distance measured in provision-clause space, not Values geometry
- `07_IMPLEMENTATION_PLAN.md` — phased build (0→1→2→3) with Claude Code build/test/gate triples; free-form-with-extraction architecture; agent self-play as vertical slice + seed

## Leverages existing Civic Arena systems

- **Values Profile axes** — the per-issue spectrum positions that the bridge/coalition engine reads
- **Common Ground mode (agent debates)** — the rubric for genuine vs. platitude agreement, now applied to humans
- **Civic Briefings** — the news triggers that spawn provisions
- **Ranking Engine** — multi-factor scoring substrate
- **Bot Heartbeat** — scheduling/cadence
- **Precomputed-choices cost philosophy** — structured scoring for continuous signals, LLM only for irreducible discrete judgments
