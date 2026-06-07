# Reusable Kickoff Template — Subsequent Phase Batches

> Use this to launch later unattended batches (e.g. Layer 1, Layer 2) after you've reviewed the prior batch. Copy, fill the brackets, paste into Claude Code. The operating rules are the same discipline that protects unattended runs; keep them verbatim.

---

## Your task

You are continuing the Civic Arena coalition game build, following `07_IMPLEMENTATION_PLAN.md`. Read **Part A (principles)** and the plan sections for **[PHASES IN THIS BATCH, e.g. 1.1 → 1.2 → 1.3]** before writing code.

Prior work is in this repo; review `BUILD_LOG.md` to see where the last batch ended and what assumptions were recorded. **Do not rebuild prior phases** — depend on them.

You will build **[PHASES]** in order, with tests between each, running unattended until **[STOP CONDITION — e.g. "the end of Phase 1.3" or "the mandatory review stop at Phase X"]**.

## Operating rules for unattended multi-phase execution

1. **The test is the gate.** Advance only when a phase's Gate test *passes when actually executed*. Run tests; read real output.
2. **A failing gate HALTS the chain.** If you can't fix it with a straightforward, correct fix (no hacks, no weakened tests), STOP, record in `BUILD_LOG.md`, end the run.
3. **Never weaken a test to make it pass.** Temptation to relax an assertion = signal to STOP for human review.
4. **MANDATORY HUMAN STOP at:** [name any judgment-heavy gate in this batch that must not be self-certified — e.g. a gate involving "does this read as genuine/neutral/has-teeth." If none, write "none — run to end of batch."]
5. **Append to `BUILD_LOG.md`** after each phase: phase id, files built, exact test commands, pasted actual output, gate PASS/FAIL, decisions/assumptions.
6. **Commit per phase**, message `Phase X.y: <desc>`.
7. **Surface assumptions, don't silently resolve.** Underspecified → choose consistent with existing conventions, record it, continue. Stop only for genuine gate failures.
8. **Stay inside this batch.** Do not start phases beyond [PHASES]. If you finish early, summarize and end — do not continue.

## What to build

The plan is authoritative. Summary of this batch:
- **[Phase X.y]** — [one-line build] · *Gate:* [one-line gate]
- **[Phase X.z]** — [one-line build] · *Gate:* [one-line gate]

## Definition of done

`BUILD_LOG.md` documents each phase with gate results; one commit per phase; no code beyond this batch; early halts explained.

## Start

Read Part A and the relevant plan sections, review `BUILD_LOG.md`, confirm understanding of these rules in one paragraph, then begin **[first phase]**. Run unattended per the rules until the stop condition.

---

## Suggested batch boundaries (where to place human review stops)

- **Layer 0:** 0.1 → 0.2 → **0.3 STOP** (extraction fidelity = judgment gate). *(this is the first kickoff)*
- **Layer 1:** 1.1 → 1.2 → 1.3, run to end (all geometry gates are machine-checkable on constructed cases — safe to run unattended).
- **Layer 2:** 2.1 → 2.2 → 2.3 → **2.4 STOP** (the vertical-slice de-risk milestone — review the autonomous coalition by hand before widening).
- **Layer 2 cont.:** 2.5, then **2H.1 STOP** (first human-facing surface — review UX + that distance bar reflects geometry honestly).
- **Layer 2H:** 2H.2, run to end.
- **Layer 3:** 3.1 → 3.2 → 3.3 → 3.4. Consider a STOP after 3.1 (gap-width estimator calibration is judgment-adjacent — confirm it tracks observed difficulty before laddering on top of it).

**Rule of thumb for where to place a STOP:** any gate whose pass/fail depends on a *judgment* ("does this read as genuine / neutral / has-teeth / sane") rather than a *constructed assertion* should end an unattended batch, because the agent self-grades those least reliably and they're upstream of expensive downstream work.
