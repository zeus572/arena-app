# 01 — Philosophy & Win Condition

## The central problem with gamifying politics

Duolingo's gamification works because the skill (language) has an objective correctness gradient — a green checkmark means something. **Civic reasoning has no such gradient.** You cannot reward "correct political opinion." So the foundational design choice is: *what are you actually rewarding?* That choice shapes everything downstream.

### Two tempting-but-wrong substrates

- **Gamifying persuasiveness / winning** → builds a debate-bro engine that rewards the most aggressive players and punishes the bridge-building we want. Zero-sum by construction; structurally reproduces the tribalism we're trying to escape.
- **Gamifying engagement / volume** → a content treadmill; the grindiest poster wins regardless of quality; reproduces the loud-edges problem.

Neither serves the academic goal.

## The win condition: a legislature, not a debate

The win condition is **building a bipartisan coalition that can govern.** This is the key structural move:

- A debate model is **zero-sum** — requires a loser — and zero-sum is the exact dynamic that made politics personal.
- A legislature model is **non-zero-sum** — multiple people win together or nothing passes. This is both the actual shape of governance and the actual antidote to the culture-war frame.

### This dissolves the candidate-loyalty conflict

Earlier tension: if the candidate a user manages is a partisan trying to win, but the meta-game rewards bridging, the two loops fight and players feel whiplash.

The legislature model resolves it: **the candidate's job is to govern, which means the candidate needs a coalition to pass anything.** Partisanship becomes a *cost* — rigid partisanship → smaller assemblable coalition → less passed → lower score. We've made the thing we want to teach (coalition-building beats purity) into the literal optimal strategy. The fun-maximizing play and the civically-desirable play are the same play.

## The governance-vs-culture thesis

> Politics has become personal and cultural, and the civics angle of governance is missed.

- **Cultural-values conflicts are often genuinely irreconcilable** — they bottom out in identity and moral foundations. Skilled discourse does not dissolve them. (And cultural values have a legitimate place — the game does not punish values talk.)
- **Governance questions frequently have available agreement that the cultural framing obscures.** Two people who will never agree on the cultural meaning of immigration can often agree that asylum processing shouldn't take six years.

The game's core skill: **find the governance layer underneath the values fight** — descend from "what kind of country are we" to "what should this specific institution do." Learnable, scorable, and almost untaught anywhere else.

### Mechanical consequence: a second scoring axis

The LLM judge scores not just **reasoning quality** but **governance score**: does this contribution operate at the level of institutions, mechanisms, tradeoffs, and implementable action — or at the level of identity and cultural signaling? Not punishing values talk; *rewarding the move into governance.*

## What the scoring rewards (and the bridge as apex)

The leaderboard's headline reward is **finding cross-spectrum common ground.** But this must be the *real* version, not the shallow one:

- **Shallow (rejected):** an "agree" button or vibes-based "you found common ground!" badge. Spammed instantly; reads as forced civility theater. (The Values spec already warns against platitude-agreement like "we both love this country.")
- **Real:** an agreement only *counts* when it is specific, names the actual policy/principle, is anchored to the briefing/provision, and is confirmed by an LLM judge as concrete, costly, and genuinely cross-cutting. Hard to fake — which is exactly why it's worth points. (Borrows directly from the agent Common Ground rubric: "NAFTA was bad for workers" beats "we both love America.")

## Design traps to actively guard against

1. **Lowest-common-denominator mush.** The failure mode of any agreement game: optimal play becomes proposing the blandest plank everyone signs because it says nothing. **Guard:** a position/plank must be specific enough to actually constrain a real institution's behavior, and must *cost* its signatories something (giving up a maximalist position). A coalition that gave up nothing didn't bridge anything.

2. **Naive both-sidesism / abandoning principle for points.** Sometimes the right civic answer is *not* to bridge. A game that rewards coalition über alles teaches people to abandon principle. **Guard:** use the intensity/non-negotiable signal (from the Values spec). If a briefing touches a user's non-negotiable, refusing the coalition is not penalized; the game occasionally surfaces "this is a place principled people *don't* bridge, and that's legitimate." Mixed-age skeptical adults will smell naive civility and reject it.

3. **Volume dominance.** "Engagement earns points" lets the grindiest poster win. **Guard:** daily reasoning points have a low ceiling and diminishing returns; the scarce breadth-expanding coalition moments stay uncapped. A thoughtful adult checking in 3×/week with two real bridges should out-rank a teen grinding 20 mediocre comments/day. This is also what makes it fair across a heterogeneous audience.

4. **Manufactured (fake-easy) success.** When laddering difficulty down for new users, do NOT serve fake-easy provisions where "agreement" is really platitude-consensus. That's the mush problem in a tutorial costume; advanced users will look back and realize early wins were hollow, breaking the credibility chain. **Guard:** early provisions are *small-gap but real-tradeoff* — training wheels on a real bike, not a tricycle.

## Safety posture (driven by mixed-age public audience)

No teacher as a filter or safety layer. Safety must be **structural, not supervised:**

- **Broadcast-only social surface.** The bridge/coalition mechanic only ever surfaces *public, already-posted* statements for co-signing. **Never open a private channel between two users as a reward.** This protects against adult–minor private interaction and keeps everything inspectable.
- Consider **age-banded leagues** as an additional layer.
- Fictional-candidate posture remains a content-safety decision independent of these mechanics.
