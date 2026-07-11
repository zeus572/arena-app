import { describe, expect, it } from "vitest";
import { easeOutCubic, tweenFrame } from "./useCountUp";

describe("easeOutCubic", () => {
  it("pins the endpoints", () => {
    expect(easeOutCubic(0)).toBe(0);
    expect(easeOutCubic(1)).toBe(1);
  });

  it("decelerates — is already past halfway at the midpoint", () => {
    // Ease-out front-loads progress, so at t=0.5 the eased value exceeds 0.5.
    expect(easeOutCubic(0.5)).toBeGreaterThan(0.5);
  });
});

describe("tweenFrame", () => {
  it("starts at the start value", () => {
    expect(tweenFrame(0, 500, 0, 900)).toBe(0);
    expect(tweenFrame(120, 500, 0, 900)).toBe(120);
  });

  it("lands exactly on target at (and past) the end", () => {
    expect(tweenFrame(0, 500, 900, 900)).toBe(500);
    expect(tweenFrame(0, 500, 5000, 900)).toBe(500);
  });

  it("clamps a negative elapsed to the start", () => {
    expect(tweenFrame(0, 500, -100, 900)).toBe(0);
  });

  it("is monotonic increasing across the sweep for an increasing target", () => {
    let prev = -Infinity;
    for (let ms = 0; ms <= 900; ms += 90) {
      const v = tweenFrame(0, 500, ms, 900);
      expect(v).toBeGreaterThanOrEqual(prev);
      prev = v;
    }
  });

  it("tweens downward when target is below start (e.g. XP reset)", () => {
    expect(tweenFrame(500, 100, 0, 900)).toBe(500);
    expect(tweenFrame(500, 100, 900, 900)).toBe(100);
    expect(tweenFrame(500, 100, 450, 900)).toBeLessThan(500);
  });

  it("returns target immediately for a zero/negative duration", () => {
    expect(tweenFrame(0, 500, 0, 0)).toBe(500);
    expect(tweenFrame(0, 500, 10, -5)).toBe(500);
  });
});
