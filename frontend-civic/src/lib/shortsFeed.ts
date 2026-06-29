import type { CampaignPost } from "@/api/campaign";
import type { ProvisionSummary } from "@/api/coalition";
import type { BudgetFact } from "@/api/budgetFacts";
import type { CivicBriefingSummary } from "@/api/types";

/**
 * One card in the Shorts feed. The feed mixes short content from a breadth of
 * synthesized civic sources; each kind carries its own payload and is rendered by
 * the matching sub-card (see components/shorts/ShortCard).
 */
export type ShortItem =
  | { kind: "post"; key: string; post: CampaignPost }
  | { kind: "coalition"; key: string; provision: ProvisionSummary }
  | { kind: "thinkDeeper"; key: string; briefing: CivicBriefingSummary }
  | { kind: "news"; key: string; briefing: CivicBriefingSummary }
  | { kind: "budget"; key: string; fact: BudgetFact };

/** The finite "interstitial" pools sprinkled between campaign posts. */
export type ShortsPools = {
  posts: CampaignPost[];
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
  /** Fact interstitials emitted (budget/news), driving the alternation between them. */
  factCount: number;
  /** Filler interstitials emitted (think-deeper/coalition), driving their alternation. */
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

/** Inject one interstitial after every N posts. Facts-first, so this also sets the fact cadence. */
const POSTS_PER_INTERSTITIAL = 2;

/**
 * Facts (budget + news) LEAD the interstitials: a fact is emitted whenever one is available,
 * alternating budget/news so a long run isn't all one kind. Only once both fact pools are
 * drained do the fillers (think-deeper, coalition) take the slot.
 */
const FACT_ROTATION = ["budget", "news"] as const;
const FILLER_ROTATION = ["thinkDeeper", "coalition"] as const;

/**
 * Pull the next interstitial item, advancing `state`. Facts are exhausted before any filler
 * appears (the "facts lead" rule); within each group the rotation alternates and skips a
 * drained pool rather than leaving a gap. Returns null when every finite pool is drained.
 * Mutates `state` in place.
 */
function nextInterstitial(pools: ShortsPools, state: MixerState): ShortItem | null {
  // 1) Facts lead — budget then news, alternating, until both are spent.
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
  // 2) Fillers fill the remaining slots — think-deeper then coalition, alternating.
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
 * Interleave a batch of campaign posts with the finite interstitial pools. Pure
 * apart from advancing `state`, which the caller carries forward across paginated
 * appends so the rotation continues without repeats. Posts are the infinite spine;
 * one interstitial — facts first — is woven in after every {@link POSTS_PER_INTERSTITIAL} posts.
 */
export function buildFeed(
  posts: CampaignPost[],
  pools: ShortsPools,
  state: MixerState,
): ShortItem[] {
  const out: ShortItem[] = [];
  posts.forEach((post, i) => {
    out.push({ kind: "post", key: `post-${post.id}`, post });
    if ((i + 1) % POSTS_PER_INTERSTITIAL === 0) {
      const item = nextInterstitial(pools, state);
      if (item) out.push(item);
    }
  });
  return out;
}
