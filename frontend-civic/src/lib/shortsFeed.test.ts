import { describe, expect, it } from "vitest";
import {
  buildFeed,
  initialMixerState,
  type MixerState,
  type ShortsPools,
} from "./shortsFeed";

// Minimal pool fixtures — only the fields the mixer reads (id, and the keys it dereferences).
const fact = (id: string) => ({ id }) as ShortsPools["budget"][number];
const briefing = (id: string) => ({ id }) as ShortsPools["news"][number];
const provision = (id: string) => ({ id }) as ShortsPools["coalition"][number];

function pools(over: Partial<ShortsPools> = {}): ShortsPools {
  return { coalition: [], thinkDeeper: [], news: [], budget: [], ...over };
}

describe("shorts mixer — facts lead, no campaign posts", () => {
  it("makes facts the spine (budget/news alternating) and weaves a filler after every 3 facts", () => {
    const p = pools({
      budget: [fact("b1"), fact("b2"), fact("b3"), fact("b4")],
      news: [briefing("n1"), briefing("n2")],
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const state: MixerState = { ...initialMixerState };

    const kinds = buildFeed(p, state).map((x) => x.kind);

    expect(kinds).toEqual([
      "budget",
      "news",
      "budget",
      "thinkDeeper", // filler woven in after the 3rd fact
      "news",
      "budget",
      "budget",
      "coalition", // filler after the next 3 facts
    ]);
  });

  it("appends any leftover fillers once the facts are spent", () => {
    const p = pools({
      budget: [fact("b1")],
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const kinds = buildFeed(p, { ...initialMixerState }).map((x) => x.kind);
    // Only one fact (no filler woven at 3), then both fillers trail.
    expect(kinds).toEqual(["budget", "thinkDeeper", "coalition"]);
  });

  it("falls back to a filler-only feed when there are no facts at all", () => {
    const p = pools({
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const kinds = buildFeed(p, { ...initialMixerState }).map((x) => x.kind);
    expect(kinds).toEqual(["thinkDeeper", "coalition"]);
  });

  it("uses stable, kind-prefixed keys", () => {
    const p = pools({
      budget: [fact("b1")],
      news: [briefing("n1")],
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const byKind = Object.fromEntries(buildFeed(p, { ...initialMixerState }).map((x) => [x.kind, x.key]));
    expect(byKind).toMatchObject({
      budget: "bf-b1",
      news: "nw-n1",
      thinkDeeper: "td-t1",
      coalition: "co-c1",
    });
  });

  it("holds fillers back across pages (flushFillers: false), then flushes on the final page", () => {
    const p = pools({
      budget: [fact("b1")],
      thinkDeeper: [briefing("t1")],
      coalition: [provision("c1")],
    });
    const state: MixerState = { ...initialMixerState };

    // Non-final page: one fact, no filler woven yet (needs 3), and fillers are NOT flushed —
    // so they don't clump at the page boundary before the next page's facts arrive.
    const first = buildFeed(p, state, { flushFillers: false }).map((x) => x.kind);
    expect(first).toEqual(["budget"]);

    // Final page: more facts arrive, then the held-back fillers flush at the very end.
    p.budget.push(fact("b2"), fact("b3"));
    const last = buildFeed(p, state, { flushFillers: true }).map((x) => x.kind);
    expect(last).toEqual(["budget", "budget", "thinkDeeper", "coalition"]);
  });

  it("carries state across appends without repeating a fact", () => {
    const p = pools({ budget: [fact("b1"), fact("b2")] });
    const state: MixerState = { ...initialMixerState };
    const first = buildFeed(p, state).map((x) => x.key);
    p.budget.push(fact("b3"));
    const second = buildFeed(p, state).map((x) => x.key);
    expect(first).toEqual(["bf-b1", "bf-b2"]);
    expect(second).toEqual(["bf-b3"]);
  });
});
