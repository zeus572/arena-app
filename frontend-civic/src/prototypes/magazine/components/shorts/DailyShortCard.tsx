import { useState } from "react";
import { Link } from "react-router-dom";
import {
  gameTagline,
  gameTitle,
  kindSlug,
  submitDailyPlay,
  type CrowdCallPayload,
  type DailyPuzzle,
  type DailyResult,
  type ForkPayload,
  type PlaceItPayload,
  type PricedInPayload,
  type TimeMachinePayload,
  type WhichIsTruePayload,
  type WhoseValuePayload,
} from "@/api/daily";
import { ShortCardShell } from "./ShortCardShell";

/**
 * Full-viewport Shorts card for one of today's daily games.
 *
 * Fork is played INLINE — it's a single tap and a reveal, which is exactly the shape a
 * feed card wants. The other five are multi-round (five sliders, three axes, a guess
 * ladder); they'd either overflow a snap card or leave a half-finished play that the
 * one-play-per-puzzle rule then rejects with a 409. Those get a teaser that hands off to
 * the full game instead.
 */
export function DailyShortCard({ puzzle }: { puzzle: DailyPuzzle }) {
  return puzzle.kind === "Fork" ? (
    <ForkShortCard puzzle={puzzle} />
  ) : (
    <TeaserShortCard puzzle={puzzle} />
  );
}

function Kicker({ puzzle }: { puzzle: DailyPuzzle }) {
  return (
    <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
      Daily · {gameTitle[puzzle.kind]} #{puzzle.edition}
    </p>
  );
}

function ForkShortCard({ puzzle }: { puzzle: DailyPuzzle }) {
  const payload = puzzle.payload as ForkPayload;
  const [result, setResult] = useState<DailyResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const choose = async (choice: "A" | "B") => {
    if (busy || result) return;
    setBusy(true);
    setError(null);
    try {
      setResult(await submitDailyPlay("Fork", { choice }));
    } catch {
      // Never swallow it — a tap that does nothing reads as a broken card.
      setError("That didn't save. Tap to try again.");
    } finally {
      setBusy(false);
    }
  };

  const national = result?.crowd?.national;
  const suppressed = national?.suppressed ?? true;

  return (
    <ShortCardShell>
      <div className="my-4 flex flex-1 flex-col justify-center" data-testid="short-daily-fork">
        <Kicker puzzle={puzzle} />
        <h2 className="display mt-3 text-2xl leading-snug">{payload.question}</h2>

        <div className="mt-5 space-y-3">
          {(["A", "B"] as const).map((key) => {
            const option = key === "A" ? payload.optionA : payload.optionB;
            const share = key === "A" ? national?.aPercent : national?.bPercent;
            return (
              <button
                key={key}
                type="button"
                disabled={busy || !!result}
                onClick={() => void choose(key)}
                className="w-full border-2 border-[var(--border)] bg-[var(--bg)] p-3 text-left transition enabled:hover:border-[var(--accent)] disabled:cursor-default"
                data-testid={`short-daily-option-${key}`}
              >
                <span className="flex items-baseline justify-between gap-3">
                  <span className="text-sm font-semibold">{option.label}</span>
                  {result && !suppressed && (
                    <span className="shrink-0 text-sm font-semibold text-[var(--accent)]">
                      {share}%
                    </span>
                  )}
                </span>
                <span className="mt-1 block text-xs text-[var(--muted)]">
                  Costs you: {option.cost}
                </span>
                {result && !suppressed && (
                  <span className="mt-2 block h-1 w-full bg-[var(--bg-elev)]">
                    <span
                      className="block h-1 bg-[var(--accent)]"
                      style={{ width: `${share ?? 0}%` }}
                    />
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {error && (
          <p className="mt-3 text-sm font-semibold text-red-600" data-testid="short-daily-error">
            {error}
          </p>
        )}

        {result && suppressed && (
          <p className="mt-3 text-xs text-[var(--muted)]" data-testid="short-daily-thin">
            Only {national?.total ?? 0} plays so far — not enough to show a split yet.
          </p>
        )}
      </div>

      <div className="mt-4">
        <Link
          to="/daily"
          className="block text-right text-sm font-semibold text-[var(--accent)] hover:underline"
          data-testid="short-daily-open"
        >
          {result ? "More of today's games →" : "See today's games →"}
        </Link>
      </div>
    </ShortCardShell>
  );
}

/**
 * A line of the puzzle's actual content for the teaser to lead with. The redacted payload
 * already carries everything here — none of it is answer key. A card showing the real
 * question is worth far more taps than one showing only the game's name.
 */
function previewFor(puzzle: DailyPuzzle): string | null {
  switch (puzzle.kind) {
    case "CrowdCall":
      return (puzzle.payload as CrowdCallPayload).rounds[0]?.prompt ?? null;
    case "PricedIn":
      return (puzzle.payload as PricedInPayload).prompt ?? null;
    case "PlaceIt":
      return (puzzle.payload as PlaceItPayload).billTitle ?? null;
    case "TimeMachine":
      return (puzzle.payload as TimeMachinePayload).items[0]?.headline ?? null;
    case "WhoseValue":
      return (puzzle.payload as WhoseValuePayload).rounds[0]?.argument ?? null;
    case "WhichIsTrue":
      return (puzzle.payload as WhichIsTruePayload).rounds[0]?.prompt ?? null;
    default:
      return null;
  }
}

/** What the player is being asked to do with the previewed line. */
const previewLead: Partial<Record<DailyPuzzle["kind"], string>> = {
  CrowdCall: "How many people know this?",
  PricedIn: "How big is this number?",
  PlaceIt: "Where does this bill sit?",
  TimeMachine: "When did this run?",
  WhoseValue: "What value is this appealing to?",
  WhichIsTrue: "Two real numbers — which one is it?",
};

function TeaserShortCard({ puzzle }: { puzzle: DailyPuzzle }) {
  const preview = previewFor(puzzle);
  const lead = previewLead[puzzle.kind];

  return (
    <ShortCardShell>
      <div className="my-4 flex flex-1 flex-col justify-center" data-testid="short-daily-teaser">
        <Kicker puzzle={puzzle} />
        <h2 className="display mt-3 text-3xl leading-tight">{gameTitle[puzzle.kind]}</h2>
        <p className="mt-2 text-base text-[var(--fg-soft)]">{gameTagline[puzzle.kind]}</p>

        {preview && (
          <div
            className="mt-6 border-l-4 border-[var(--accent)] bg-[var(--bg)] p-4"
            data-testid="short-daily-preview"
          >
            {lead && (
              <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                {lead}
              </p>
            )}
            <p className="mt-2 text-lg leading-snug">{preview}</p>
          </div>
        )}

        <p className="mt-5 text-sm text-[var(--muted)]">
          Takes about a minute. No account needed.
        </p>
      </div>

      <div className="mt-4">
        <Link
          to={`/daily/${kindSlug[puzzle.kind]}`}
          className="block text-right text-sm font-semibold text-[var(--accent)] hover:underline"
          data-testid="short-daily-open"
        >
          Play {gameTitle[puzzle.kind]} →
        </Link>
      </div>
    </ShortCardShell>
  );
}
