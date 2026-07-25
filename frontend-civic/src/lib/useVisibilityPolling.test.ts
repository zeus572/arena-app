import { describe, expect, it } from "vitest";
import { isIdle, pollingActive } from "./useVisibilityPolling";

describe("isIdle", () => {
  it("is never idle when no timeout is configured", () => {
    expect(isIdle(0, 10_000_000, undefined)).toBe(false);
  });

  it("is idle only once the timeout has fully elapsed", () => {
    const last = 1_000;
    expect(isIdle(last, last + 4_999, 5_000)).toBe(false);
    expect(isIdle(last, last + 5_000, 5_000)).toBe(true); // boundary counts as idle
    expect(isIdle(last, last + 9_999, 5_000)).toBe(true);
  });
});

describe("pollingActive", () => {
  it("runs only when enabled, visible, and not idle", () => {
    expect(pollingActive({ enabled: true, visible: true, idle: false })).toBe(true);
  });

  it("stops for a hidden tab regardless of idleness", () => {
    expect(pollingActive({ enabled: true, visible: false, idle: false })).toBe(false);
    expect(pollingActive({ enabled: true, visible: false, idle: true })).toBe(false);
  });

  it("stops when idle even if visible", () => {
    expect(pollingActive({ enabled: true, visible: true, idle: true })).toBe(false);
  });

  it("stops when disabled", () => {
    expect(pollingActive({ enabled: false, visible: true, idle: false })).toBe(false);
  });
});
