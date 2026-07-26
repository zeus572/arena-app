import { describe, expect, it } from "vitest";
import {
  buildFeed,
  createMixerState,
  initialMixerState,
  type MixerState,
  type ShortsPools,
} from "./shortsFeed";

// Minimal pool fixtures — only the fields the mixer reads (id, and the keys it dereferences).
const fact = (id: string) => ({ id }) as ShortsPools["budget"][number];
const briefing = (id: string) => ({ id }) as ShortsPools["news"][number];
const provision = (id: string) => ({ id }) as ShortsPools["coalition"][number];
const daily = (id: string) => ({ id }) as ShortsPools["daily"][number];

function pools(over: Partial<ShortsPools> = {}): ShortsPools {
  return { coalition: [], thinkDeeper: [], news: [], budget: [], daily: [], ...over };
}

const facts = (n: number) => Array.from({ length: n }, (_, i) => fact(`b${i}`));
const dailies = (n: number) => Array.from({ length: n }, (_, i) => daily(`d${i}`));

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

  it("emits nothing daily-shaped when there are no daily games", () => {
    // Guards the pre-existing sequences above: an empty daily pool must not perturb them.
    const p = pools({ budget: facts(6), thinkDeeper: [briefing("t1")] });
    const kinds = buildFeed(p, { ...initialMixerState }).map((x) => x.kind);
    expect(kinds).not.toContain("daily");
  });
});

describe("shorts mixer — daily games scattered through the feed", () => {
  it("places every daily game exactly once, with a stable key", () => {
    const p = pools({ budget: facts(30), daily: dailies(4) });
    const out = buildFeed(p, createMixerState(42));

    const keys = out.filter((x) => x.kind === "daily").map((x) => x.key);
    expect(keys).toHaveLength(4);
    expect(new Set(keys).size).toBe(4);
    expect(keys).toContain("dl-d0");
  });

  it("never places two daily cards back to back", () => {
    // The scatter must not clump — a run of game cards reads as an ad break.
    for (let seed = 0; seed < 40; seed++) {
      const p = pools({ budget: facts(40), daily: dailies(6) });
      const kinds = buildFeed(p, createMixerState(seed)).map((x) => x.kind);
      for (let i = 1; i < kinds.length; i++) {
        if (kinds[i] === "daily" && kinds[i - 1] === "daily") {
          throw new Error(`seed ${seed}: adjacent daily cards at index ${i}`);
        }
      }
    }
  });

  it("keeps daily cards within a bounded gap rather than clumping at the start or end", () => {
    const p = pools({ budget: facts(40), daily: dailies(5) });
    const kinds = buildFeed(p, createMixerState(7)).map((x) => x.kind);
    const at = kinds.flatMap((k, i) => (k === "daily" ? [i] : []));

    expect(at).toHaveLength(5);
    // Gap is drawn from [0, 6), so consecutive placements sit at most 7 cards apart.
    for (let i = 1; i < at.length; i++) {
      expect(at[i] - at[i - 1]).toBeLessThanOrEqual(7);
    }
  });

  it("scatters differently for different seeds", () => {
    const positions = (seed: number) => {
      const p = pools({ budget: facts(40), daily: dailies(5) });
      return buildFeed(p, createMixerState(seed))
        .flatMap((x, i) => (x.kind === "daily" ? [i] : []))
        .join(",");
    };
    // Not a strict guarantee for any given pair, but across this many seeds a fixed
    // layout would be obvious.
    const layouts = new Set([1, 2, 3, 4, 5, 6, 7, 8].map(positions));
    expect(layouts.size).toBeGreaterThan(1);
  });

  it("is stable for a given seed, so paging never reshuffles what's already on screen", () => {
    const run = () => {
      const p = pools({ budget: facts(20), daily: dailies(3) });
      return buildFeed(p, createMixerState(99)).map((x) => x.key).join("|");
    };
    expect(run()).toBe(run());
  });

  it("carries the scatter across paginated appends without repeating a game", () => {
    const p = pools({ budget: facts(8), daily: dailies(3) });
    const state = createMixerState(5);

    const first = buildFeed(p, state, { flushFillers: false });
    p.budget.push(...Array.from({ length: 8 }, (_, i) => fact(`b2-${i}`)));
    const second = buildFeed(p, state, { flushFillers: true });

    const keys = [...first, ...second].filter((x) => x.kind === "daily").map((x) => x.key);
    expect(new Set(keys).size).toBe(keys.length);
    expect(keys).toHaveLength(3);
  });

  it("surfaces remaining games at the tail rather than dropping them on a short feed", () => {
    // Two facts can't absorb four games at a 0-6 gap; the leftovers must still appear.
    const p = pools({ budget: facts(2), daily: dailies(4) });
    const kinds = buildFeed(p, createMixerState(3)).map((x) => x.kind);
    expect(kinds.filter((k) => k === "daily")).toHaveLength(4);
  });

  it("shows a daily-only feed when there is no other content at all", () => {
    const p = pools({ daily: dailies(2) });
    const out = buildFeed(p, createMixerState(11));
    expect(out.map((x) => x.kind)).toEqual(["daily", "daily"]);
  });
});
