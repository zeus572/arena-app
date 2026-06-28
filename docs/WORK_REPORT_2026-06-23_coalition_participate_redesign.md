# Work Report — Coalition participate-page redesign

**Date:** 2026-06-23
**Branch:** `feature/coalition-ui-redesign` (git worktree at `.claude/worktrees/coalition-ui-redesign`)
**Scope:** Frontend-civic only. No backend, API, or data-model changes.

## The brief

> "Clean up the coalition UI — it's still not clear what is the prevailing
> position and how many are on the table. The starting drafts are on the
> participate page in a chevron. I think this page needs a UX redesign. The
> calls to action are not prominent. And they're not enticing action."

Four problems, addressed below.

## What changed

### 1. "What is the prevailing position / how many are on the table" — now explicit

Both the overview (`CoalitionProvisionDetail`) and the participate page lead with
the **Prevailing coalition position** card, and that card now carries an
explicit count chip: **`N on the table`** (counts cohort-*presented* positions;
neutral seed drafts are not counted as positions). On the overview the leading
line also notes "leading N positions" when more than one is in play.

- 0 presented → chip reads `0 on the table` and the copy flips to "Be the first
  to take a position."
- ≥1 presented → chip shows the count and the leading wording's co-sign/decline
  tally.

### 2. Starting drafts — out of the chevron, into the comparison

The old page hid neutral drafts inside a `<details>` chevron at the very bottom,
so you couldn't see how many wordings were actually in play. They're now a
labelled **"Neutral starting drafts (N)"** group inside Step 2's comparison,
directly under **"Positions on the table (N)"**. Drafts keep their dashed,
de-emphasized frame (they're reference wordings, not anyone's position) but are
discoverable and ranked by how well they match your answers — same as real
positions.

### 3. Participate page — redesigned as a two-step funnel

`CoalitionProvisionParticipate.tsx` was restructured around a clear flow:

- **Step 1 · Your answers** — numbered step header with a live progress bar and
  `2/4 answered`; answered sub-question cards highlight and check off.
- **Step 2 · Where you land** — appears live as you answer (the old "Save my
  answers" gate, which only toggled local state, is gone). Each version row
  checks your picked answers against its positions inline (green = match, with a
  per-version `3/4 match` bar), and the closest presented position gets a
  **"Closest to you"** star.

### 4. Calls to action — prominent and enticing

- **Participate:** the present action is now a filled-accent hero card —
  "None of these fit? Put yours on the table" / first-mover "Be the first…" —
  with a full-width `Put my version on the table` / `Present this as the bill`
  button. Co-sign / Decline sit on every version row.
- **Overview:** the "Take part" box was an outlined panel lost among other
  accent-bordered cards; it's now a **filled** primary CTA (white-on-accent,
  arrow nudges on hover) both inline and in the desktop rail.

## Notes / decisions

- **`data-testid`s preserved** (`coalition-participate`, `prevailing-position`,
  `subquestion-cards`, `closeness-list`, `starting-drafts`, `present-version`,
  `participate-cta`, `opt-*`, `match-*`) and added (`on-the-table-count`,
  `present-cta`, `presented-row`, `draft-row`, `compare-empty`). No unit or e2e
  test references the coalition routes today, so nothing was broken, but the
  selectors are kept stable for future tests.
- Matched the existing `var(--line)` / `var(--accent)` token convention used
  throughout these pages (note: `--line` is undefined repo-wide but is the
  established pattern here — not changed, to keep my sections visually identical
  to their surroundings).
- No copy/strings were added to committed config; no LLM, backend, or DB touch.

## Verification

- `npm run build` (tsc -b + vite build) passes clean in the worktree.
- Rendered every state with Playwright against mocked API responses (authed
  session faked via a future-exp token + mocked `/profile/me`). Screenshots in
  `frontend-civic/shots/` (untracked): `detail-versions`, `participate-answered`,
  `participate-initial`, `participate-firstmover`.
- ESLint is not runnable repo-wide (`eslint.config.js` missing — pre-existing).
