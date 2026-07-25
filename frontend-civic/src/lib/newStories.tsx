import {
  createContext,
  useCallback,
  useContext,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { getBriefingsLatest, type BriefingLatest } from "@/api/briefings";
import { useVisibilityPolling } from "./useVisibilityPolling";

// Check for new stories every 2 minutes while the tab is active. Briefings arrive on
// the news pipeline's cadence (minutes-to-hours), so a tight interval buys nothing.
const POLL_INTERVAL_MS = 120_000;
// Stop polling after 10 minutes with no interaction, even in a foreground tab.
const IDLE_TIMEOUT_MS = 10 * 60_000;

/**
 * How many stories are new relative to the baseline the reader has already seen.
 *
 * `latestCreatedAt` is the authoritative "is there something newer" signal; the count
 * is derived from the total delta. If the newest timestamp advanced but the total
 * didn't grow (e.g. a story was replaced), we still surface a single "new story" so the
 * reader knows the feed moved. Returns 0 when nothing is newer.
 */
export function computeNewCount(
  baseline: BriefingLatest | null,
  latest: BriefingLatest,
): number {
  if (!baseline) return 0;
  if (!latest.latestCreatedAt) return 0;
  // No prior timestamp but stories now exist — treat all of them as new.
  if (!baseline.latestCreatedAt) return Math.max(0, latest.total);
  if (latest.latestCreatedAt <= baseline.latestCreatedAt) return 0;
  const delta = latest.total - baseline.total;
  return delta > 0 ? delta : 1;
}

type NewStoriesValue = {
  /** Stories published since the reader's baseline. 0 ⇒ hide the notification. */
  newCount: number;
  /** Bumped by {@link refresh}; the feed reloads whenever it changes. */
  feedVersion: number;
  /** Acknowledge the new stories: reset the baseline and reload the feed. */
  refresh: () => void;
  /** Mark the new stories as seen without reloading the feed (a "not now"). */
  dismiss: () => void;
};

const NewStoriesContext = createContext<NewStoriesValue>({
  newCount: 0,
  feedVersion: 0,
  refresh: () => {},
  dismiss: () => {},
});

// eslint-disable-next-line react-refresh/only-export-components
export function useNewStories(): NewStoriesValue {
  return useContext(NewStoriesContext);
}

/**
 * Polls the cheap briefings signal (only while the tab is active — see
 * {@link useVisibilityPolling}) and tracks whether new stories have landed since the
 * reader last caught up. The banner in the layout reads `newCount`; the feed reads
 * `feedVersion` to know when to reload after a refresh.
 */
export function NewStoriesProvider({ children }: { children: ReactNode }) {
  const [newCount, setNewCount] = useState(0);
  const [feedVersion, setFeedVersion] = useState(0);
  // What the reader has already seen, and the most recent poll result.
  const baselineRef = useRef<BriefingLatest | null>(null);
  const latestRef = useRef<BriefingLatest | null>(null);

  const poll = useCallback(async () => {
    let latest: BriefingLatest;
    try {
      latest = await getBriefingsLatest();
    } catch {
      return; // transient failure — try again on the next tick
    }
    latestRef.current = latest;
    // First successful poll establishes the baseline without notifying: the reader
    // hasn't "missed" anything that was already there when they arrived.
    if (!baselineRef.current) {
      baselineRef.current = latest;
      return;
    }
    setNewCount(computeNewCount(baselineRef.current, latest));
  }, []);

  useVisibilityPolling(() => void poll(), POLL_INTERVAL_MS, {
    immediate: true,
    idleTimeoutMs: IDLE_TIMEOUT_MS,
  });

  // Move the baseline up to the latest poll so the notification clears and won't
  // re-fire for stories the reader has now accounted for.
  const catchUp = useCallback(() => {
    if (latestRef.current) baselineRef.current = latestRef.current;
    setNewCount(0);
  }, []);

  const refresh = useCallback(() => {
    catchUp();
    setFeedVersion((v) => v + 1); // reload the feed to show the new stories
  }, [catchUp]);

  return (
    <NewStoriesContext.Provider
      value={{ newCount, feedVersion, refresh, dismiss: catchUp }}
    >
      {children}
    </NewStoriesContext.Provider>
  );
}
