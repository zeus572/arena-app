import { useState } from "react";
import {
  submitDailyPlay,
  type CrowdCallPayload,
  type DailyPuzzle,
  type DailyResult,
} from "@/api/daily";
import { Button } from "../../Button";

/**
 * Crowd Call — guess what share of people got each question right.
 *
 * The skill is calibration, not knowledge: you're shown the answer up front and asked
 * how many people know it. The end card reports SIGNED error, because systematically
 * overestimating how divided/uninformed the country is the specific bias worth surfacing.
 */
export function CrowdCallGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as CrowdCallPayload;
  const [index, setIndex] = useState(0);
  const [guesses, setGuesses] = useState<number[]>(() => payload.rounds.map(() => 50));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const round = payload.rounds[index];
  const last = index === payload.rounds.length - 1;

  const setGuess = (value: number) =>
    setGuesses((g) => g.map((v, i) => (i === index ? value : v)));

  const next = async () => {
    if (!last) {
      setIndex((i) => i + 1);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      onResult(await submitDailyPlay("CrowdCall", { guesses: guesses.map((g) => g / 100) }));
    } catch {
      setError("Couldn't submit your answers. Try again.");
    } finally {
      setBusy(false);
    }
  };

  if (result) {
    const reveal = result.reveal;
    return (
      <div data-testid="crowdcall-result">
        <h1 className="display text-3xl">
          {result.score}
          <span className="text-xl text-[var(--muted)]">/100</span>
        </h1>
        <p className="mt-2 text-sm text-[var(--fg-soft)]">
          You overestimated how divided people are on{" "}
          <strong>{reveal?.overestimatedDivision ?? 0}</strong> of {payload.rounds.length}.
        </p>

        <ul className="mt-6 space-y-4">
          {payload.rounds.map((r, i) => {
            const truth = Math.round((reveal?.rounds?.[i]?.trueRate ?? 0) * 100);
            return (
              <li
                key={i}
                className="border border-[var(--border)] bg-[var(--bg)] p-4"
                data-testid={`crowdcall-reveal-${i}`}
              >
                <p className="text-sm font-semibold">{r.prompt}</p>
                <p className="mt-1 text-sm text-[var(--fg-soft)]">{r.answer}</p>
                <p className="mt-2 text-sm">
                  You said <strong>{guesses[i]}%</strong> · actually <strong>{truth}%</strong>
                </p>
                {/* Always name the crowd — attributing a published poll to our own users
                    (or the reverse) is a credibility problem, not a cosmetic one. */}
                <p className="mt-2 text-xs text-[var(--muted)]">
                  {r.attribution}
                  {reveal?.rounds?.[i]?.sampleSize
                    ? ` · n=${reveal.rounds[i].sampleSize}`
                    : ""}
                  {r.fieldedOn ? ` · fielded ${r.fieldedOn}` : ""}
                </p>
                {r.sourceUrl && (
                  <p className="mt-1 text-xs">
                    <a
                      href={r.sourceUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="text-[var(--accent)] underline"
                    >
                      Source
                    </a>
                  </p>
                )}
                <p className="mt-2 text-xs italic text-[var(--fg-soft)]">{r.explanation}</p>
              </li>
            );
          })}
        </ul>
      </div>
    );
  }

  return (
    <div data-testid="crowdcall-game">
      <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        Question {index + 1} of {payload.rounds.length}
      </p>
      <h1 className="display mt-3 text-2xl leading-snug">{round.prompt}</h1>
      <p className="mt-3 border-l-4 border-[var(--accent)] bg-[var(--bg)] p-3 text-sm">
        <span className="font-semibold">Answer:</span> {round.answer}
      </p>

      <label
        className="mt-8 block text-sm font-semibold"
        htmlFor="crowdcall-slider"
      >
        What share of people got this right?
      </label>
      <div className="mt-3 flex items-center gap-4">
        <input
          id="crowdcall-slider"
          type="range"
          min={0}
          max={100}
          value={guesses[index]}
          onChange={(e) => setGuess(Number(e.target.value))}
          className="w-full accent-[var(--accent)]"
          data-testid="crowdcall-slider"
        />
        <span
          className="display w-16 text-right text-2xl text-[var(--accent)]"
          data-testid="crowdcall-guess"
        >
          {guesses[index]}%
        </span>
      </div>
      <p className="mt-2 text-xs text-[var(--muted)]">{round.attribution}</p>

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="crowdcall-error">
          {error}
        </p>
      )}

      <Button
        fullWidth
        disabled={busy}
        onClick={() => void next()}
        className="mt-6"
        data-testid="crowdcall-next"
      >
        {last ? "See how you did" : "Next question"}
      </Button>
    </div>
  );
}
