import { describe, expect, it } from "vitest";
import {
  buildFeed,
  initialMixerState,
  type MixerState,
  type ShortsPools,
} from "./shortsFeed";

// Minimal pool fixtures — only the fields the mixer reads (id, and the keys it dereferences).
const post = (id: string) => ({ id }) as ShortsPools["posts"][number];
const fact = (id: string) => ({ id }) as ShortsPools["budget"][number];
const briefing = (id: string) => ({ id }) as ShortsPools["news"][number];
const provision = (id: string) => ({ id }) as ShortsPools["coalition"][number];

function pools(over: Partial<ShortsPools> = {}): ShortsPools {
  return { posts: [], coalition: [], thinkDeeper: [], news: [], budget: [], ...over };
}

describe("shorts mixer — facts lead", () => {
  it("emits a fact (budget/news) at every interstitial before any filler, until facts drain", () => {
    const p = pools({
      budget: [fact("b1"), fact("b2")],
      news: [briefing("n1")],
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const state: MixerState = { ...initialMixerState };
    // 8 posts → an interstitial after every 2 posts → 4 interstitials.
    const feed = buildFeed([...Array(8)].map((_, i) => post(`p${i}`)), p, state);

    const interstitials = feed.filter((x) => x.kind !== "post").map((x) => x.kind);
    // First three interstitials are the three available facts (budget, news, budget);
    // only after facts are spent does a filler appear.
    expect(interstitials).toEqual(["budget", "news", "budget", "thinkDeeper"]);
  });

  it("falls back to coalition/think-deeper when there are no facts at all", () => {
    const p = pools({
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const state: MixerState = { ...initialMixerState };
    const feed = buildFeed([...Array(4)].map((_, i) => post(`p${i}`)), p, state);
    const interstitials = feed.filter((x) => x.kind !== "post").map((x) => x.kind);
    expect(interstitials).toEqual(["thinkDeeper", "coalition"]);
  });

  it("carries state across paginated appends without repeating a fact", () => {
    const p = pools({ budget: [fact("b1"), fact("b2")] });
    const state: MixerState = { ...initialMixerState };
    const first = buildFeed([post("p0"), post("p1")], p, state); // 1 interstitial → b1
    const second = buildFeed([post("p2"), post("p3")], p, state); // next interstitial → b2
    const firstFact = first.find((x) => x.kind === "budget");
    const secondFact = second.find((x) => x.kind === "budget");
    expect(firstFact?.key).toBe("bf-b1");
    expect(secondFact?.key).toBe("bf-b2");
  });
});
