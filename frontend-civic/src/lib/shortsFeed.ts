import type { ProvisionSummary } from "@/api/coalition";
import type { BudgetFact } from "@/api/budgetFacts";
import type { CivicBriefingSummary } from "@/api/types";

/**
 * One card in the Shorts feed. The feed leads with interesting civic facts (budget +
 * news) and weaves in reflective content (think-deeper prompts, coalition provisions).
 * Candidate campaign posts are intentionally NOT part of Shorts — they're only relevant
 * to Campaign Managers and live in the campaign surfaces instead.
 */
export type ShortItem =
  | { kind: "coalition"; key: string; provision: ProvisionSummary }
  | { kind: "thinkDeeper"; key: string; briefing: CivicBriefingSummary }
  | { kind: "news"; key: string; briefing: CivicBriefingSummary }
  | { kind: "budget"; key: string; fact: BudgetFact };

/** The finite content pools the feed is mixed from. */
export type ShortsPools = {
  coalition: ProvisionSummary[];
  thinkDeeper: CivicBriefingSummary[];
  /** News-sourced briefings (carry an upstream publisher) — surfaced as fact cards. */
  news: CivicBriefingSummary[];
  budget: BudgetFact[];
};

/** Bookkeeping threaded across appends so the rotation never repeats an item. */
export type MixerState = {
  /** Next index to read from each finite pool. */
  coalitionAt: number;
  thinkDeeperAt: number;
  budgetAt: number;
  newsAt: number;
  /** Facts emitted (budget/news), driving the alternation between them. */
  factCount: number;
  /** Fillers emitted (think-deeper/coalition), driving their alternation. */
  fillerCount: number;
};

export const initialMixerState: MixerState = {
  coalitionAt: 0,
  thinkDeeperAt: 0,
  budgetAt: 0,
  newsAt: 0,
  factCount: 0,
  fillerCount: 0,
};

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
 * Build a facts-first batch of the Shorts feed. Interesting facts (budget + news, alternating)
 * are the spine; a reflective filler (think-deeper / coalition) is woven in after every
 * {@link FACTS_PER_FILLER} facts. Pure apart from advancing `state`, which the caller carries
 * forward across paginated appends so the rotation never repeats an item.
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

  let sinceFiller = 0;
  let fact: ShortItem | null;
  while ((fact = nextFact(pools, state)) !== null) {
    out.push(fact);
    if (++sinceFiller >= FACTS_PER_FILLER) {
      const filler = nextFiller(pools, state);
      if (filler) {
        out.push(filler);
        sinceFiller = 0;
      }
    }
  }

  // Facts drained for now — on the final batch, surface any remaining reflective/coalition content.
  if (flushFillers) {
    let filler: ShortItem | null;
    while ((filler = nextFiller(pools, state)) !== null) out.push(filler);
  }

  return out;
}
