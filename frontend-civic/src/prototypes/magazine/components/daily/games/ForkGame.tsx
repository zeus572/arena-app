import { useState } from "react";
import { Link } from "react-router-dom";
import { submitDailyPlay, type DailyPuzzle, type DailyResult, type ForkPayload } from "@/api/daily";
import { CrowdBar } from "../DailyChrome";

/**
 * Fork — the daily "would you rather". One tap, then the split.
 *
 * There's no score here: the payoff is seeing where the country landed. Both options
 * always state what they cost, which is the whole neutrality guard (an option with no
 * stated cost never makes it past generation).
 */
export function ForkGame({
  puzzle,
  result,
  onResult,
}: {
  puzzle: DailyPuzzle;
  result: DailyResult | null;
  onResult: (r: DailyResult) => void;
}) {
  const payload = puzzle.payload as ForkPayload;
  const [picked, setPicked] = useState<"A" | "B" | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const choose = async (choice: "A" | "B") => {
    if (busy || result) return;
    setPicked(choice);
    setBusy(true);
    setError(null);
    try {
      onResult(await submitDailyPlay("Fork", { choice }));
    } catch {
      // Never swallow a failed write — the tap must visibly succeed or visibly fail.
      setPicked(null);
      setError("That didn't save. Tap to try again.");
    } finally {
      setBusy(false);
    }
  };

  const crowd = result?.crowd;

  return (
    <div data-testid="fork-game">
      <h1 className="display text-3xl leading-snug">{payload.question}</h1>
      {payload.tradeoff && (
        <p className="mt-3 text-sm text-[var(--fg-soft)]">{payload.tradeoff}</p>
      )}

      <div className="mt-6 grid gap-3 sm:grid-cols-2">
        {(["A", "B"] as const).map((key) => {
          const option = key === "A" ? payload.optionA : payload.optionB;
          const isPicked = picked === key;
          return (
            <button
              key={key}
              type="button"
              disabled={busy || !!result}
              onClick={() => void choose(key)}
              className={`border-2 p-4 text-left transition disabled:cursor-default ${
                isPicked
                  ? "border-[var(--accent)] bg-[var(--accent)]/5"
                  : "border-[var(--border)] bg-[var(--bg)] hover:border-[var(--accent)]"
              }`}
              data-testid={`fork-option-${key}`}
            >
              <span className="block text-base font-semibold">{option.label}</span>
              <span className="mt-2 block text-sm text-[var(--muted)]">
                Costs you: {option.cost}
              </span>
            </button>
          );
        })}
      </div>

      {error && (
        <p className="mt-4 text-sm font-semibold text-red-600" data-testid="fork-error">
          {error}
        </p>
      )}

      {result && crowd && (
        <div className="mt-8 border-t border-[var(--border)] pt-6" data-testid="fork-reveal">
          <CrowdBar
            label="The country"
            aLabel={payload.optionA.label}
            bLabel={payload.optionB.label}
            aPercent={crowd.national?.aPercent ?? 0}
            bPercent={crowd.national?.bPercent ?? 0}
            total={crowd.national?.total ?? 0}
            suppressed={crowd.national?.suppressed ?? true}
          />
          {crowd.locality && (
            <CrowdBar
              label={`Your state (${crowd.locality.label})`}
              aLabel={payload.optionA.label}
              bLabel={payload.optionB.label}
              aPercent={crowd.locality.aPercent}
              bPercent={crowd.locality.bPercent}
              total={crowd.locality.total}
              suppressed={crowd.locality.suppressed}
            />
          )}
          {crowd.ageBand && (
            <CrowdBar
              label="Your age group"
              aLabel={payload.optionA.label}
              bLabel={payload.optionB.label}
              aPercent={crowd.ageBand.aPercent}
              bPercent={crowd.ageBand.bPercent}
              total={crowd.ageBand.total}
              suppressed={crowd.ageBand.suppressed}
            />
          )}

          {/* A single tap carries no confidence or intensity, so it never silently edits
              the compass. The upgrade is offered explicitly instead. */}
          <div className="mt-6 border-l-4 border-[var(--accent)] bg-[var(--bg)] p-4 text-sm">
            <p className="font-semibold">Want this on your compass?</p>
            <p className="mt-1 text-[var(--fg-soft)]">
              One tap isn't enough to place you on an axis — answer it properly, with how
              sure and how strongly you feel, and it counts.
            </p>
            <p className="mt-2 text-xs uppercase tracking-wider">
              <Link to="/onboarding" className="text-[var(--accent)] underline">
                Add it to your compass →
              </Link>
            </p>
          </div>

          {payload.provisionSlug && (
            <p className="mt-4 text-xs uppercase tracking-wider">
              <Link
                to={`/coalition/${payload.provisionSlug}`}
                className="text-[var(--accent)] underline"
                data-testid="fork-provision-link"
              >
                See the bill this came from →
              </Link>
            </p>
          )}
        </div>
      )}
    </div>
  );
}
