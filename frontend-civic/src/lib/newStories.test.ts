import { describe, expect, it } from "vitest";
import { computeNewCount } from "./newStories";
import type { BriefingLatest } from "@/api/briefings";

const at = (iso: string | null, total: number): BriefingLatest => ({
  latestCreatedAt: iso,
  total,
});

describe("computeNewCount", () => {
  it("reports nothing new before a baseline exists", () => {
    expect(computeNewCount(null, at("2026-07-24T00:00:00Z", 10))).toBe(0);
  });

  it("reports nothing new when the newest timestamp hasn't advanced", () => {
    const baseline = at("2026-07-24T00:00:00Z", 10);
    expect(computeNewCount(baseline, at("2026-07-24T00:00:00Z", 10))).toBe(0);
    // An older-or-equal timestamp never counts, even if the total drifted.
    expect(computeNewCount(baseline, at("2026-07-23T00:00:00Z", 12))).toBe(0);
  });

  it("counts the total delta when newer stories have landed", () => {
    const baseline = at("2026-07-24T00:00:00Z", 10);
    expect(computeNewCount(baseline, at("2026-07-24T09:00:00Z", 13))).toBe(3);
  });

  it("surfaces at least one when the feed moved but the total didn't grow", () => {
    // A story replaced (newest timestamp advanced, count unchanged) still notifies.
    const baseline = at("2026-07-24T00:00:00Z", 10);
    expect(computeNewCount(baseline, at("2026-07-24T09:00:00Z", 10))).toBe(1);
  });

  it("treats all stories as new when the baseline had none", () => {
    const empty = at(null, 0);
    expect(computeNewCount(empty, at("2026-07-24T09:00:00Z", 4))).toBe(4);
  });

  it("reports nothing when the feed is (still) empty", () => {
    const baseline = at("2026-07-24T00:00:00Z", 10);
    expect(computeNewCount(baseline, at(null, 0))).toBe(0);
  });
});
