import { useState } from "react";
import { ArrowDown, ArrowUp } from "lucide-react";
import {
  submitPricedInGuess,
  type DailyPuzzle,
  type DailyResult,
  type PricedInPayload,
} from "@/api/daily";
import { Button } from "../../Button";

/**
 * Log-scale slider: a linear one would put every interesting answer in the first pixel,
 * since these ranges span millions to trillions.
 */
function fromLog(position: number, min: number, max: number): number {
  const exponent =
    Math.log10(min) + (position / 100) * (Math.log10(max) - Math.log10(min));
  return Math.pow(10, exponent);
}

function formatUsd(value: number): string {
  if (value >= 1e12) return `$${(value / 1e12).toFixed(2)} trillion`;
  if (value >= 1e9) return `$${(value / 1e9).toFixed(2)} billion`;
  if (value >= 1e6) return `$${(value / 1e6).toFixed(2)} million`;
  return `$${Math.round(value).toLocaleString()}`;
}

/**
 * Priced In — guess the size of a real civic figure.
 *
 * Higher/lower is computed server-side, one request per guess, so the true value never
 * reaches the browser before the reveal.
 */
export function PricedInGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as PricedInPayload;
  const [position, setPosition] = useState(50);
  const [history, setHistory] = useState<{ guess: number; direction: string }[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const guess = fromLog(position, payload.minBound, payload.maxBound);
  const guessesLeft = payload.maxGuesses - history.length;

  const submit = async (final: boolean) => {
    if (busy || result) return;
    setBusy(true);
    setError(null);
    try {
      const rounded = Math.round(guess);
      const res = await submitPricedInGuess(rounded, final);
      setHistory((h) => [...h, { guess: rounded, direction: res.direction }]);
      if (res.result) onResult(res.result);
    } catch {
      setError("Couldn't record that guess. Try again.");
    } finally {
      setBusy(false);
    }
  };

  if (result) {
    const reveal = result.reveal;
    return (
      <div data-testid="pricedin-result">
        <p className="text-sm text-[var(--fg-soft)]">{payload.prompt}</p>
        <h1 className="display mt-3 text-4xl text-[var(--accent)]" data-testid="pricedin-truth">
          {formatUsd(reveal?.trueValue ?? 0)}
        </h1>
        <p className="mt-3 text-sm text-[var(--fg-soft)]">{reveal?.anchor}</p>
        <p className="mt-4 text-sm">
          You scored <strong>{result.score}</strong>/100 in {result.attemptsUsed}{" "}
          {result.attemptsUsed === 1 ? "guess" : "guesses"} — within{" "}
          {(reveal?.closeness ?? 1).toFixed(2)}×.
        </p>
        <p className="mt-4 text-xs text-[var(--muted)]">
          Source: {reveal?.source}
          {reveal?.asOf ? ` · as of ${reveal.asOf}` : ""}
        </p>
        {reveal?.sourceUrl && (
          <p className="mt-1 text-xs">
            <a
              href={reveal.sourceUrl}
              target="_blank"
              rel="noreferrer"
              className="text-[var(--accent)] underline"
            >
              Check it yourself
            </a>
          </p>
        )}
      </div>
    );
  }

  return (
    <div data-testid="pricedin-game">
      <h1 className="display text-2xl leading-snug">{payload.prompt}</h1>

      <div className="mt-8">
        <div className="display text-center text-4xl text-[var(--accent)]" data-testid="pricedin-guess">
          {formatUsd(guess)}
        </div>
        <input
          type="range"
          min={0}
          max={100}
          step={0.1}
          value={position}
          onChange={(e) => setPosition(Number(e.target.value))}
          className="mt-4 w-full accent-[var(--accent)]"
          data-testid="pricedin-slider"
        />
        <div className="flex justify-between text-xs text-[var(--muted)]">
          <span>{formatUsd(payload.minBound)}</span>
          <span>{formatUsd(payload.maxBound)}</span>
        </div>
      </div>

      {history.length > 0 && (
        <ul className="mt-6 space-y-2" data-testid="pricedin-history">
          {history.map((h, i) => (
            <li
              key={i}
              className="flex items-center justify-between border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm"
            >
              <span>{formatUsd(h.guess)}</span>
              <span className="flex items-center gap-1 font-semibold text-[var(--accent)]">
                {h.direction === "higher" ? (
                  <>
                    <ArrowUp size={14} /> Higher
                  </>
                ) : h.direction === "lower" ? (
                  <>
                    <ArrowDown size={14} /> Lower
                  </>
                ) : (
                  "Exact"
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="pricedin-error">
          {error}
        </p>
      )}

      <div className="mt-6 flex gap-3">
        <Button
          fullWidth
          disabled={busy || guessesLeft <= 1}
          onClick={() => void submit(false)}
          variant="secondary"
          data-testid="pricedin-guess-btn"
        >
          Guess ({guessesLeft} left)
        </Button>
        <Button
          fullWidth
          disabled={busy}
          onClick={() => void submit(true)}
          data-testid="pricedin-final"
        >
          Lock it in
        </Button>
      </div>
      <p className="mt-3 text-xs text-[var(--muted)]">
        Each extra guess costs 10% of the score. Lock in early if you're confident.
      </p>
    </div>
  );
}
