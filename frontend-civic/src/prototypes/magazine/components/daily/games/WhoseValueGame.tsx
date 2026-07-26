import { useState } from "react";
import { Link } from "react-router-dom";
import { Check, X } from "lucide-react";
import {
  submitDailyPlay,
  type DailyPuzzle,
  type DailyResult,
  type WhoseValuePayload,
} from "@/api/daily";
/**
 * Whose Value — read an argument, name the value it appeals to.
 *
 * Non-partisan by construction: the answer space is the compass axes, never two parties.
 *
 * Careful with the end-card copy — "sharpest on Authority" is a READING result. It says
 * nothing about where the player sits on that axis, and must never be phrased as if the
 * game measured their values.
 */
export function WhoseValueGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as WhoseValuePayload;
  const [index, setIndex] = useState(0);
  const [picks, setPicks] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const round = payload.rounds[index];
  const last = index === payload.rounds.length - 1;

  const choose = async (axisKey: string) => {
    if (busy || result) return;
    const next = [...picks, axisKey];
    setPicks(next);

    if (!last) {
      setIndex((i) => i + 1);
      return;
    }

    setBusy(true);
    setError(null);
    try {
      onResult(await submitDailyPlay("WhoseValue", { picks: next }));
    } catch {
      setPicks(picks);
      setError("Couldn't submit your answers. Try again.");
    } finally {
      setBusy(false);
    }
  };

  if (result) {
    const reveal = result.reveal;
    const correctCount = result.rounds.filter((r) => r.band === "hit").length;
    return (
      <div data-testid="whosevalue-result">
        <h1 className="display text-3xl">
          {correctCount}
          <span className="text-xl text-[var(--muted)]">/{payload.rounds.length}</span>
        </h1>
        {reveal?.sharpestAxisName && (
          <p className="mt-2 text-sm text-[var(--fg-soft)]">
            You read <strong>{reveal.sharpestAxisName}</strong> arguments most reliably.
            That's about spotting the appeal — it says nothing about where you sit on that
            axis.{" "}
            <Link to="/profile" className="text-[var(--accent)] underline">
              Your compass is here.
            </Link>
          </p>
        )}

        <ul className="mt-6 space-y-4">
          {payload.rounds.map((r, i) => {
            const correctKey = reveal?.rounds?.[i]?.correctAxisKey;
            const correctChoice = r.choices.find((c) => c.axisKey === correctKey);
            const got = result.rounds[i]?.band === "hit";
            return (
              <li
                key={i}
                className="border border-[var(--border)] bg-[var(--bg)] p-4"
                data-testid={`whosevalue-reveal-${i}`}
              >
                <p className="text-sm italic">"{r.argument}"</p>
                <p className="mt-2 flex items-center gap-1.5 text-sm">
                  {got ? (
                    <Check size={14} className="text-emerald-600" />
                  ) : (
                    <X size={14} className="text-rose-600" />
                  )}
                  <span>
                    Appeals to <strong>{correctChoice?.name ?? correctKey}</strong>
                  </span>
                </p>
                {reveal?.rounds?.[i]?.billTitle && (
                  <p className="mt-1 text-xs text-[var(--muted)]">
                    From: {reveal.rounds[i].billTitle}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      </div>
    );
  }

  return (
    <div data-testid="whosevalue-game">
      <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        Argument {index + 1} of {payload.rounds.length}
      </p>
      <h1 className="display mt-3 text-2xl leading-snug">"{round.argument}"</h1>
      <p className="mt-4 text-sm font-semibold">Which value is this appealing to?</p>

      <ul className="mt-4 space-y-3">
        {round.choices.map((choice) => (
          <li key={choice.axisKey}>
            <button
              type="button"
              disabled={busy}
              onClick={() => void choose(choice.axisKey)}
              className="w-full border-2 border-[var(--border)] bg-[var(--bg)] px-4 py-3 text-left transition hover:border-[var(--accent)]"
              data-testid={`whosevalue-choice-${choice.axisKey}`}
            >
              <span className="block text-base font-medium">{choice.name}</span>
              <span className="mt-0.5 block text-xs text-[var(--muted)]">
                {choice.lowLabel} ↔ {choice.highLabel}
              </span>
            </button>
          </li>
        ))}
      </ul>

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="whosevalue-error">
          {error}
        </p>
      )}
    </div>
  );
}
