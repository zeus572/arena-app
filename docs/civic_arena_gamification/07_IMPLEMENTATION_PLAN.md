# 07 — Implementation Plan

**How to use this doc:** It is written to be executed progressively through Claude Code, one phase at a time, with tests between phases. Each phase has a **Build / Test / Gate** triple. *Do not start a phase until the prior phase's Gate passes.* Phases name their dependencies explicitly so ordering is enforced.

**Goal of the first push:** a full vertical slice end-to-end, **played by agents** (agent self-play is both the cold-start seed and the validation harness). Humans slot into the same machine later.

---

## Part A — Architectural principles (decide once, hold throughout)

### A1. Build order is the reverse of the excitement order
The dependency chain is: **data model → calculator → game loop → curriculum.** You cannot compute a distance signal without acceptance data; you cannot generate acceptance data without provisions that accept structured engagement. So:
- **Layer 0:** Provisions exist and accrue structured engagement.
- **Layer 1:** Geometry computed but invisible (validated in isolation).
- **Layer 2:** The coalition loop — the playable gameplay.
- **Layer 3:** Ladder, leagues, campaign milestones (most empirical; must observe real loops first).

### A2. Free-form on the surface, structured underneath — structure is *extracted, not authored*
Players write and read natural-language provisions, amendments, and versions (realism). Every free-form artifact passes through a one-time **extraction** step at write time that maps text → a structured representation the geometry computes over cheaply. **Expensive semantic work happens once at authoring; continuous distance computation runs over the extracted structure and stays cheap.**

### A3. The structured representation: resolved sub-questions
A provision has latent **dimensions of disagreement** ("sub-questions") — e.g. for a data-center grid-cost fee: *which facilities are covered? marginal or average cost? are existing facilities grandfathered?* Any free-form version, however worded, **resolves some sub-questions to some positions.** Extraction maps `text version → vector of resolved sub-question positions`.
- Overlap of acceptance sets = intersection of acceptable regions in **sub-question space**.
- Distance = movement in sub-question space.
- Breadth = still measured in **Values-axis space** (unchanged).

### A4. Sub-questions are emergent, not predefined
At provision birth, an LLM identifies the initial sub-questions. **Amendments may introduce new sub-questions nobody anticipated** (realistic — bargaining surfaces hidden cruxes); the structured space expands to absorb them. Free-form authoring can surprise the system; the geometry still stays tractable because text is reduced to positions on the currently-known sub-questions before distance is computed.

### A5. Three LLM tiers (the cost model)
| Tier | Role | Frequency | Cost posture |
|---|---|---|---|
| **Extraction** | free-form text → sub-question positions | once per authored artifact (write time) | frequent but bounded, cacheable |
| **Continuous geometry** | overlap, distance, breadth, fork | every recompute | **no LLM** — pure computation |
| **Discrete integrity gates** | amendment substantive? text has teeth? did signer move? synthesis of final plank | rare, near-coalition only | bounded (precomputed-choices discipline) |

### A6. One state machine, two kinds of player
The provision lifecycle is a state machine driven by **acts + geometry**, not by *who* acts. Agents and humans produce the same acts (take position, propose amendment, accept version). Build the machine once; swap input source later. Never rebuild the loop for humans.

### A7. The new riskiest assumption is extraction fidelity
Extraction is load-bearing and fallible. A misread version produces confidently-wrong distances, invisibly, upstream of all the "trustworthy cheap" computation. **Extraction-fidelity tests are the center of the test strategy** (displacing acceptance-set inference as the top risk).

### A8. Safety invariants (hold across all layers)
- Broadcast-only social surface; **never open a private channel between two users** as a reward.
- Fictional-candidate content-safety posture is independent of these mechanics and remains in force.
- For mixed-age public: keep age-banding in mind from Layer 3; ensure no mechanic creates adult↔minor private interaction.

---

## Part B — The coalition loop as a state machine (the heart of Layer 2)

```
                 ┌─────────────────────────────────────────────┐
                 │                                             │
  BIRTH ──▶ OPEN ──▶ CONTESTED ──▶ NEAR-COALITION ──▶ { PASSED }
            │           │                │            { FORKED } ──▶ (two new loops)
            │           │                │            { DIED  }
            └───────────┴────────────────┘  (deadline can send any active state → DIED)
```

| State | Primary act | Geometry running | LLM? | Exit transition |
|---|---|---|---|---|
| **OPEN** | position + intensity + reasoning tag | initial spread on relevant axes | extraction only (on each artifact) | enough positions to define meaningful spread → CONTESTED |
| **CONTESTED** | amendment (proposes modified version) | recompute distance/breadth on each amendment; detect "does this version land in more acceptance sets & broaden span" | extraction (per amendment); geometry no-LLM | a version sits in enough acceptance sets AND signers are spectrum-broad → NEAR-COALITION |
| **NEAR-COALITION** | accept / decline synthesized plank | confirm spanning intersection | **synthesis + integrity gates fire here only** | all approve → PASSED; two broad basins → FORKED; deadline → DIED |
| **PASSED** | — | final breadth/cost/movement recorded | gate: text has teeth, signers moved | deposits into record + breadth meter + governance ratio |
| **FORKED** | — | two non-overlapping broad basins detected | — | spawn two child provisions, each re-enters CONTESTED |
| **DIED** | — | no spanning version by deadline | — | leaves "no-bridge issue this week" artifact; participants keep earned reasoning pts |

**Pass criteria (unchanged):** breadth · cost · specificity · movement. Each now measured in its proper space (breadth in Values space; the rest in sub-question space + intensity).

---

## Part C — The agent policy (spine of agent self-play)

Agents already exist (celebrity/historical, Values-grounded, source libraries). The loop needs **one new agent behavior**: not "argue to win" (debate behavior) but **"decide what I'd accept."**

- **Core function: `wouldSign(agent, version) → { accept: bool, intensity, reasoning }`** — given a free-form version, the agent decides acceptance from its Values profile + sources. This *operationally defines an acceptance set* and is the forcing function for the extraction schema (build this and you've designed the structure).
- **Act policy:** given provision state, agent chooses among {take position, propose amendment, accept/decline}. Amendment proposal = "what carve-out would move this version into my acceptance set without violating a high-intensity position."
- **Honest reporting:** the agent must be able to say "I'd accept the size-threshold version but not the blanket version" — partial, conditional acceptance is the whole point.

Agent self-play gives you **constructed test scenarios**: pick Values profiles on purpose — a bridgeable pair, an unbridgeable pair, a three-way that should fork — and check the geometry agrees with what you engineered.

---

## Part D — Phased build (Claude Code execution, with test gates)

### LAYER 0 — Provisions & structured engagement

**Phase 0.1 — Provision & engagement data model**
- *Build:* Entities — `Provision` (links to source briefing; status/state; relevant-Values-axes tag; deadline), `SubQuestion` (belongs to provision; emergent; can be added post-birth), `Position` (player, provision, stance, intensity, reasoning tag), `Amendment` (player, provision, free-form text, proposed version), `Version` (free-form text + extracted sub-question-position vector), `AcceptanceRecord` (player, version, accept/intensity). EF migration.
- *Test:* schema round-trips; a provision can hold positions, amendments, versions, acceptance records; a sub-question can be added to an existing provision without migration.
- *Gate:* all CRUD + the "add sub-question late" path pass.

**Phase 0.2 — Provision birth from a briefing**
- *Build:* system-extraction pipeline: briefing → neutral provision text + initial sub-question set (LLM extraction tier). Relevant-Values-axes tagging (one LLM call at birth).
- *Test:* feed the 4 sample briefings (SAMPLE_CONTENT.md); assert each yields a neutral-surface provision + ≥1 real-tradeoff sub-question; assert axis tags are sane.
- *Gate:* human review confirms provisions are neutral-surface / real-tradeoff (not partisan, not toothless) on all 4.

**Phase 0.3 — The extraction function (THE critical one)**
- *Build:* `extract(versionText, knownSubQuestions) → { positions: map<subQ, value>, newSubQuestions: [...] }`. Caching by text hash.
- *Test:* **extraction-fidelity suite** — a hand-labeled corpus of free-form versions with known sub-question positions; assert extraction matches human labels above a threshold; assert it correctly *adds* a new sub-question when a version introduces one.
- *Gate:* fidelity ≥ agreed threshold on the labeled corpus. **This is the highest-value gate in the plan — invest test effort here.**

### LAYER 1 — Geometry (computed, invisible)

**Phase 1.1 — Acceptance-set & overlap computation** *(depends 0.1, 0.3)*
- *Build:* represent each player's acceptance set as their acceptable region in sub-question space (from `AcceptanceRecord`s + extracted vectors). `overlap(players, version)` = does version sit in all their regions. No LLM.
- *Test:* synthetic players with constructed acceptable regions; assert overlap detection matches hand-computed truth.
- *Gate:* overlap correct on constructed cases incl. edge (empty overlap, full overlap, single-dimension disagreement).

**Phase 1.2 — Distance & breadth signals** *(depends 1.1)*
- *Build:* `distanceToCoalition(provision)` (how far the best spanning version is from sitting in enough acceptance sets) and `breadth(coalition)` (Values-axis span of signers — **measured against the league's composed spectrum, not self-selected responders**). `movement(player)` = acceptance set expanded to include a previously-rejected version.
- *Test:* assert distance *shrinks* when an amendment pulls a corner in; assert breadth *ignores headcount* (adding a signer in an already-covered region doesn't move it); assert movement fires on reject→accept.
- *Gate:* all three behave monotonically/correctly on constructed sequences.

**Phase 1.3 — Fork detection** *(depends 1.2)*
- *Build:* detect two non-overlapping broad basins vs. one.
- *Test:* a constructed three-way that should fork is flagged; a convergent case is not.
- *Gate:* fork vs. no-fork classified correctly on constructed scenarios.

### LAYER 2 — The coalition loop (agent-played vertical slice)

**Phase 2.1 — The state machine** *(depends 0.x, 1.x)*
- *Build:* the Part B state machine; transitions driven by acts + geometry; deadline handling; PASSED/FORKED/DIED resolution; deposit into record/breadth/governance-ratio.
- *Test:* drive the machine with scripted acts through every transition incl. each resolution; assert correct state at each step.
- *Gate:* every transition and all three resolutions traversed correctly by scripted input.

**Phase 2.2 — Agent acceptance + act policy** *(depends 2.1, agent infra)*
- *Build:* `wouldSign()` + agent act policy (Part C), reusing existing agent Values/sources.
- *Test:* a known-bridgeable agent pair reports overlapping acceptance after a sensible amendment; a known-unbridgeable pair never overlaps without violating a high-intensity position.
- *Gate:* agent acceptance behavior matches engineered expectations on the constructed pairs.

**Phase 2.3 — Synthesis + integrity gates** *(depends 2.1)*
- *Build:* near-coalition synthesis (draft plank from live amendments — bounded/precomputed-choices style) + the discrete gates (substantive? teeth? moved?).
- *Test:* synthesis produces a plank inside the spanning intersection; gates reject a cosmetic amendment and a toothless plank.
- *Gate:* gates catch the negative cases; synthesis output is itself accepted by the would-be signers' `wouldSign()`.

**Phase 2.4 — THE VERTICAL SLICE (agent self-play)** *(depends all above)*
- *Build:* one briefing → one provision → 2–3 agents (one bridgeable pair) → run the loop autonomously end-to-end.
- *Test:* the bridgeable pair drives the provision to PASSED via a sensible amendment; distance signal moves as expected throughout; a passed plank lands in the record.
- *Gate:* **the full slice runs autonomously and produces a sane coalition.** This is the de-risking milestone — the core thesis is now demonstrated.

**Phase 2.5 — Widen the agent scenarios** *(depends 2.4)*
- *Build/Test:* scenarios engineered to fork (does it FORK?), to fail (does it DIE honestly?), to scale breadth (more agents → does breadth scale?). Strategic-over-breadth guard: a low-intensity-everywhere agent's cheap acceptances score low.
- *Gate:* each engineered property reproduced; the over-breadth guard demonstrably bites.

### LAYER 2H — Human gameplay (same machine, new input)

**Phase 2H.1 — Human act UI** *(depends 2.x)*
- *Build:* UI for the daily acts (reaction-with-reason, position+intensity, steelman, co-sign/amend) feeding the *same* state machine. The spectrum-bar surfacing of distance (covered/uncovered regions, deadline).
- *Test:* a human-driven provision traverses the same transitions as agent-driven; spectrum bar reflects geometry.
- *Gate:* human input produces identical machine behavior to scripted/agent input.

**Phase 2H.2 — Mixed agent+human provisions** *(depends 2H.1)*
- *Build:* agents and humans co-participate in one provision (agents as seed/ballast in thin early leagues).
- *Test:* mixed loop reaches coalition; broadcast-only invariant holds (no private channel created).
- *Gate:* mixed play works; safety invariant verified.

### LAYER 3 — Ladder, leagues, campaign (most empirical — observe real loops first)

**Phase 3.1 — Provision gap-width estimation** *(depends data from 2.4/2.5)*
- *Build:* estimate a provision's expected gap width at birth (from how Values axes typically spread on the issue type) so the curriculum can sort it. Calibrate against *observed* closure data from agent self-play.
- *Test:* estimated gap width correlates with observed difficulty-to-close in the self-play corpus.
- *Gate:* estimator predicts observed closure difficulty above chance.

**Phase 3.2 — Difficulty laddering** *(depends 3.1)*
- *Build:* serve narrow-gap provisions to new leagues; widen as the group's bridging track record grows. Per-group skill level.
- *Test:* a new league receives provisions whose acceptance sets are close to overlapping; a veteran league receives wider gaps.
- *Gate:* served gap width tracks group skill on simulated league histories.

**Phase 3.3 — League composition** *(depends 3.2)*
- *Build:* structured-diverse leagues (balanced spectrum, invisible to users); scoring tilted so cross-cutting play climbs fastest. Age-banding layer.
- *Test:* composed leagues span the intended spectrum; scoring rewards breadth over volume; age-banding prevents adult↔minor exposure where required.
- *Gate:* composition + scoring produce breadth-favoring standings on simulated cohorts.

**Phase 3.4 — Campaign milestones & promotion/relegation** *(depends 3.x)*
- *Build:* legislative record, coalition-breadth meter, governance-vs-culture ratio accruing over the election-calendar campaign; league promotion/relegation to keep players near their ability edge; soft "campaign participation" cadence (not hard streak).
- *Test:* milestones accrue correctly from passed planks; promotion moves over-skilled players to wider-gap leagues; cadence rewards consistency without all-or-nothing breakage.
- *Gate:* a simulated full campaign produces sensible records, breadth meters, ratios, and league movement.

---

## Part E — Cross-cutting workstreams (parallel to the layers)

- **Extraction-fidelity test corpus** — grows continuously; the single most important test asset. Start in 0.3, keep expanding.
- **LLM-judge prompts** — extraction, the three integrity gates, synthesis. Each has its own prompt, failure modes, cost. Track as one workstream; apply precomputed-choices discipline.
- **Agent self-play harness** — the validation + seeding engine. Reused from 2.2 onward as a regression harness for every later change.
- **Cost telemetry** — measure LLM calls per provision per state; assert the continuous geometry stays no-LLM and integrity gates stay near-coalition-only.

## Part F — Sequencing summary
0.1 → 0.2 → 0.3 → 1.1 → 1.2 → 1.3 → 2.1 → 2.2 → 2.3 → **2.4 (de-risk milestone)** → 2.5 → 2H.1 → 2H.2 → 3.1 → 3.2 → 3.3 → 3.4
