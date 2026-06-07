# Claude Code — Kickoff Prompt: Civic Arena Coalition Game (Layer 0)

> Paste everything below the line into Claude Code as the initial instruction. Attach `07_IMPLEMENTATION_PLAN.md` (and the rest of the design docs for context) to the session. This kickoff covers the **unattended run of Layer 0 (Phases 0.1 → 0.2 → 0.3)**, with a hard stop for human review at 0.3.

---

## Your task

You are implementing **Layer 0** of the Civic Arena coalition game, following `07_IMPLEMENTATION_PLAN.md`. Read **Part A (Architectural principles)** and **Part D → LAYER 0** in that doc before writing any code. Those principles are binding constraints, not suggestions.

You will build **Phases 0.1, 0.2, and 0.3 in order**, writing tests between each, **running unattended** up to a mandatory stop. Read the operating rules below carefully — they govern how you proceed between phases.

## Operating rules for unattended multi-phase execution

1. **The test is the gate.** Each phase has a Gate in the plan. You may only advance to the next phase when that phase's Gate test **passes when actually executed** — not when you believe it would pass. Run the tests. Read the real output.

2. **A failing gate HALTS the chain.** If a gate test fails and you cannot make it pass with a *straightforward, correct* fix (not a hack, not weakening the test, not deleting the assertion), **stop immediately.** Do not proceed to the next phase. Do not paper over it. Write your status to `BUILD_LOG.md` (see rule 5) and end the run.

3. **Never weaken a test to make it pass.** If you find yourself tempted to relax an assertion, lower a threshold, skip a case, or mark a test as expected-fail to get green, that is the signal to STOP and leave it for human review instead. Note it in `BUILD_LOG.md`.

4. **MANDATORY HUMAN STOP at end of Phase 0.3.** Phase 0.3 (the extraction function) has a gate that is partly a *judgment* call (does extracted structure match human meaning). You must **build 0.3 and its test harness, run it, record results, and then STOP** — do not treat 0.3's gate as self-certifiable and do not proceed past it under any circumstances. Phase 0.3 ends this unattended run regardless of outcome.

5. **Keep a running `BUILD_LOG.md`** at the repo root. After each phase, append a section with: phase id, what you built (files + brief description), the exact test command(s), the actual test output (pasted, not summarized), gate PASS/FAIL, and any decisions or assumptions you made. This is how the human will walk through your work afterward — make it complete enough to audit without re-running anything.

6. **Commit per phase.** One git commit per completed phase, message `Phase 0.x: <short description>`. This lets the human walk back to any phase boundary.

7. **Surface assumptions, don't silently resolve them.** When the plan is underspecified (e.g. exact field types, framework choices), make a reasonable choice consistent with the existing Civic Arena codebase conventions (EF migrations, the existing backend service layer — see design docs), implement it, and **record the assumption in `BUILD_LOG.md`** so the human can tweak it. Do not stop for trivial choices; do stop (per rule 2) only for genuine gate failures.

8. **Stay inside Layer 0.** Do not start Layer 1 geometry, the state machine, agents, or any later layer. If you finish 0.3's build + tests + review-stop with time/context to spare, do NOT continue — summarize and end.

## What to build (summary — the plan is authoritative)

**Phase 0.1 — Provision & engagement data model.** Entities: `Provision`, `SubQuestion` (emergent; addable post-birth without migration), `Position`, `Amendment`, `Version` (free-form text + extracted sub-question-position vector), `AcceptanceRecord`. EF migration.
- *Gate:* schema round-trips; CRUD works; **a sub-question can be added to an existing provision without a new migration** (this "late sub-question" path is critical per principle A4 — test it explicitly).

**Phase 0.2 — Provision birth from a briefing.** Pipeline: briefing → neutral provision text + initial sub-question set (extraction-tier LLM). Relevant-Values-axes tagging (one LLM call at birth).
- *Gate:* run against the 4 sample briefings in `SAMPLE_CONTENT.md`; each yields a neutral-surface provision + ≥1 real-tradeoff sub-question; axis tags sane. **Because "neutral-surface / real-tradeoff" is a judgment, write the outputs to `BUILD_LOG.md` for human review but you MAY proceed to 0.3** (0.2 is not the mandatory stop — 0.3 is). Flag any provision that looks partisan or toothless.

**Phase 0.3 — The extraction function (CRITICAL — ends this run).** `extract(versionText, knownSubQuestions) → { positions, newSubQuestions }`, cached by text hash.
- *Build* the function AND a **fidelity test harness** that runs a hand-labeled corpus (create a starter corpus of ~10–15 labeled free-form versions across the sample provisions; clearly mark it as a starter the human will expand). Assert extracted positions match labels above a threshold; assert a version introducing a new sub-question causes `newSubQuestions` to fire.
- *Record* the fidelity results in `BUILD_LOG.md` in full.
- **STOP. Do not proceed.** Leave a clear "AWAITING HUMAN REVIEW OF EXTRACTION FIDELITY" banner at the top of `BUILD_LOG.md`.

## Definition of done for this run

`BUILD_LOG.md` exists and documents 0.1 (gate PASS), 0.2 (built, outputs recorded for review), 0.3 (built, fidelity harness run, results recorded, AWAITING REVIEW banner). Three commits. No code from any later layer. If you halted early on a gate failure, the log explains exactly where and why.

## Start

Begin by reading Part A and Layer 0 of `07_IMPLEMENTATION_PLAN.md`, then confirm your understanding of these operating rules in one short paragraph, then start Phase 0.1. Do not ask for confirmation after that — run unattended per the rules until the Phase 0.3 stop or a gate halt.
