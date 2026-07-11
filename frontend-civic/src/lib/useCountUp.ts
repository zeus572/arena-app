import { useEffect, useRef, useState } from "react";
import { usePrefersReducedMotion } from "./useReducedMotion";

// Animate a number from a starting point up to `target` over `durationMs`,
// easing out so it decelerates into place. Returns the current frame value.
//
// Used for XP counters and the XP ring sweep — the payoff of a level/progress
// screen is watching the number climb, not seeing it already landed.
//
// Honors reduced-motion (returns `target` immediately) and re-animates whenever
// `target` changes, tweening from wherever it currently is so mid-flight target
// changes stay smooth rather than snapping back to zero.

export function easeOutCubic(t: number): number {
  return 1 - Math.pow(1 - t, 3);
}

// The tweened value at `elapsedMs` into a sweep from `start` to `target`.
// Clamps progress to [0,1] so callers past the end sit exactly on target.
// Pure + exported so the animation math is unit-testable without a DOM.
export function tweenFrame(
  start: number,
  target: number,
  elapsedMs: number,
  durationMs: number,
): number {
  if (durationMs <= 0) return target;
  const t = Math.min(1, Math.max(0, elapsedMs / durationMs));
  return start + (target - start) * easeOutCubic(t);
}

type Options = {
  /** Milliseconds for the sweep. Default 900ms. */
  durationMs?: number;
  /** Delay before the sweep begins, e.g. to sequence after a card entrance. */
  delayMs?: number;
  /** Value to start the very first animation from. Default 0. */
  from?: number;
};

export function useCountUp(target: number, options: Options = {}): number {
  const { durationMs = 900, delayMs = 0, from = 0 } = options;
  const prefersReduced = usePrefersReducedMotion();
  const [value, setValue] = useState(prefersReduced ? target : from);
  const valueRef = useRef(value);
  valueRef.current = value;

  useEffect(() => {
    if (prefersReduced) {
      setValue(target);
      return;
    }

    const start = valueRef.current;
    if (start === target) return;

    let rafId = 0;
    let startTs: number | null = null;
    let timeoutId: ReturnType<typeof setTimeout> | undefined;

    const step = (ts: number) => {
      if (startTs === null) startTs = ts;
      const elapsed = ts - startTs;
      setValue(tweenFrame(start, target, elapsed, durationMs));
      if (elapsed < durationMs) {
        rafId = requestAnimationFrame(step);
      } else {
        setValue(target);
      }
    };

    const begin = () => {
      rafId = requestAnimationFrame(step);
    };
    if (delayMs > 0) {
      timeoutId = setTimeout(begin, delayMs);
    } else {
      begin();
    }

    return () => {
      cancelAnimationFrame(rafId);
      if (timeoutId !== undefined) clearTimeout(timeoutId);
    };
    // valueRef is read (not depended on) so a re-trigger tweens from the live
    // value; target/duration/delay are the real inputs.
  }, [target, durationMs, delayMs, prefersReduced]);

  return value;
}
