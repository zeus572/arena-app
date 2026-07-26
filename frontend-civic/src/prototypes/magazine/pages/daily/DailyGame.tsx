import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  getDailyPuzzle,
  slugToKind,
  type DailyPuzzle,
  type DailyResult,
} from "@/api/daily";
import { DailyCardShell } from "../../components/daily/DailyChrome";
import { ShareGrid } from "../../components/daily/ShareGrid";
import { ForkGame } from "../../components/daily/games/ForkGame";
import { CrowdCallGame } from "../../components/daily/games/CrowdCallGame";
import { PricedInGame } from "../../components/daily/games/PricedInGame";
import { PlaceItGame } from "../../components/daily/games/PlaceItGame";
import { TimeMachineGame } from "../../components/daily/games/TimeMachineGame";
import { WhoseValueGame } from "../../components/daily/games/WhoseValueGame";

/** Player shell: loads today's puzzle for a kind and dispatches to that game's body. */
export default function DailyGame() {
  const { kind: slug } = useParams<{ kind: string }>();
  const kind = slug ? slugToKind[slug] : undefined;

  const [puzzle, setPuzzle] = useState<DailyPuzzle | null>(null);
  const [result, setResult] = useState<DailyResult | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!kind) {
      setLoaded(true);
      return;
    }
    setLoaded(false);
    setResult(null);
    void getDailyPuzzle(kind)
      .then(setPuzzle)
      .catch(() => setPuzzle(null))
      .finally(() => setLoaded(true));
  }, [kind]);

  if (!kind) {
    return (
      <p className="py-12 text-base text-[var(--muted)]" data-testid="daily-unknown">
        That game doesn't exist.{" "}
        <Link to="/daily" className="text-[var(--accent)] underline">
          See today's games
        </Link>
        .
      </p>
    );
  }

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="daily-game-loading">
        Loading…
      </p>
    );
  }

  if (!puzzle) {
    // A kind with no live puzzle today is normal, not an error.
    return (
      <p className="py-12 text-base text-[var(--muted)]" data-testid="daily-game-empty">
        This one isn't live today.{" "}
        <Link to="/daily" className="text-[var(--accent)] underline">
          See what is
        </Link>
        .
      </p>
    );
  }

  const props = { puzzle, result, onResult: setResult };

  return (
    <DailyCardShell kind={puzzle.kind} edition={puzzle.edition}>
      {puzzle.kind === "Fork" && <ForkGame {...props} />}
      {puzzle.kind === "CrowdCall" && <CrowdCallGame {...props} />}
      {puzzle.kind === "PricedIn" && <PricedInGame {...props} />}
      {puzzle.kind === "PlaceIt" && <PlaceItGame {...props} />}
      {puzzle.kind === "TimeMachine" && <TimeMachineGame {...props} />}
      {puzzle.kind === "WhoseValue" && <WhoseValueGame {...props} />}

      {result?.shareGrid && <ShareGrid grid={result.shareGrid} />}

      {result && result.pointsAwarded > 0 && (
        <p
          className="mt-4 text-xs font-semibold uppercase tracking-wider text-[var(--accent)]"
          data-testid="daily-xp"
        >
          +{result.pointsAwarded} reasoning XP
        </p>
      )}

      {/* Already played earlier today: the server rejects a replay, so say so rather than
          letting someone submit into a 409. */}
      {!result && puzzle.play?.completed && (
        <p className="mt-6 text-sm text-[var(--muted)]" data-testid="daily-already-played">
          You've already played this one today
          {puzzle.kind === "Fork" ? "" : ` — you scored ${puzzle.play.score}/100`}. Come back
          tomorrow for a new one.
        </p>
      )}
    </DailyCardShell>
  );
}
