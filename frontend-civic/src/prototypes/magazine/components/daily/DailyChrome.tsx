import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Flame } from "lucide-react";
import type { DailyCadence, DailyGameKind } from "@/api/daily";
import { gameTitle } from "@/api/daily";

/** Shell for a single game: kicker, title, edition, and the body. */
export function DailyCardShell({
  kind,
  edition,
  children,
}: {
  kind: DailyGameKind;
  edition: number;
  children: ReactNode;
}) {
  return (
    <article className="mx-auto max-w-2xl" data-testid={`daily-game-${kind}`}>
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Daily · {gameTitle[kind]} #{edition}
      </p>
      <section className="mt-6 border border-[var(--border)] bg-[var(--bg-elev)] p-6">
        {children}
      </section>
      <p className="mt-6 text-xs uppercase tracking-wider">
        <Link to="/daily" className="text-[var(--accent)] underline">
          ← All of today's games
        </Link>
      </p>
    </article>
  );
}

/**
 * "What everyone else did" — the reveal bar every game shares.
 *
 * A percentage over a handful of plays is noise dressed up as a fact, so a thin sample
 * is shown as a count and labelled, never as a percentage.
 */
export function CrowdBar({
  label,
  aLabel,
  bLabel,
  aPercent,
  bPercent,
  total,
  suppressed,
}: {
  label: string;
  aLabel: string;
  bLabel: string;
  aPercent: number;
  bPercent: number;
  total: number;
  suppressed: boolean;
}) {
  if (suppressed) {
    return (
      <div className="mt-4 text-xs text-[var(--muted)]" data-testid="daily-crowd-suppressed">
        {label}: only {total} {total === 1 ? "play" : "plays"} so far — not enough to show a
        split yet.
      </div>
    );
  }

  return (
    <div className="mt-4" data-testid="daily-crowd-bar">
      <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        <span>{label}</span>
        <span>{total} plays</span>
      </div>
      <div className="mt-2 flex h-3 w-full overflow-hidden rounded bg-[var(--bg)]">
        <div className="h-3 bg-[var(--accent)]" style={{ width: `${aPercent}%` }} />
        <div className="h-3 bg-[var(--border)]" style={{ width: `${bPercent}%` }} />
      </div>
      <div className="mt-1 flex justify-between text-xs text-[var(--fg-soft)]">
        <span>
          {aPercent}% {aLabel}
        </span>
        <span>
          {bPercent}% {bLabel}
        </span>
      </div>
    </div>
  );
}

/**
 * The weekly ring. Deliberately not a breakable streak counter — a hard streak punishes
 * the occasional, busy player and pushes compulsive daily check-ins, which the
 * gamification docs rule out.
 */
export function CadenceRing({ cadence }: { cadence: DailyCadence }) {
  return (
    <div
      className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]"
      data-testid="daily-cadence"
    >
      <Flame size={14} className="text-[var(--accent)]" />
      <span data-testid="daily-cadence-count">{cadence.activeDays} of 7 days this week</span>
      <span className="flex gap-1">
        {cadence.last7Days.map((active, i) => (
          <span
            key={i}
            className={`h-2 w-2 rounded-full ${active ? "bg-[var(--accent)]" : "bg-[var(--border)]"}`}
          />
        ))}
      </span>
    </div>
  );
}

/** Shown to players with no stable id: they can play, nothing is recorded. */
export function AnonymousNote() {
  return (
    <p className="mt-4 text-xs text-[var(--muted)]" data-testid="daily-anon-note">
      You're playing as a guest — scores and streaks aren't saved.{" "}
      <Link to="/register" className="text-[var(--accent)] underline">
        Create an account
      </Link>{" "}
      to keep them.
    </p>
  );
}
