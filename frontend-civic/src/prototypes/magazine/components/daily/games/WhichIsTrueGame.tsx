import { useState } from "react";
import { Link } from "react-router-dom";
import { Check, X } from "lucide-react";
import { cn } from "@/lib/cn";
import {
  submitDailyPlay,
  type DailyPuzzle,
  type DailyResult,
  type WhichIsTruePayload,
} from "@/api/daily";

/**
 * Which Is True — a question and two figures, one of which answers it.
 *
 * Both numbers on the card are real; the loser is another true figure from the same
 * family (a different state, a different bracket, a different bill). The reveal has to
 * say so on every round — "the other one is Ohio's" is the thing worth remembering, and
 * without it a wrong guess teaches nothing.
 */
export function WhichIsTrueGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as WhichIsTruePayload;
  const [index, setIndex] = useState(0);
  const [picks, setPicks] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const round = payload.rounds[index];
  const last = index === payload.rounds.length - 1;

  const choose = async (pick: "A" | "B") => {
    if (busy || result) return;
    const next = [...picks, pick];
    setPicks(next);

    if (!last) {
      setIndex((i) => i + 1);
      return;
    }

    setBusy(true);
    setError(null);
    try {
      onResult(await submitDailyPlay("WhichIsTrue", { picks: next }));
    } catch {
      // Roll the optimistic pick back so the last round is re-answerable rather than
      // leaving the player on a dead end.
      setPicks(picks);
      setError("Couldn't submit your answers. Try again.");
    } finally {
      setBusy(false);
    }
  };

  if (result) {
    const correctCount = result.rounds.filter((r) => r.band === "hit").length;
    return (
      <div data-testid="whichistrue-result">
        <h1 className="display text-3xl">
          {correctCount}
          <span className="text-xl text-[var(--muted)]">/{payload.rounds.length}</span>
        </h1>
        <p className="mt-2 text-sm text-[var(--fg-soft)]">
          Both numbers were real every time — the one you didn't pick just answers a
          different question.
        </p>

        <ul className="mt-6 space-y-4">
          {payload.rounds.map((r, i) => {
            const reveal = result.reveal?.rounds?.[i];
            const correct = reveal?.correct as "A" | "B" | undefined;
            const got = result.rounds[i]?.band === "hit";
            const truth = correct === "B" ? r.optionB : r.optionA;
            return (
              <li
                key={i}
                className="border border-[var(--border)] bg-[var(--bg)] p-4"
                data-testid={`whichistrue-reveal-${i}`}
              >
                <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                  {r.topic}
                </p>
                <p className="mt-1 text-sm">{r.prompt}</p>
                <p className="mt-2 flex items-center gap-1.5 text-sm">
                  {got ? (
                    <Check size={14} className="text-emerald-600" />
                  ) : (
                    <X size={14} className="text-rose-600" />
                  )}
                  <strong>{truth}</strong>
                </p>
                {reveal?.explanation && (
                  <p className="mt-2 text-sm text-[var(--fg-soft)]">{reveal.explanation}</p>
                )}
                {reveal?.decoyTruth && (
                  <p className="mt-1 text-sm text-[var(--muted)]">
                    The other one? {reveal.decoyTruth}
                  </p>
                )}
                {reveal?.source && (
                  <p className="mt-2 text-xs text-[var(--muted)]">
                    {reveal.sourceUrl ? (
                      <a
                        href={reveal.sourceUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="underline"
                      >
                        {reveal.source}
                      </a>
                    ) : (
                      reveal.source
                    )}
                    {reveal.asOf ? ` · as of ${reveal.asOf}` : ""}
                  </p>
                )}
                {reveal?.billId && (
                  <Link
                    to={`/bills/${reveal.billId}`}
                    className="mt-2 inline-block text-xs font-semibold text-[var(--accent)] hover:underline"
                    data-testid={`whichistrue-bill-${i}`}
                  >
                    See the bill →
                  </Link>
                )}
              </li>
            );
          })}
        </ul>
      </div>
    );
  }

  return (
    <div data-testid="whichistrue-game">
      <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        {round.topic} · {index + 1} of {payload.rounds.length}
      </p>
      <h1 className="display mt-3 text-2xl leading-snug">{round.prompt}</h1>
      <p className="mt-4 text-sm font-semibold">Which one is true?</p>

      <div className="mt-4 space-y-3">
        {(["A", "B"] as const).map((key) => (
          <button
            key={key}
            type="button"
            disabled={busy}
            onClick={() => void choose(key)}
            className={cn(
              "w-full border-2 border-[var(--border)] bg-[var(--bg)] px-4 py-4 text-left transition",
              "enabled:hover:border-[var(--accent)] disabled:opacity-60",
            )}
            data-testid={`whichistrue-option-${key}`}
          >
            <span className="display text-2xl">{key === "A" ? round.optionA : round.optionB}</span>
          </button>
        ))}
      </div>

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="whichistrue-error">
          {error}
        </p>
      )}
    </div>
  );
}
