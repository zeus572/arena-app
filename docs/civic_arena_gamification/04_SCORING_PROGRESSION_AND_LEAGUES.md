# 04 — Scoring, Progression & Leagues

## Two clocks

- **Fast clock (the day):** stories arrive, you respond/argue/find ground, you earn. Always open; nothing gates the daily act.
- **Slow clock (the campaign):** synced to the real election calendar (locked architecture: real calendar as environment, not outcome). The daily points are the *flow*; the campaign milestones are the *stock* those flows fill.

The Duolingo trick applied to a civic arc: the daily act is never blocked, but it's visibly feeding something larger. That visible accrual is what makes a Tuesday's small contribution feel like it matters.

## Currencies

- **Daily reliable currency (reasoning XP):** earnable every day, solo, reliably — but **low ceiling, diminishing returns.** A few good contributions cap out the daily grind. This is the baseline that keeps the leaderboard alive on weeks when bridges are rare.
- **Scarce premium currency (coalition / breadth):** rare, high-value, **requires a partner / cross-spectrum reach, uncapped.** This is where prestige and the biggest standing jumps come from.

League standing is mostly reasoning XP; the *prestige markers* and biggest jumps come from coalitions. (Mirrors Duolingo's XP-vs-special-event economy.) This is also the structural guard against volume dominance — showing up daily is necessary but plateaus; climbing requires the rarer cross-spectrum governance moves.

## Macro milestones (what accrues over a campaign)

Layer all three — daily points are the currency, but the *milestones* tell a three-part story:

1. **Legislative record** — the planks your coalitions passed over the cycle, accumulating into a body of work the candidate can stand on. A trophy case that fills (not a gate). By election season the candidate either has a real cross-spectrum governance record or a thin one. The daily act is literally writing your own season's history.
2. **Coalition-breadth meter** — how *wide* a span of the Values spectrum you've successfully built with. Not points — *reach.* Creates a beautiful long-game incentive: late in a campaign the highest-value move is reaching the one corner you've never bridged, because that's what's still empty.
3. **Governance-vs-culture ratio** — trend over the season: are your contributions descending into governance or floating in culture-war? Watching your own ratio move over weeks is the reflective/academic payoff — the kind of evidence a teacher/parent/curious adult can point to.

A passed provision deposits into all three: joins the candidate's **record**, extends each signatory's **breadth meter** (especially for reaching a new corner), and improves the **governance ratio** for everyone who engaged at the governance layer.

## The antagonist (friction) comes for free

In a daily-turn, news-driven model, no artificial rival is needed:
- **The news itself** — some days briefings are bridgeable (bank breadth); some are culturally radioactive (find the narrow governance seam, or recognize a no-bridge day and bank reasoning points).
- **The clock** — the election calendar is the running-out clock; finite daily turns before the campaign closes and your record is what it is.

Scarcity of *days* + variability of *stories* supply all the tension. More elegant and more honest than a bolted-on opponent — governance genuinely is doing what you can with the news you're handed before time runs out.

## Difficulty laddering (by gap width)

**New leagues/groups start on small-distance, bridgeable provisions to hook people; the system widens the gap as the group demonstrates bridging skill.** This is Duolingo difficulty pedagogy ("el gato" before the subjunctive) applied to civics — an early *real* win buys the right to make people struggle later.

### Why this is the most important churn defense
The biggest first-session risk is the **cynicism wall:** a skeptic sees "find common ground across the spectrum," priors say *impossible/naive*, and a polarizing first provision confirms the cynicism → bounce. But a first provision that is **small-gap but real-tradeoff** (e.g., data-center fee), where a cross-spectrum coalition actually assembles *including them*, falsifies the prior with a real experience rather than a slogan. **This is the most persuasive thing the product can do, and it has to happen early or never.**

### Laddering is on gap width, not topic complexity
A provision can be intellectually sophisticated but small-gap (governance framing lands people close), or simple but wide-gap (everyone retreats to tribe). The curriculum sequences by *how far people must travel to coalition*, not policy difficulty.

| Tier | Provisions served | Skill being trained |
|---|---|---|
| **Early / new group** | Small distance — governance framing reliably converges; amendments visibly narrow the gap; coalition reachable in the week | *Recognizing the governance layer exists and that agreement is possible there.* The win is the lesson. |
| **Mid** | Real gap; convergence requires amendment work (can't just co-sign — must find the carve-out) | *The bargaining move* — locating the clause that costs both sides something tolerable. |
| **Advanced** | Wide-gap, genuinely hard; coalition may be unreachable | *Judgment about which gaps are bridgeable at all* — incl. recognizing when NOT to bridge (principled dissent). Protects against naive both-sidesism. |

### Guard: no manufactured success
Easy-to-bridge must mean *the distance is small*, **never** *the stakes are fake*. Even early provisions must carry a real tradeoff. Training wheels on a real bike, not a tricycle. (See doc 01, trap #4.)

### The ladder is per-group, not just per-user
New *leagues* start small → a league has a **collective skill level**; the distance it's served scales with the group's track record of closing gaps. The group levels up *together* — a stronger social bond than individual progression. (Duolingo leagues are parallel strangers; this is a group that collectively unlocks harder civic terrain.)

### Keeping everyone in flow (promotion/relegation)
If a league only gets gradually harder, skilled members outgrow it and get bored; if it jumps too fast the median bounces. Borrow Duolingo's league churn: individuals who demonstrate bridging skill faster than their league **graduate into leagues operating at wider gaps**, so everyone stays near the edge of their ability — gaps wide enough to engage, narrow enough to close. *The distance signal, difficulty ladder, and league-matching are really one system whose job is to keep every player perpetually at a gap they can just barely close.*

## League composition (who's in a league together)

The placement problem — three options:
- **Homogeneous (matched by Values Profile)** — comfortable, high engagement, but builds the echo chamber the Values spec warns against.
- **Maximally mixed** — great for bridging, but loud edges dominate and moderates churn.
- **Structured-diverse (preferred)** — deliberately balanced spectrum, scoring tilted so cross-cutting interaction climbs fastest. Only survives if moderation + scoring make bridging *win*.

**Make the diversity invisible.** Don't tell people "you've been placed in a diverse league to bridge with your enemies" — that framing makes moderates flee. Users just see a league; the system quietly composes it to span the spectrum and tunes scoring so cross-cutting play climbs fastest. Bridging should feel like *discovering a surprising ally*, not completing an assigned reconciliation exercise.

## The bridge / co-sign mechanic (concrete)

The candidate posts a response to a briefing. Users respond as managers / comment. The system knows each user's Values Profile axes. When two users who diverge on the *relevant axis for this story* (locally, per-issue — not globally) produce comments an LLM judge identifies as containing a shared, specific position, it surfaces a **"Bridge Found"** and offers a lightweight async co-op step: *"You and [user] seem to agree that X. Want to co-sign a one-sentence joint statement?"* If both confirm, **both** get the high-value points and the statement is featured.

What this achieves:
- **Async-native** — no live coordination.
- **Makes the other person necessary for your highest score** — the social hook Duolingo lacks (its leagues are parallel-play; you never *need* leaguemates). Here your best move requires a counterpart, ideally an ideological one.
- **Pedagogically exactly the target** — practicing locating real agreement across difference.

**Safety constraint (mixed-age public):** the mechanic only surfaces *public, already-posted* statements for co-signing — **never opens a private channel between two users.** Broadcast-only keeps it safe and inspectable.

## Streaks (philosophical fork — leaning gentle)

Streaks are the strongest return mechanic ever built, but they punish the reflective, occasional, busy user a civics product should respect, and can manufacture compulsive engagement the wellbeing concerns in this space warn against. For a mixed-age public audience including minors, lean toward a **softer "campaign participation" cadence** — rewards consistency over a week without anxiety-inducing all-or-nothing daily breaks — rather than a hard daily streak. (Named as a real fork, not silently defaulted.)

---

# Implementation reference (as built)

This section pins the prose above to the concrete numbers in code, so the economy is auditable without reading the source. When the two disagree, the code wins — keep this section updated alongside it.

**Where it lives**
- `CoalitionPoints.cs` — base points, currency mapping, quality gating, the diminishing curve + daily cap. *Pure, no I/O — the single source of truth for "what an act is worth."*
- `ReasoningLedger.cs` — writes one `CoalitionAct` row per scored action, applies the curve, logs the active day. All XP-earning paths funnel through here.
- `CoalitionLoopService.cs` — `RecordActAsync` (judging + ledger write), `GetQuestsAsync` (daily quests + rewards), `GetMeAsync` (totals shown on the dashboard).
- `frontend-civic/.../PlayerHome.tsx` — level/tier ladder derived from reasoning XP.

## The two currencies

Every `CoalitionAct` is paid in exactly one currency (`CoalitionPoints.Currency`):

| Currency | Earned by | Rules | Drives |
|---|---|---|---|
| **reasoning** | daily micro/mid acts (reactions, positions, steelmans, co-signs, briefing reads, campaign responses, the culture↔governance sort) | **diminishing returns + daily cap** | your **level** (the daily grind / leaderboard floor) |
| **scarce** | macro/coalition acts (`AuthorProvision`, `WritePlank`, `PrincipledDissent`, `Longform`, `CoalitionPassReward`) + the all-quests bonus | **uncapped, no diminishing** | **prestige** (the "Coalition pts" tile; the biggest standing jumps) |

This is the structural guard against volume dominance from the prose above: showing up daily is necessary but plateaus (capped reasoning); climbing requires the rarer cross-spectrum moves (uncapped scarce).

## Act base points (`CoalitionPoints.BasePoints`)

| Act | Base | Currency | Quality-gated? |
|---|---|---|---|
| `BriefingRead` | 2 | reasoning | — |
| `CampaignReaction` | 1 | reasoning | — |
| `CoSign` | 2 | reasoning | — | 
| `ReactionWithReason` | 3 | reasoning | — |
| `ClaimTag` | 3 | reasoning | — |
| `Position` | 5 | reasoning | — |
| `ReactAndRoute` | 5 | reasoning | — |
| `CampaignNewsResponse` | 5 | reasoning | — |
| `CultureGovernanceSort` | 6 | reasoning | — |
| `Steelman` | 8 | reasoning | ✅ |
| `Amend` | 12 | reasoning | — |
| `DiedReasoningPayout` | 4 | reasoning | — |
| `PrincipledDissent` | 20 | scarce | ✅ |
| `Longform` | 20 | scarce | ✅ |
| `AuthorProvision` | 25 | scarce | ✅ |
| `WritePlank` | 30 | scarce | ✅ |
| `CoalitionPassReward` | 30 (+breadth bonus) | scarce | — |
| `QuestReward` | 0 (caller sets the amount) | reasoning¹ | — |

The **agree-vs-amend asymmetry** is encoded here: a bare `CoSign` is worth 2, a substantive `Amend` is worth 12. Mush is cheap; carve-outs pay.

¹ `QuestReward` is reasoning for the per-quest daily rewards, and scarce for the once-per-day "all quests complete" bonus (see below).

**Quality gating** (`QualityGated`): for `Steelman`, `Longform`, `PrincipledDissent`, `WritePlank`, `AuthorProvision`, the base is first scaled by `max(0.2, judgeQuality/100)` — a weak contribution earns as little as 20% of base.

## The reasoning curve (diminishing returns + daily cap)

Reasoning acts are scored by `CoalitionPoints.ApplyDiminishing`:

```
factor    = DiminishingFactor ^ (reasoning acts already done today)   // DiminishingFactor = 0.8
points    = max(1, round(base * factor))                             // never below 1
remaining = max(0, DailyReasoningCap - reasoning earned today)        // DailyReasoningCap = 150
award     = min(points, remaining)                                    // clamp to the cap
```

Two knobs: **`DailyReasoningCap = 150`** (hard daily ceiling) and **`DiminishingFactor = 0.8`** (each successive reasoning act of the day pays 80% of the previous one, with a floor of 1).

Worked example — repeating a base-5 act through one day:

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

So raw acting front-loads (~19 XP over the first six acts) and then pays a flat **1 XP/act** tail — a deliberate anti-farm shape. Pure act-grinding plateaus well under the cap; the cap (150) is really only reachable in combination with quest rewards.

**History / tuning note:** the cap was **30** and the factor **0.6** until 2026-06. That compressed everyone toward the same ~30/day ceiling, so engagement barely differentiated. Raised to **150 / 0.8** to let sustained effort spread scores out while keeping a hard ceiling against runaway farming.

## Daily quests (`GetQuestsAsync`)

Four daily quests, with completion computed **server-side from the acts ledger** (the client owns only routing/subtitle). This is the source of truth — there is no client-side "done" bookkeeping.

| Quest id | Title | Reward | "Done" when, today, there is a … |
|---|---|---|---|
| `briefing-read` | Read today's briefing | 10 | `BriefingRead` act (deduped to once/day) |
| `co-sign` | Co-sign one coalition position | 20 | `CoSign` **or** `Amend` act |
| `campaign-headline` | Respond to a headline in your campaign | 30 | `CampaignNewsResponse` act |
| `bridge-culture` | Bridge a culture-war provision | 30 | `CultureGovernanceSort` **or** `ReactAndRoute` act |

**Quest rewards** are the main XP engine and behave differently from raw acts:
- granted **once per day per quest** (idempotent — a `QuestReward` act keyed by the quest id is the marker, so repeated dashboard loads never double-pay),
- **not** subject to the diminishing curve (granted at full value),
- still **clamped to the daily cap** (`min(reward, 150 - earnedToday)`).

The four rewards total **90 reasoning XP/day**. Because they bypass diminishing, *completing your dailies* — not grinding acts — is how an engaged player approaches the cap. Raw acts then add a diminishing, effort-proportional margin on top.

**All-quests bonus:** finishing all four in a day grants **1 scarce coalition point**, once per day (idempotent via an `all-complete` marker). This is the only scarce point earnable from the daily loop; it backs the "claim a scarce coalition point" promise on the dashboard's quest card.

### Rough daily distribution (with the 150/0.8 curve + quests)

| Engagement | Reasoning XP/day | Composition |
|---|---|---|
| Casual (reads briefing) | ~12 | briefing act + 10 quest |
| Moderate (briefing + co-sign) | ~34 | acts + 30 quests |
| Engaged (all 4 quests) | ~100 | ~10 acts + 90 quests |
| Grinder (all quests + heavy acting) | up to 150 | + diminishing act margin to the ceiling |

Known soft spot: among players who *all* complete their quests (all carry the 90 base), further separation comes only from the 1-XP/act raw tail, so the very top end is gently compressed. Acceptable for now; raise the per-act floor or flatten the curve if more top-end spread is wanted.

## Progression: levels & tiers (`PlayerHome.tsx`)

There is no stored "level" field; it is derived from lifetime reasoning XP:

```
XP_PER_LEVEL = 500
level        = floor(reasoningXp / 500) + 1
```

Each level has a distinct name (`TIER_NAMES`), shown on the progression rail. Levels past 10 fall back to `Level N`:

`1 Voter · 2 Advocate · 3 Organizer · 4 Aspirant · 5 Apprentice · 6 Co-signer · 7 Bridgewright · 8 Statewright · 9 Whip · 10 Speaker`

This level ladder is **separate from the Circle ladder** below and deliberately shares no names, so the dashboard never shows the same tier word twice.

## Circles (skill cohorts) vs the level ladder

A **Circle** is the skill/engagement cohort a player is grouped into (the "league" of the prose above), distinct from an individual's XP level. Circles are composed by skill (`ComposeCirclesAsync`) and **named by their rank in gap-tier order** (`CircleTierLadder` in `CoalitionLoopService.cs`):

`Citizen · Delegate · Framer · Senator · Statesman · Founder` (ascending gap difficulty)

The name is derived from rank at read time, so existing rows show the ladder name without a recompose. Ranks beyond the ladder fall back to `Circle N`.
