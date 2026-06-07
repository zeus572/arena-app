# 06 — The Distance Signal (Acceptance-Set Model)

**Status:** Core definition resolved this conversation. Distance is relocated from Values-axis geometry to **provision-clause acceptance space.** This supersedes the "distance on the Values axes" framing in earlier docs (03/04/05).

## The decision

Rather than add more Values Profile dimensions (rejected — overwhelms users if user-facing; breaks the traceability contract and reads as manipulation if hidden), **distance is measured in provision space, not person/values space.** The resolution we needed is local to each provision, so it's solved with local provision data, not by inflating the global axis set.

### Why not more dimensions
- More **user-facing** axes → overwhelm (the product's whole pitch is no-box, low-friction).
- **Hidden values dimensions** → break the Values spec's traceability contract ("every profile statement traceable, user-correctable"); a latent ideological axis is exactly the box, just invisible. Skeptical adults will read it as manipulation, correctly.
- More dimensions also **manufacture artificial distance** — distance generically grows with dimensionality; irrelevant axes push bridgeable people apart.
- **Allowed exception:** hidden *derived/behavioral* signals (bridging skill, persuadability, movement history, reasoning-consistency) are fine to keep internal — they're behavioral observations, not political boxes. Just never a hidden *values* axis.

## Core concept: the acceptance set

For a given provision, a user is **not a point in values-space.** They are a **region of acceptable provision-configurations** — the set of versions (base text + amendments/clauses) they would co-sign.

- **Distance between two users** = how far apart their acceptance regions are / how little they overlap.
- **A coalition exists** where enough acceptance regions **intersect** on a specific configuration **that also spans the spectrum.**

This is more civically honest (people who share values still bargain over specifics) and more bridgeable (a clause carve-out closes gaps that values-talk never could). It relocates distance to **where governance actually happens.**

## Two quantities, now in their natural spaces

The old model crammed two things into one geometry. They now separate cleanly:

| Quantity | Lives in | Measures |
|---|---|---|
| **Distance** | Provision-clause / acceptance space | How far apart users are on *this provision* — overlap of acceptance sets |
| **Breadth** | Values-axis space | Whether the people in the overlap *span the spectrum* |

**A pass = a configuration exists in the intersection of enough acceptance sets, AND those signatories are values-broad, AND the configuration has teeth, AND signers moved to reach it.** (breadth · cost · specificity · movement — unchanged, but now each is measured in the right space.)

Values axes keep a job: they define **breadth** (did the coalition span the spectrum?), not **distance** (how far apart on this provision?).

## Amendments are the distance-moving engine

In the values model, closing distance required someone to *change their mind* — rare, slow, hard to detect honestly. In the acceptance-set model, **distance closes when an amendment reshapes the configuration so it lands inside more acceptance sets.** That's bargaining, and it's observable.

- **Measure an amendment's value** = how much it expands the *spanning* intersection (pulled in a new spectrum-corner > deepened an already-covered one).
- Makes the amendment act (already flagged as where the real cognition lives) the literal engine of distance reduction.

## Movement, redefined (and cheaper to detect)

- **Old:** detect a values shift — fuzzy.
- **New:** did a user's acceptance set **expand to include a configuration they earlier rejected?** Discrete, logged, honest: rejected version A → amendment produced B → signed B.
- **Geometry detects that movement happened (no LLM).** LLM only audits whether the amendment that moved them was a *substantive* concession vs. cosmetic. Cost spine intact.

## Forking falls out of the math

A fork is simply: **no single configuration in a spanning intersection, but two.** If acceptance sets cluster into two non-overlapping basins, each of which is values-broad, that's two governable answers — detected geometrically, not handled as a special case.

## Cost model (how acceptance sets stay cheap)

The real cost risk: you can't make users rate every clause-variant to map their acceptance set. So:
- **Infer** acceptance from sparse signals already collected: position, intensity, the amendment they proposed/co-signed, reasoning tag.
- **Probe** only at the moment it matters — when a near-coalition configuration needs "would you also accept this slight variant?"
- **Precomputed-choices architecture earns its keep:** the system precomputes the small set of configurations a provision is converging toward and asks users to react to *those bounded options* (max-2-refinement pattern). The acceptance set is approximated by responses to a handful of precomputed variants, never enumerated. Fits the locked cost architecture exactly.

**Structured (no LLM):** acceptance-set overlap, intersection size, breadth of intersection (values-spread of signers), movement detection, fork detection. Continuous, every provision, every day.
**LLM (discrete gates only, near-coalition only):** amendment substantive vs. restatement, final text has teeth, claimed bridge genuine, provision axis-tagging at birth (one call per provision).

## Failure mode guard: strategic over-breadth

Acceptance sets can be gamed by marking willingness to accept almost anything → coalition credit for free (the mush problem, new hat).
- **Guard via intensity + cost:** an acceptance that required giving up a *high-intensity* position counts; an acceptance from someone who'd accept anything (low intensity everywhere) is cheap → scores low.
- **Non-negotiable signal protects the honest holdout:** a user whose acceptance set legitimately *excludes* the only spanning configuration is not a failed bridge — they're a **principled dissent**, distinguishable because their exclusion is anchored to a high-intensity axis. Geometry tells the difference.

## The three stacked jobs, restated in this model

1. **Daily return hook** — "this configuration is one corner short of spanning; the [market/precaution] corner hasn't accepted; 3 days left." Directional and actionable, not a bare dial.
2. **Anti-majoritarian pass criterion** — pass needs breadth of the *intersection*, not headcount. Adding signers in an already-covered region does nothing.
3. **Difficulty knob** — a provision's expected difficulty = how disjoint the acceptance sets are at birth (estimated from issue type). New leagues get provisions whose acceptance sets are *close to already overlapping*.

## Still open (carried forward)

- **Surfacing:** leaning toward a **spectrum bar showing covered vs. uncovered regions** (coalition's reach lit across the relevant axis, dark corners = unrepresented, deadline ticking) over a single "% to coalition" number — the visual *is* the call to action and makes breadth-not-headcount visceral.
- **Multi-axis provisions:** when a provision loads on 2–3 axes, must breadth be covered on *all* of them or just the dominant one? Determines whether distance is a scalar or a per-axis vector, and whether multi-axis provisions naturally tend to fork. (Acceptance-set model leans: incomplete cross-axis coverage is a natural fork trigger.)
- **Payout coupling:** should payout = spread-at-start × breadth-achieved, so bridging a genuinely polarized provision is worth dramatically more? Makes difficulty ladder and scoring economy the same mechanism — check against the "don't let advanced players lap everyone" balance concern.
- **Engaged-pool vs. composed-spectrum:** measure breadth against the *league's composed spectrum* (known, since we placed them) rather than self-selected responders, to avoid sampling artifacts from who happened to post.
- **Axis-tag accuracy:** provision axis-tagging at birth is one LLM call; add a correction signal if observed position data shows the real disagreement is on a different axis than tagged.
