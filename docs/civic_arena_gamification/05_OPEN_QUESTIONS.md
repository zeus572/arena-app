# 05 — Open Questions & Next Steps

## Distance signal — RESOLVED → see `06_DISTANCE_SIGNAL.md`

Core definition resolved: distance is measured in **provision-clause acceptance space**, not Values-axis geometry. A user is a *region of acceptable provision-configurations* (an acceptance set); distance = overlap of acceptance sets; breadth still lives in Values space; amendments are the distance-moving engine. Decided against adding Values dimensions (overwhelm if visible; manipulation if hidden) — resolution is local to provisions and solved with local data.

Remaining sub-questions on the signal (carried into doc 06's "Still open"):
- **Surfacing form** — leaning spectrum-bar (covered/uncovered regions) over a "% to coalition" number.
- **Multi-axis provisions** — breadth on all loaded axes vs. dominant only; likely a natural fork trigger.
- **Payout coupling** — payout = spread-at-start × breadth-achieved? (ties difficulty ladder to scoring economy; check balance).
- **Engaged-pool vs. composed-spectrum** — measure breadth against the league's *composed* spectrum, not self-selected responders.
- **Axis-tag accuracy** — birth-time tagging is one LLM call; add correction if position data disagrees.

## Other open decisions

### Minimum viable daily act (~60s)
Leading candidates: reaction-with-reason (governance vocabulary) or single fact/prediction/value tag. Pick the floor that's genuinely educational, earns, and is survivable on a busy day.

### Who holds the pen on final provision text / when it locks
Leaning: **system drafts synthesis from live amendments; signatories publicly approve** (async-native, no single-author hijack, broadcast-only safe). Cost caveat: reserve full synthesis for near-coalition provisions only.

### Streak vs. soft cadence
Leaning **soft "campaign participation" cadence** over hard daily streak, given mixed-age audience and wellbeing concerns. Confirm.

### League composition
Leaning **structured-diverse with invisible diversity.** Confirm, and decide age-banding interaction with diversity (age-banded leagues may constrain how wide a spectrum any one league can span).

### Cumulative governance — confirmed direction
Governance is **cumulative** across a campaign (legislative record, breadth meter, governance ratio accrue), not fresh-per-round. Heavier to build but far more motivating. Confirmed in conversation; flagging build-weight.

## Cost spine (carry into every decision)
- **Structured scoring (no LLM)** for continuous, every-provision, every-day signals: distance/spread on Values axes.
- **LLM only for irreducible discrete judgments:** amendment substantive vs. restatement; final text has teeth; signatories actually moved; steelman quality; bridge genuineness.
- Consistent with the precomputed-choices philosophy (~80% LLM cost reduction target).

## Systems to integrate (from existing Civic Arena)
- Values Profile axes (the spectrum the distance signal reads)
- Common Ground rubric (genuine vs. platitude agreement, applied to humans)
- Civic Briefings pipeline (spawns provisions)
- Ranking Engine (scoring substrate; may need provision/format-aware components)
- Bot Heartbeat (daily cadence, provision lifecycle scheduling)
- Candidate / Campaign Manager mode (the candidate's coalition-dependence ties the meta-loop together)

## Not yet touched (future)
- Exact data models (Provision, Position, Amendment, Coalition, Signatory, LeagueSkillLevel, etc.)
- API surface
- Moderation pipeline for user-authored provisions and dissents
- Frontend / UX component specs
- How fictional-candidate content-safety posture interacts with user-authored provisions
- Non-election-year mode (already deferred platform-wide)
