import { useState } from "react";
import { Link } from "react-router-dom";
import { ArrowDown, ArrowUp, Check } from "lucide-react";
import {
  submitPlaceItRound,
  type DailyPuzzle,
  type DailyResult,
  type PlaceItPayload,
} from "@/api/daily";
import { Button } from "../../Button";

const BUCKETS = [0, 1, 2, 3, 4];

/**
 * Place It — guess where a real bill sits on three compass axes.
 *
 * IMPORTANT framing: the "right answer" is our LLM synthesis of the bill, not ground
 * truth. The reveal is a comparison, never a verdict — no copy in this component says
 * "wrong" or "incorrect", every axis shows its rationale, and the bill text is one tap
 * away so a player can disagree with the receipt in hand.
 */
export function PlaceItGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as PlaceItPayload;
  const [guesses, setGuesses] = useState<number[]>(() => payload.axes.map(() => 2));
  const [hints, setHints] = useState<string[] | null>(null);
  const [roundsUsed, setRoundsUsed] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (busy || result) return;
    setBusy(true);
    setError(null);
    try {
      const res = await submitPlaceItRound(guesses);
      setHints(res.hints);
      setRoundsUsed(res.roundsUsed);
      if (res.result) onResult(res.result);
    } catch {
      setError("Couldn't record that round. Try again.");
    } finally {
      setBusy(false);
    }
  };

  const revealAxes = result?.reveal?.axes as
    | { axisKey: string; trueBucket: number; rationale: string; evidence: string | null }[]
    | undefined;

  return (
    <div data-testid="placeit-game">
      <h1 className="display text-2xl leading-snug">{payload.billTitle}</h1>
      <p className="mt-1 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        {payload.billStatus}
      </p>
      <p className="mt-3 text-sm text-[var(--fg-soft)]">{payload.billSummary}</p>

      <ul className="mt-8 space-y-6">
        {payload.axes.map((axis, i) => {
          const hint = hints?.[i];
          const truth = revealAxes?.[i];
          return (
            <li key={axis.axisKey} data-testid={`placeit-axis-${axis.axisKey}`}>
              <div className="flex items-center justify-between">
                <span className="text-sm font-semibold">{axis.name}</span>
                {hint && !result && (
                  <span className="flex items-center gap-1 text-xs font-semibold text-[var(--accent)]">
                    {hint === "exact" ? (
                      <>
                        <Check size={13} /> Got it
                      </>
                    ) : hint === "higher" ? (
                      <>
                        <ArrowUp size={13} /> Further right
                      </>
                    ) : (
                      <>
                        <ArrowDown size={13} /> Further left
                      </>
                    )}
                  </span>
                )}
              </div>

              <div className="mt-2 flex gap-1">
                {BUCKETS.map((b) => {
                  const selected = guesses[i] === b;
                  const isTruth = truth?.trueBucket === b;
                  return (
                    <button
                      key={b}
                      type="button"
                      disabled={busy || !!result}
                      onClick={() => setGuesses((g) => g.map((v, j) => (j === i ? b : v)))}
                      className={`h-9 flex-1 border-2 transition disabled:cursor-default ${
                        isTruth
                          ? "border-emerald-500 bg-emerald-50"
                          : selected
                            ? "border-[var(--accent)] bg-[var(--accent)]/10"
                            : "border-[var(--border)] bg-[var(--bg)]"
                      }`}
                      data-testid={`placeit-${axis.axisKey}-${b}`}
                      aria-label={`${axis.name}, position ${b + 1} of 5`}
                    />
                  );
                })}
              </div>
              <div className="mt-1 flex justify-between text-xs text-[var(--muted)]">
                <span>{axis.lowLabel}</span>
                <span>{axis.highLabel}</span>
              </div>

              {truth && (
                <div
                  className="mt-3 border-l-4 border-[var(--accent)] bg-[var(--bg)] p-3 text-sm"
                  data-testid={`placeit-rationale-${axis.axisKey}`}
                >
                  <p className="text-[var(--fg-soft)]">
                    <span className="font-semibold">Our synthesis put this here.</span>{" "}
                    {truth.rationale}
                  </p>
                  {truth.evidence && (
                    <p className="mt-1 text-xs italic text-[var(--muted)]">{truth.evidence}</p>
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="placeit-error">
          {error}
        </p>
      )}

      {result ? (
        <div className="mt-8 border-t border-[var(--border)] pt-6" data-testid="placeit-result">
          <p className="text-sm">
            <strong>{result.score}</strong>/100 in {result.attemptsUsed}{" "}
            {result.attemptsUsed === 1 ? "round" : "rounds"}.
          </p>
          <p className="mt-2 text-sm text-[var(--fg-soft)]">
            Read it differently? That's a fair reading — this is our synthesis, not a verdict.
          </p>
          <p className="mt-3 text-xs uppercase tracking-wider">
            <Link
              to={`/bills/${payload.billId}`}
              className="text-[var(--accent)] underline"
              data-testid="placeit-bill-link"
            >
              See this bill against your compass →
            </Link>
          </p>
        </div>
      ) : (
        <Button
          fullWidth
          disabled={busy}
          onClick={() => void submit()}
          className="mt-8"
          data-testid="placeit-submit"
        >
          {roundsUsed === 0
            ? "Place it"
            : `Try again (${payload.maxRounds - roundsUsed} left)`}
        </Button>
      )}
    </div>
  );
}
