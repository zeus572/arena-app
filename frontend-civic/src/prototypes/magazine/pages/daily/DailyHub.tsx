import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Check } from "lucide-react";
import {
  getDailySlate,
  gameTagline,
  gameTitle,
  kindSlug,
  type DailySlate,
} from "@/api/daily";
import { AnonymousNote, CadenceRing } from "../../components/daily/DailyChrome";

/**
 * The daily hub — one URL to share and return to.
 *
 * Renders however many games are live today. A kind with no approved puzzle simply
 * doesn't appear: the slate degrading to four games is normal, not an error state.
 */
export default function DailyHub() {
  const [slate, setSlate] = useState<DailySlate | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    void getDailySlate()
      .then((s) => {
        setSlate(s);
        setFailed(false);
      })
      .catch(() => setFailed(true))
      .finally(() => setLoaded(true));
  }, []);

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="daily-loading">
        Loading today's games…
      </p>
    );
  }

  if (failed) {
    return (
      <p className="py-12 text-base text-[var(--muted)]" data-testid="daily-error">
        Today's games couldn't load. Try again in a moment.
      </p>
    );
  }

  const puzzles = slate?.puzzles ?? [];

  return (
    <section className="mx-auto max-w-3xl" data-testid="daily-hub">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Daily
      </p>
      <h1 className="display mt-2 text-4xl">A minute of civics</h1>
      <p className="mt-2 max-w-xl text-base text-[var(--fg-soft)]">
        Small games built on real bills, real headlines and real numbers. No account
        needed — just play.
      </p>

      <div className="mt-4">
        {slate && <CadenceRing cadence={slate.cadence} />}
        {slate?.anonymous && <AnonymousNote />}
      </div>

      {puzzles.length === 0 ? (
        <p className="mt-10 text-base text-[var(--muted)]" data-testid="daily-empty">
          No games are live today. Check back tomorrow.
        </p>
      ) : (
        <ul className="mt-8 grid gap-4 sm:grid-cols-2">
          {puzzles.map((p) => {
            const played = p.play?.completed === true;
            return (
              <li key={p.id}>
                <Link
                  to={`/daily/${kindSlug[p.kind]}`}
                  className="flex h-full flex-col border border-[var(--border)] bg-[var(--bg-elev)] p-5 transition hover:border-[var(--accent)]"
                  data-testid={`daily-card-${p.kind}`}
                >
                  <div className="flex items-start justify-between gap-3">
                    <h2 className="display text-2xl">{gameTitle[p.kind]}</h2>
                    <span className="whitespace-nowrap text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                      #{p.edition}
                    </span>
                  </div>
                  <p className="mt-2 flex-1 text-sm text-[var(--fg-soft)]">
                    {gameTagline[p.kind]}
                  </p>
                  <p className="mt-4 text-xs font-semibold uppercase tracking-wider">
                    {played ? (
                      <span
                        className="inline-flex items-center gap-1 text-emerald-600"
                        data-testid={`daily-card-${p.kind}-played`}
                      >
                        <Check size={13} />
                        {p.kind === "Fork" ? "Played" : `Played · ${p.play?.score}/100`}
                      </span>
                    ) : (
                      <span className="text-[var(--accent)]">Play →</span>
                    )}
                  </p>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
