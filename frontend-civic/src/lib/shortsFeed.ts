import type { ProvisionSummary } from "@/api/coalition";
import type { BudgetFact } from "@/api/budgetFacts";
import type { CivicBriefingSummary } from "@/api/types";
import type { DailyPuzzle } from "@/api/daily";

/**
 * One card in the Shorts feed. The feed leads with interesting civic facts (budget +
 * news) and weaves in reflective content (think-deeper prompts, coalition provisions)
 * plus today's unplayed daily games. Candidate campaign posts are intentionally NOT part
 * of Shorts — they're only relevant to Campaign Managers and live in the campaign
 * surfaces instead.
 */
export type ShortItem =
  | { kind: "coalition"; key: string; provision: ProvisionSummary }
  | { kind: "thinkDeeper"; key: string; briefing: CivicBriefingSummary }
  | { kind: "news"; key: string; briefing: CivicBriefingSummary }
  | { kind: "budget"; key: string; fact: BudgetFact }
  | { kind: "daily"; key: string; puzzle: DailyPuzzle };

/** The finite content pools the feed is mixed from. */
export type ShortsPools = {
  coalition: ProvisionSummary[];
  thinkDeeper: CivicBriefingSummary[];
  /** News-sourced briefings (carry an upstream publisher) — surfaced as fact cards. */
  news: CivicBriefingSummary[];
  budget: BudgetFact[];
  /** Today's daily games the caller hasn't finished yet. Scattered, not rotated. */
  daily: DailyPuzzle[];
};

/** Bookkeeping threaded across appends so the rotation never repeats an item. */
export type MixerState = {
  /** Next index to read from each finite pool. */
  coalitionAt: number;
  thinkDeeperAt: number;
  budgetAt: number;
  newsAt: number;
  dailyAt: number;
  /** Facts emitted (budget/news), driving the alternation between them. */
  factCount: number;
  /** Fillers emitted (think-deeper/coalition), driving their alternation. */
  fillerCount: number;
  /** Non-daily cards emitted since the last daily game card. */
  sinceDaily: number;
  /** How many cards to wait before the next daily. -1 = not drawn yet. */
  dailyGap: number;
  /** Mulberry32 state — see {@link createMixerState} for why this is seeded, not Math.random. */
  rng: number;
};

export const initialMixerState: MixerState = {
  coalitionAt: 0,
  thinkDeeperAt: 0,
  budgetAt: 0,
  newsAt: 0,
  dailyAt: 0,
  factCount: 0,
  fillerCount: 0,
  sinceDaily: 0,
  dailyGap: -1,
  // Fixed default so `{...initialMixerState}` stays deterministic for tests.
  rng: 1,
};

/**
 * A mixer state with a randomized daily-card placement seed.
 *
 * The RNG lives in the state rather than being a bare Math.random() call because the feed
 * is paginated: the caller carries one state across appends, and re-running the mixer must
 * not reshuffle where daily cards land in pages already on screen. One seed per session
 * gives a different scatter each visit and a stable one within it.
 */
export function createMixerState(seed?: number): MixerState {
  return {
    ...initialMixerState,
    rng: seed ?? Math.floor(Math.random() * 2 ** 32),
  };
}

/** Mulberry32 — small, fast, seeded. Advances `state.rng`; returns a float in [0, 1). */
function nextRandom(state: MixerState): number {
  state.rng = (state.rng + 0x6d2b79f5) >>> 0;
  let t = state.rng;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

/** Facts are the spine; a reflective/coalition card is woven in after every N facts. */
const FACTS_PER_FILLER = 3;

/** Facts alternate budget/news; fillers alternate think-deeper/coalition. */
const FACT_ROTATION = ["budget", "news"] as const;
const FILLER_ROTATION = ["thinkDeeper", "coalition"] as const;

/**
 * Pull the next fact (budget then news, alternating), advancing `state`. Skips a drained
 * pool rather than leaving a gap; returns null when both fact pools are spent.
 * Mutates `state` in place.
 */
function nextFact(pools: ShortsPools, state: MixerState): ShortItem | null {
  for (let probe = 0; probe < FACT_ROTATION.length; probe++) {
    const slot = FACT_ROTATION[(state.factCount + probe) % FACT_ROTATION.length];
    if (slot === "budget" && state.budgetAt < pools.budget.length) {
      const fact = pools.budget[state.budgetAt++];
      state.factCount++;
      return { kind: "budget", key: `bf-${fact.id}`, fact };
    }
    if (slot === "news" && state.newsAt < pools.news.length) {
      const briefing = pools.news[state.newsAt++];
      state.factCount++;
      return { kind: "news", key: `nw-${briefing.id}`, briefing };
    }
  }
  return null;
}

/**
 * Pull the next reflective filler (think-deeper then coalition, alternating), advancing
 * `state`. Returns null when both filler pools are spent. Mutates `state` in place.
 */
function nextFiller(pools: ShortsPools, state: MixerState): ShortItem | null {
  for (let probe = 0; probe < FILLER_ROTATION.length; probe++) {
    const slot = FILLER_ROTATION[(state.fillerCount + probe) % FILLER_ROTATION.length];
    if (slot === "thinkDeeper" && state.thinkDeeperAt < pools.thinkDeeper.length) {
      const briefing = pools.thinkDeeper[state.thinkDeeperAt++];
      state.fillerCount++;
      return { kind: "thinkDeeper", key: `td-${briefing.id}`, briefing };
    }
    if (slot === "coalition" && state.coalitionAt < pools.coalition.length) {
      const provision = pools.coalition[state.coalitionAt++];
      state.fillerCount++;
      return { kind: "coalition", key: `co-${provision.id}`, provision };
    }
  }
  return null;
}

/**
 * Widest gap between daily-game cards, in other cards. The actual gap is drawn uniformly
 * from [0, DAILY_MAX_GAP) each time, so placement feels scattered rather than metronomic
 * while still staying spread out — a daily card can't land two slots in a row, and can't
 * disappear for a whole page either.
 */
const DAILY_MAX_GAP = 6;

/**
 * Emit the next unplayed daily game if this slot is the one the RNG picked. Mutates
 * `state`. Returns null when the pool is spent or it isn't time yet.
 */
function nextDaily(pools: ShortsPools, state: MixerState): ShortItem | null {
  if (state.dailyAt >= pools.daily.length) return null;
  if (state.dailyGap < 0) state.dailyGap = Math.floor(nextRandom(state) * DAILY_MAX_GAP);
  if (state.sinceDaily < state.dailyGap) return null;

  const puzzle = pools.daily[state.dailyAt++];
  state.sinceDaily = 0;
  state.dailyGap = Math.floor(nextRandom(state) * DAILY_MAX_GAP);
  return { kind: "daily", key: `dl-${puzzle.id}`, puzzle };
}

/**
 * Build a facts-first batch of the Shorts feed. Interesting facts (budget + news, alternating)
 * are the spine; a reflective filler (think-deeper / coalition) is woven in after every
 * {@link FACTS_PER_FILLER} facts, and today's unplayed daily games are scattered through at
 * randomized intervals. Pure apart from advancing `state`, which the caller carries forward
 * across paginated appends so the rotation never repeats an item.
 *
 * `flushFillers` (default true) controls the tail: when this is the last batch it appends any
 * remaining fillers so nothing synthesized is dropped. While more news pages are still coming
 * the caller passes `false`, holding fillers back so they keep weaving between facts instead of
 * clumping between page boundaries.
 */
export function buildFeed(
  pools: ShortsPools,
  state: MixerState,
  opts: { flushFillers?: boolean } = {},
): ShortItem[] {
  const { flushFillers = true } = opts;
  const out: ShortItem[] = [];

  // Every non-daily card counts toward the gap, so a daily can land between a fact and its
  // filler as readily as between two facts.
  const push = (item: ShortItem) => {
    out.push(item);
    state.sinceDaily++;
    const daily = nextDaily(pools, state);
    if (daily) out.push(daily);
  };

  let sinceFiller = 0;
  let fact: ShortItem | null;
  while ((fact = nextFact(pools, state)) !== null) {
    push(fact);
    if (++sinceFiller >= FACTS_PER_FILLER) {
      const filler = nextFiller(pools, state);
      if (filler) {
        push(filler);
        sinceFiller = 0;
      }
    }
  }

  // Facts drained for now — on the final batch, surface any remaining reflective/coalition content.
  if (flushFillers) {
    let filler: ShortItem | null;
    while ((filler = nextFiller(pools, state)) !== null) push(filler);

    // A short feed can end before the scatter has placed every daily. Append the rest
    // rather than dropping them — a game the reader never sees is worse than a clump.
    while (state.dailyAt < pools.daily.length) {
      const puzzle = pools.daily[state.dailyAt++];
      out.push({ kind: "daily", key: `dl-${puzzle.id}`, puzzle });
    }
    state.sinceDaily = 0;
  }

  return out;
}
