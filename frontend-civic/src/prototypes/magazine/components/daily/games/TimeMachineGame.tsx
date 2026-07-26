import { useState } from "react";
import { ChevronDown, ChevronUp } from "lucide-react";
import {
  submitDailyPlay,
  type DailyPuzzle,
  type DailyResult,
  type TimeMachinePayload,
} from "@/api/daily";
import { Button } from "../../Button";

/**
 * Time Machine — real headlines, wrong order.
 *
 * Every headline here is real and carries a publisher, a date and a URL. We never
 * fabricate one: the "spot the fake news" version of this game would mean generating
 * plausible misinformation, and sorting real headlines gets the same media-literacy
 * beat without it.
 *
 * Reordering is up/down buttons rather than drag-and-drop — keyboard-reachable, touch
 * friendly, and it doesn't need a drag library.
 */
export function TimeMachineGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as TimeMachinePayload;
  const [order, setOrder] = useState<string[]>(() => payload.items.map((i) => i.id));
  const [pick, setPick] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const byId = Object.fromEntries(payload.items.map((i) => [i.id, i]));
  const isSort = payload.mode === "sort";

  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= order.length) return;
    setOrder((o) => {
      const next = [...o];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  };

  const submit = async () => {
    if (busy || result) return;
    setBusy(true);
    setError(null);
    try {
      onResult(await submitDailyPlay("TimeMachine", isSort ? { order } : { pick }));
    } catch {
      setError("Couldn't submit that. Try again.");
    } finally {
      setBusy(false);
    }
  };

  const dates = result?.reveal?.dates as Record<string, string> | undefined;
  const trueOrder = result?.reveal?.trueOrder as string[] | undefined;
  const displayOrder = result && trueOrder ? trueOrder : order;

  return (
    <div data-testid="timemachine-game">
      <h1 className="display text-2xl leading-snug">
        {isSort ? "Put these in order, oldest first" : "Which one is from this week?"}
      </h1>
      <p className="mt-2 text-sm text-[var(--fg-soft)]">
        Every headline below actually ran.
      </p>

      <ul className="mt-6 space-y-2">
        {displayOrder.map((id, index) => {
          const item = byId[id];
          if (!item) return null;
          return (
            <li
              key={id}
              className={`flex items-start gap-3 border-2 p-3 ${
                !isSort && pick === id
                  ? "border-[var(--accent)] bg-[var(--accent)]/5"
                  : "border-[var(--border)] bg-[var(--bg)]"
              }`}
              data-testid={`timemachine-item-${index}`}
            >
              <div className="flex-1">
                {isSort || result ? (
                  <p className="text-sm font-medium">{item.headline}</p>
                ) : (
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => setPick(id)}
                    className="text-left text-sm font-medium"
                    data-testid={`timemachine-pick-${index}`}
                  >
                    {item.headline}
                  </button>
                )}
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {item.publisher}
                  {dates?.[id] ? ` · ${dates[id]}` : ""}
                </p>
                {result && payload.urls?.[id] && (
                  <p className="mt-1 text-xs">
                    <a
                      href={payload.urls[id]}
                      target="_blank"
                      rel="noreferrer"
                      className="text-[var(--accent)] underline"
                    >
                      Read it
                    </a>
                  </p>
                )}
              </div>

              {isSort && !result && (
                <div className="flex flex-col gap-1">
                  <button
                    type="button"
                    onClick={() => move(index, -1)}
                    disabled={index === 0 || busy}
                    className="border border-[var(--border)] p-1 disabled:opacity-30"
                    aria-label="Move earlier"
                    data-testid={`timemachine-up-${index}`}
                  >
                    <ChevronUp size={14} />
                  </button>
                  <button
                    type="button"
                    onClick={() => move(index, 1)}
                    disabled={index === order.length - 1 || busy}
                    className="border border-[var(--border)] p-1 disabled:opacity-30"
                    aria-label="Move later"
                    data-testid={`timemachine-down-${index}`}
                  >
                    <ChevronDown size={14} />
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="timemachine-error">
          {error}
        </p>
      )}

      {result ? (
        <div className="mt-6 border-t border-[var(--border)] pt-6" data-testid="timemachine-result">
          <p className="text-sm">
            <strong>{result.score}</strong>/100
          </p>
          <p className="mt-2 text-sm text-[var(--fg-soft)]">{result.reveal?.revealLine}</p>
        </div>
      ) : (
        <Button
          fullWidth
          disabled={busy || (!isSort && !pick)}
          onClick={() => void submit()}
          className="mt-6"
          data-testid="timemachine-submit"
        >
          {isSort ? "Lock in this order" : "Lock in my pick"}
        </Button>
      )}
    </div>
  );
}
