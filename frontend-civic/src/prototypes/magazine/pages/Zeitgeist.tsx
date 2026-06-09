import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Handshake, Compass, Radio, GraduationCap } from "lucide-react";
import { getZeitgeist, type Zeitgeist, type ZeitgeistAxis } from "@/api/zeitgeist";

function AxisLean({ axis }: { axis: ZeitgeistAxis }) {
  // Score in [-1, 1] → marker position 0%..100%.
  const markerLeft = `${50 + axis.averageScore * 50}%`;
  const hasData = axis.sampleSize > 0;
  return (
    <div className="border-t border-[var(--line)] pt-4" data-testid={`zeitgeist-axis-${axis.axisKey}`}>
      <div className="flex items-baseline justify-between gap-3">
        <p className="font-semibold">{axis.axisName}</p>
        <p className="text-xs uppercase tracking-wider text-[var(--accent)]">{axis.leanLabel}</p>
      </div>
      <div className="mt-2 flex items-center justify-between text-xs text-[var(--muted)]">
        <span>{axis.lowLabel}</span>
        <span>{axis.highLabel}</span>
      </div>
      <div className="relative mt-2 h-2 w-full rounded bg-[var(--bg-elev)]">
        <div className="absolute left-1/2 top-0 h-full w-px bg-[var(--line)]" />
        {hasData && (
          <div
            className="absolute top-1/2 h-4 w-4 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white bg-[var(--accent)]"
            style={{ left: markerLeft }}
          />
        )}
      </div>
      <p className="mt-1 text-[11px] text-[var(--muted)]">
        {hasData ? `${axis.sampleSize} compass${axis.sampleSize === 1 ? "" : "es"}` : "No data yet"}
      </p>
    </div>
  );
}

export default function Zeitgeist() {
  const [z, setZ] = useState<Zeitgeist | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    void getZeitgeist()
      .then(setZ)
      .finally(() => setLoaded(true));
  }, []);

  if (!loaded) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="zeitgeist-loading">Reading the room…</p>;
  }
  if (!z) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="zeitgeist-error">The Zeitgeist is unavailable right now.</p>;
  }

  const leaningAxes = z.axes.filter((a) => a.sampleSize > 0);

  return (
    <section data-testid="zeitgeist-page">
      <header>
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
          <Radio size={14} /> The Zeitgeist
        </p>
        <h1 className="display mt-1 text-4xl md:text-5xl">What people are discovering</h1>
        <p className="mt-3 max-w-prose text-[var(--fg-soft)]">
          Not a poll of slogans — a read-out of how people are actually governing themselves here:
          where their Civic Compasses point, the coalition positions that are coming together, and the
          civics that trip us up. These are signals worth sending to the people who write the rules.
        </p>
        <div className="mt-4 flex flex-wrap gap-4 text-xs uppercase tracking-wider text-[var(--muted)]">
          <span data-testid="zeitgeist-total-profiles">{z.totals.profileCount} compasses built</span>
          <span data-testid="zeitgeist-total-coalitions">{z.totals.coalitionCount} live coalitions</span>
          <span data-testid="zeitgeist-total-quiz">{z.totals.quizResponseCount} quiz answers (60d)</span>
        </div>
      </header>

      {/* Coalition discoveries */}
      <div className="mt-12">
        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
          <Handshake size={16} className="text-[var(--accent)]" /> Where coalitions are forming
        </h2>
        {z.coalitions.length === 0 ? (
          <p className="mt-3 text-sm text-[var(--muted)]">No coalitions are running yet.</p>
        ) : (
          <ul className="mt-4 grid gap-3" data-testid="zeitgeist-coalitions">
            {z.coalitions.map((c) => (
              <li key={c.provisionId}>
                <Link
                  to={`/coalition/${c.provisionId}`}
                  className="block rounded-2xl border border-[var(--line)] p-4 transition hover:border-[var(--accent)]"
                >
                  <div className="flex items-center justify-between gap-3">
                    <h3 className="font-semibold">{c.title}</h3>
                    <span className="shrink-0 text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{c.state}</span>
                  </div>
                  <p className="mt-1.5 text-sm leading-snug text-[var(--fg-soft)]">{c.prevailingPosition}</p>
                  <p className="mt-2 text-xs text-[var(--accent)]">{c.signal}</p>
                  <p className="mt-1 text-[11px] uppercase tracking-wider text-[var(--muted)]">
                    {c.participantCount} participant{c.participantCount === 1 ? "" : "s"}
                    {c.accepts > 0 && ` · ${c.accepts} co-sign${c.accepts === 1 ? "" : "s"}`}
                  </p>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Where the public leans */}
      <div className="mt-12">
        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
          <Compass size={16} className="text-[var(--accent)]" /> Where the public leans
        </h2>
        {leaningAxes.length === 0 ? (
          <p className="mt-3 text-sm text-[var(--muted)]">
            Not enough Civic Compasses yet. As people answer, the prevailing leans appear here.
          </p>
        ) : (
          <div className="mt-4 grid gap-4 rounded-2xl border border-[var(--line)] p-5" data-testid="zeitgeist-axes">
            {leaningAxes.map((a) => (
              <AxisLean key={a.axisKey} axis={a} />
            ))}
          </div>
        )}
      </div>

      {/* Civic blind spots */}
      {z.quizSignals.length > 0 && (
        <div className="mt-12">
          <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
            <GraduationCap size={16} className="text-[var(--accent)]" /> What trips people up
          </h2>
          <ul className="mt-4 grid gap-3" data-testid="zeitgeist-quiz-signals">
            {z.quizSignals.map((s, i) => (
              <li key={i} className="rounded-2xl border border-[var(--line)] p-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{s.topic}</p>
                  <span className="shrink-0 text-sm font-semibold">{Math.round(s.correctRate * 100)}% correct</span>
                </div>
                <p className="mt-1.5 text-sm text-[var(--fg-soft)]">{s.question}</p>
                <p className="mt-1 text-[11px] uppercase tracking-wider text-[var(--muted)]">
                  {s.responseCount} answer{s.responseCount === 1 ? "" : "s"} · 60-day average
                </p>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="mt-12 text-xs text-[var(--muted)]">
        Updated {new Date(z.generatedAt).toLocaleString()}. Aggregate and anonymous — no individual is identified.
      </p>
    </section>
  );
}
