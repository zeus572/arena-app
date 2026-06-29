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
  | { kind: "budget"; key: string; fact: BudgetFact };

/** The finite "interstitial" pools sprinkled between campaign posts. */
export type ShortsPools = {
  posts: CampaignPost[];
  coalition: ProvisionSummary[];
  thinkDeeper: CivicBriefingSummary[];
  budget: BudgetFact[];
};

/** Bookkeeping threaded across appends so the rotation never repeats an item. */
export type MixerState = {
  /** Next index to read from each finite pool. */
  coalitionAt: number;
  thinkDeeperAt: number;
  budgetAt: number;
  /** How many interstitials have been emitted (drives the round-robin order). */
  interstitialCount: number;
};

export const initialMixerState: MixerState = {
  coalitionAt: 0,
  thinkDeeperAt: 0,
  budgetAt: 0,
  interstitialCount: 0,
};

/** Inject one interstitial after every N posts. */
const POSTS_PER_INTERSTITIAL = 3;

/** Round-robin order for interstitials — think-deeper first, then budget, then coalition. */
const ROTATION = ["thinkDeeper", "budget", "coalition"] as const;
type Interstitial = (typeof ROTATION)[number];

/**
 * Pull the next available interstitial item, advancing `state`. Starts at the
 * rotation slot implied by how many interstitials we've already emitted, then walks
 * the rotation so an exhausted pool is skipped rather than leaving a gap. Returns
 * null when every finite pool is drained. Mutates `state` in place.
 */
function nextInterstitial(pools: ShortsPools, state: MixerState): ShortItem | null {
  for (let probe = 0; probe < ROTATION.length; probe++) {
    const slot: Interstitial = ROTATION[(state.interstitialCount + probe) % ROTATION.length];
    if (slot === "thinkDeeper" && state.thinkDeeperAt < pools.thinkDeeper.length) {
      const briefing = pools.thinkDeeper[state.thinkDeeperAt++];
      state.interstitialCount++;
      return { kind: "thinkDeeper", key: `td-${briefing.id}`, briefing };
    }
    if (slot === "budget" && state.budgetAt < pools.budget.length) {
      const fact = pools.budget[state.budgetAt++];
      state.interstitialCount++;
      return { kind: "budget", key: `bf-${fact.id}`, fact };
    }
    if (slot === "coalition" && state.coalitionAt < pools.coalition.length) {
      const provision = pools.coalition[state.coalitionAt++];
      state.interstitialCount++;
      return { kind: "coalition", key: `co-${provision.id}`, provision };
    }
  }
  return null;
}

/**
 * Interleave a batch of campaign posts with the finite interstitial pools. Pure
 * apart from advancing `state`, which the caller carries forward across paginated
 * appends so the rotation continues without repeats. Posts are the infinite spine;
 * one interstitial is woven in after every {@link POSTS_PER_INTERSTITIAL} posts.
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
