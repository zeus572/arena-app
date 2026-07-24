import { useEffect, useRef } from "react";

/**
 * Run `callback` on an interval, but only while the tab is worth polling:
 *
 *  - **Tab visibility** (Page Visibility API) — polling stops the moment the tab is
 *    backgrounded or the window is hidden, and resumes when it comes back to the
 *    foreground. Background tabs never poll.
 *  - **User idleness** (optional) — if `idleTimeoutMs` is set, polling also stops
 *    once there's been no user interaction (mouse / keyboard / touch / scroll) for
 *    that long, and resumes on the next interaction. This keeps a foreground-but-
 *    abandoned tab from polling forever.
 *
 * The idle check is evaluated at tick boundaries (not on every input event), so
 * activity listeners stay cheap — they just stamp a timestamp. This is a general
 * utility; the "new stories" watcher is one caller, but any lightweight foreground
 * poll (unread counts, live status, …) can reuse it.
 *
 * No-ops where `document` is unavailable (SSR / jsdom without a DOM).
 */

/** True once `idleTimeoutMs` has elapsed since the last activity. No timeout ⇒ never idle. */
export function isIdle(
  lastActivityTs: number,
  now: number,
  idleTimeoutMs: number | undefined,
): boolean {
  if (idleTimeoutMs === undefined) return false;
  return now - lastActivityTs >= idleTimeoutMs;
}

/** Whether the interval should be running given the current gating conditions. */
export function pollingActive(opts: {
  enabled: boolean;
  visible: boolean;
  idle: boolean;
}): boolean {
  return opts.enabled && opts.visible && !opts.idle;
}

type Options = {
  /** Fire once immediately whenever polling (re)activates. Default true. */
  immediate?: boolean;
  /** Stop polling after this long with no user interaction; resume on activity. */
  idleTimeoutMs?: number;
  /** Master switch — when false the hook does nothing. Default true. */
  enabled?: boolean;
};

// Coarse-grained signals of "the user is still here". Passive so they never block
// scrolling. visibilitychange is handled separately (it also gates, not just resets idle).
const ACTIVITY_EVENTS: (keyof DocumentEventMap)[] = [
  "mousemove",
  "keydown",
  "pointerdown",
  "scroll",
  "touchstart",
];

export function useVisibilityPolling(
  callback: () => void,
  intervalMs: number,
  options: Options = {},
): void {
  const { immediate = true, idleTimeoutMs, enabled = true } = options;

  // Keep the latest callback without re-arming the interval when its identity changes.
  const savedCallback = useRef(callback);
  savedCallback.current = callback;

  useEffect(() => {
    if (!enabled) return;
    if (typeof document === "undefined") return;

    let intervalId: ReturnType<typeof setInterval> | undefined;
    let lastActivityTs = Date.now();

    const isVisible = () => document.visibilityState === "visible";

    const stop = () => {
      if (intervalId !== undefined) {
        clearInterval(intervalId);
        intervalId = undefined;
      }
    };

    const tick = () => {
      // Went idle since the last tick — stand down until activity wakes us.
      if (isIdle(lastActivityTs, Date.now(), idleTimeoutMs)) {
        stop();
        return;
      }
      savedCallback.current();
    };

    const start = (fireNow: boolean) => {
      if (intervalId !== undefined) return; // already running
      if (fireNow) savedCallback.current();
      intervalId = setInterval(tick, intervalMs);
    };

    // (Re)decide whether the interval should be running right now.
    const evaluate = (fireNow: boolean) => {
      const idle = isIdle(lastActivityTs, Date.now(), idleTimeoutMs);
      if (pollingActive({ enabled: true, visible: isVisible(), idle })) {
        start(fireNow);
      } else {
        stop();
      }
    };

    const onVisibility = () => {
      if (isVisible()) {
        // Returning to the tab counts as activity, and we catch up immediately.
        lastActivityTs = Date.now();
        evaluate(immediate);
      } else {
        stop();
      }
    };

    const onActivity = () => {
      const wasStopped = intervalId === undefined;
      lastActivityTs = Date.now();
      // Only the idle-stop case needs a wake-up; while running we just refresh the stamp.
      if (wasStopped && isVisible()) evaluate(immediate);
    };

    document.addEventListener("visibilitychange", onVisibility);
    if (idleTimeoutMs !== undefined) {
      for (const evt of ACTIVITY_EVENTS) {
        document.addEventListener(evt, onActivity, { passive: true });
      }
    }

    evaluate(immediate);

    return () => {
      stop();
      document.removeEventListener("visibilitychange", onVisibility);
      if (idleTimeoutMs !== undefined) {
        for (const evt of ACTIVITY_EVENTS) {
          document.removeEventListener(evt, onActivity);
        }
      }
    };
  }, [intervalMs, immediate, idleTimeoutMs, enabled]);
}
