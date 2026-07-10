# XP System — How It Works, How It Ladders, and Where to Re-evaluate

*A working document for reconsidering the Civic Arena XP economy. Part 1 describes the
system as built (pinned to code); Part 2 is the re-evaluation — tensions, levers, and
open questions. When this disagrees with code, the code wins — see
`04_SCORING_PROGRESSION_AND_LEAGUES.md` for the canonical "as built" reference.*

---

## 0. TL;DR

- There are **two currencies**: **reasoning XP** (daily grind, capped + diminishing, drives your *level*) and **scarce coalition points** (rare cross-spectrum moves, uncapped, drives *prestige*).
- There are **two progression ladders**: a personal **level** ladder (`500 XP/level`, 10 named tiers) and a **Circle** skill-cohort ladder (`Citizen → Founder`), plus a per-group **difficulty ladder** with promotion/relegation.
- The **daily quests are the real engine** — 90 XP/day from 4 quests, bypassing diminishing returns. Raw acting front-loads then decays to a 1-XP tail.
- Everything worth ~one act-worth lives in one pure file (`CoalitionPoints.cs`). The **level formula lives in the frontend** (`PlayerHome.tsx`), not the backend.
- Separately, the **Debate app has its own trivial `User.Xp`** (a linear recompute from activity counts) — unrelated, no anti-abuse, a candidate for reconciliation or retirement.

---

# Part 1 — The system as built

## 1.1 Two currencies

Every scored action writes exactly one `CoalitionAct` row, paid in one currency.

| Currency | Earned by | Rules | Drives |
|---|---|---|---|
| **reasoning** | daily micro/mid acts — reactions, positions, steelmans, co-signs, briefing reads, campaign responses, the culture↔governance sort | **diminishing returns + daily cap (150)** | your **level** (daily grind / leaderboard floor) |
| **scarce** | macro/coalition acts — `AuthorProvision`, `WritePlank`, `PrincipledDissent`, `Longform`, `CoalitionPassReward` — plus the all-quests bonus | **uncapped, no diminishing** | **prestige** (the "Coalition pts" tile; biggest standing jumps) |

The split is the structural guard against volume dominance: showing up daily is necessary
but plateaus; climbing requires the rarer cross-spectrum governance moves.

*Source: `backend-civic/Services/Coalition/Product/CoalitionPoints.cs`, `ReasoningLedger.cs`.*

## 1.2 What each act is worth (`CoalitionPoints.BasePoints`)

| Act | Base | Currency | Quality-gated? |
|---|---|---|---|
| `CampaignReaction` | 1 | reasoning | — |
| `BriefingRead` | 2 | reasoning | — |
| `CoSign` | 2 | reasoning | — |
| `ReactionWithReason` | 3 | reasoning | — |
| `ClaimTag` | 3 | reasoning | — |
| `DiedReasoningPayout` | 4 | reasoning | — |
| `Position` | 5 | reasoning | — |
| `ReactAndRoute` | 5 | reasoning | — |
| `CampaignNewsResponse` | 5 | reasoning | — |
| `CultureGovernanceSort` | 6 | reasoning | — |
| `Steelman` | 8 | reasoning | ✅ |
| `Amend` | 12 | reasoning | — |
| `PrincipledDissent` | 20 | scarce | ✅ |
| `Longform` | 20 | scarce | ✅ |
| `AuthorProvision` | 25 | scarce | ✅ |
| `WritePlank` | 30 | scarce | ✅ |
| `CoalitionPassReward` | 30 (+ breadth×5) | scarce | — |
| `QuestReward` | 0 (caller sets amount) | reasoning / scarce | — |

**Agree-vs-amend asymmetry** is encoded here: a bare `CoSign` is 2; a substantive `Amend` is 12. Mush is cheap; carve-outs pay.

**Quality gating** — for `Steelman`, `Longform`, `PrincipledDissent`, `WritePlank`, `AuthorProvision`, base is scaled by `max(0.2, judgeQuality/100)`. A weak contribution earns as little as 20% of base. Quality is scored by an LLM (or heuristic fallback) judge in `CoalitionLoopService.RecordActAsync`.

## 1.3 The reasoning curve (diminishing returns + daily cap)

```
factor    = 0.8 ^ (reasoning acts already done today)   // DiminishingFactor = 0.8
points    = max(1, round(base * factor))                // never below 1
remaining = max(0, 150 - reasoning earned today)        // DailyReasoningCap = 150
award     = min(points, remaining)                       // clamp to the cap
```

Two knobs: **`DailyReasoningCap = 150`** and **`DiminishingFactor = 0.8`**.

Worked example — repeating a base-5 act all day:

| Act # | factor (0.8ⁿ) | award |
|---|---|---|
| 1 | 1.00 | 5 |
| 2 | 0.80 | 4 |
| 3 | 0.64 | 3 |
| 4 | 0.51 | 3 |
| 5 | 0.41 | 2 |
| 6 | 0.33 | 2 |
| 7 | 0.26 | 1 |
| 8+ | … | 1 (floor) |

Raw acting front-loads (~19 XP over the first six acts) then pays a flat **1 XP/act** tail — a deliberate anti-farm shape. Pure grinding plateaus well under the cap. **Scarce currency is never capped or diminished.**

> **History:** cap was `30` / factor `0.6` until 2026-06 (compressed everyone toward ~30/day). Raised to `150 / 0.8` to spread sustained effort while keeping a hard ceiling.

## 1.4 Daily quests — the real engine (`GetQuestsAsync`)

Completion is computed **server-side from the acts ledger**; the client owns only routing/subtitle.

| Quest id | Title | Reward | "Done" today when there is a … |
|---|---|---|---|
| `briefing-read` | Read today's briefing | 10 | `BriefingRead` act (deduped to once/day) |
| `co-sign` | Co-sign one coalition position | 20 | `CoSign` **or** `Amend` act |
| `campaign-headline` | Respond to a headline in your campaign | 30 | `CampaignNewsResponse` act |
| `bridge-culture` | Bridge a culture-war provision | 30 | `CultureGovernanceSort` **or** `ReactAndRoute` act |

Quest rewards behave differently from raw acts: granted **once per quest per day** (idempotent via a `QuestReward` marker act), **not** diminished (full value), but still **clamped to the daily cap**.

The four total **90 reasoning XP/day**. Because they bypass diminishing, *completing your dailies* — not grinding acts — is how an engaged player approaches the cap.

**All-quests bonus:** finishing all four grants **1 scarce coalition point**, once/day. The only scarce point earnable from the daily loop.

## 1.5 How it ladders

### (a) Personal level ladder — `PlayerHome.tsx` (frontend-derived)

```
XP_PER_LEVEL = 500
level = floor(reasoningXp / 500) + 1     // reasoning XP only; scarce never raises level
```

`1 Voter · 2 Advocate · 3 Organizer · 4 Aspirant · 5 Apprentice · 6 Co-signer · 7 Bridgewright · 8 Statewright · 9 Whip · 10 Speaker` — levels past 10 fall back to `Level N`.

**Days to a level, by engagement** (reasoning-only, from §1.6):

| Engagement | XP/day | Days per level (÷500) | Days to L10 (4500 XP) |
|---|---|---|---|
| Casual (~12) | 12 | ~42 | ~375 |
| Moderate (~34) | 34 | ~15 | ~132 |
| Engaged (~100) | 100 | ~5 | ~45 |
| Grinder (150 cap) | 150 | ~3.3 | ~30 |

### (b) Circle ladder — skill cohorts (`CoalitionLoopService.cs`)

A **Circle** is the skill/engagement cohort you're grouped into (the "league"), distinct from your level. Circles are composed by skill (`ComposeCirclesAsync`) and **named by rank in gap-tier order**:

`Citizen · Delegate · Framer · Senator · Statesman · Founder` (ascending gap difficulty). Ranks beyond the ladder → `Circle N`.

Deliberately shares **no names** with the level ladder, so the dashboard never shows the same tier word twice.

### (c) Difficulty ladder + promotion/relegation (`Curriculum/`)

Provisions are served by **gap width** matched to a group's skill (`DifficultyLadder.TargetGap`). New groups start on small-gap, bridgeable-but-real provisions (the cynicism-wall churn defense); the system widens the gap as the group demonstrates bridging skill. Individuals who outpace their Circle **graduate to wider-gap Circles** (`PromotionService.Decide`, margin `0.15`) so everyone stays at the edge of their ability.

### (d) Macro milestones (the campaign "stock")

Daily points are *flow*; a campaign accrues *stock* across three meters: **legislative record** (planks passed), **coalition-breadth** (span of spectrum bridged), and **governance-vs-culture ratio**. A passed provision deposits into all three. These are not XP — they're the long-game story XP feeds.

## 1.6 Ways to earn, by engagement level

| Engagement | Reasoning XP/day | How it's composed |
|---|---|---|
| **Casual** (reads briefing) | ~12 | briefing act (2) + briefing quest (10) |
| **Moderate** (briefing + co-sign) | ~34 | acts + briefing & co-sign quests (10+20) |
| **Engaged** (all 4 quests) | ~100 | ~10 XP of acts + 90 quest XP |
| **Grinder** (all quests + heavy acting) | up to 150 | 90 quest XP + diminishing act margin to the ceiling |
| **Prestige track** (any level) | scarce, uncapped | author provisions, write planks, principled dissent, longform, + coalition-pass payouts + the daily all-quests scarce point |

The intended shape: **quests carry the engaged player to ~90**; raw acts add an effort-proportional but decaying margin; **scarce is a parallel track** for the cross-spectrum builder, unbounded and level-invisible.

## 1.7 Anti-abuse (reasoning only)

- Diminishing curve + hard daily cap (§1.3).
- Quality gating on premium acts (§1.2).
- Briefing-read deduped to once/day; quest rewards idempotent once/day.
- Pass/died payouts guarded by existence checks (one per provision+user).
- Campaign reactions award only on a *genuinely new* reaction (flips earn nothing).
- Locality wall: a forged POST can't earn on an out-of-locality provision.
- **No time-decay** anywhere; lifetime totals are simple sums. Weekly leaderboards window the ledger but don't mutate stored XP.

## 1.8 The other XP system — Debate app (`backend/`)

Unrelated and much simpler. `User.Xp` is a single int, **recomputed on every stats read** from raw counts:

```
xp    = votes*10 + reactions*5 + debatesStarted*50 + predictions*20 + correct*30 + interventions*15
level = floor(sqrt(xp / 100)) + 1     // quadratic curve; titles Newcomer → Arena Legend
```

No cap, no diminishing, no anti-abuse, no ledger. Flagged here because "the XP system" is ambiguous across the two apps — worth deciding whether to align, retire, or keep it deliberately separate.

---

# Part 2 — Re-evaluation

Structured as **observations → the lever that addresses it**. Nothing here is a recommendation to change yet; it's the menu for the re-eval conversation.

## 2.1 Tensions worth a decision

1. **Scarce currency has no ladder.** Level is driven by reasoning XP *only*. A prolific coalition-builder can accumulate unbounded prestige while their *level* is capped by the same 150/day grind as everyone else. Is prestige meant to be entirely off the level ladder, or should scarce points feed a second visible track (a "prestige rank")? Right now scarce shows only as a number tile.

2. **Top-end compression (known soft spot).** Once everyone completes their quests, all carry the same 90 base; further separation comes only from the 1-XP/act tail. The most engaged players barely separate from the merely-consistent. Levers: raise the per-act floor, flatten the diminishing curve, or add quest *tiers* / rotating harder quests.

3. **Casual progression is very slow.** At ~12 XP/day a casual player needs ~42 days to reach Level 2. That's a long time to see the first tier change — the exact window where churn is highest. Levers: front-load early levels (variable `XP_PER_LEVEL`, cheap first rungs), or surface Circle/milestone progress more prominently for players who won't level fast.

4. **The level formula lives in the frontend.** `XP_PER_LEVEL`, tier names, and the level calc are all in `PlayerHome.tsx`. No backend authority means no server-side gating on level, easy display drift across clients, and a second place to change if the curve is retuned. Consider moving level derivation server-side (or into a shared config the backend owns).

5. **Two ladders, unclear relationship.** Level (personal, XP) and Circle (cohort, skill) deliberately share no names — good for avoiding confusion, but is the *relationship* legible? Does a player understand that leveling ≠ Circle promotion and that they advance on different signals?

6. **Debate vs Civic divergence.** Two XP systems with different philosophies (event-sourced/anti-abuse vs linear recompute/none). Decide: unify, retire the Debate one, or document them as intentionally separate products.

## 2.2 The knobs (and what each one moves)

| Knob | Current | Turning it up… | Turning it down… |
|---|---|---|---|
| `DailyReasoningCap` | 150 | more room for grinders to separate; more farm surface | tighter ceiling, more compression |
| `DiminishingFactor` | 0.8 | raw acting stays valuable longer (rewards volume) | acts decay faster, quests dominate more |
| Per-act floor | 1 | top-end spreads via the tail | — |
| `XP_PER_LEVEL` | 500 (flat) | slower ladder, tiers feel earned | faster early wins, more dopamine, risk of trivializing |
| Quest count / rewards | 4 × (10/20/30/30) = 90 | more/rotating quests add variety + top-end spread | — |
| Quality-gate floor | 0.2 | — | harsher penalty on low-effort premium acts |
| Streaks | none (gentle by design) | strongest known return mechanic | (staying off protects the reflective/minor audience — a real fork, see doc 04 §Streaks) |

## 2.3 Open questions for the re-eval

- Should **scarce points** contribute to level (or a parallel prestige rank), or stay a pure side tile?
- Is the **10-tier level ladder** the right length? Engaged players hit Speaker in ~45 days; casuals may never see it.
- Should **quests rotate / scale** with Circle tier, so the daily 90 isn't identical for a Citizen and a Founder?
- Is a **gentle weekly-cadence streak** worth revisiting, or is the no-streak stance settled?
- Do we want **server-authoritative levels**, or is client-derived acceptable given levels gate nothing today?
- **Debate `User.Xp`**: unify, retire, or keep separate — and if kept, does it need any anti-abuse at all?

---

*Code references: `backend-civic/Services/Coalition/Product/{CoalitionPoints,ReasoningLedger,CoalitionLoopService}.cs`, `backend-civic/Services/Coalition/Curriculum/{DifficultyLadder,CampaignProgression,CircleComposition}.cs`, `frontend-civic/src/prototypes/magazine/pages/PlayerHome.tsx`; Debate: `backend/Controllers/Api/ProfileController.cs`, `backend/Models/User.cs`.*
