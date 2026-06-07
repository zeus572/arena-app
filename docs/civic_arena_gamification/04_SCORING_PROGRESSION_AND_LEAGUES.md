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
