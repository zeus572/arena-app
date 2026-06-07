import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Handshake, RefreshCw, Trophy, Flame, Scale, Award, TrendingUp } from "lucide-react";
import {
  listProvisions,
  seedCoalition,
  getMe,
  getLeagues,
  composeLeagues,
  type ProvisionSummary,
  type Me,
  type League,
} from "@/api/coalition";

function stateColor(state: string): string {
  switch (state) {
    case "Passed": return "text-emerald-600";
    case "Forked": return "text-amber-600";
    case "Died": return "text-[var(--muted)]";
    default: return "text-[var(--accent)]";
  }
}

function difficultyBadge(difficulty: string) {
  const map: Record<string, string> = {
    Narrow: "bg-emerald-100 text-emerald-700",
    Moderate: "bg-amber-100 text-amber-700",
    Wide: "bg-rose-100 text-rose-700",
  };
  return (
    <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ${map[difficulty] ?? "bg-[var(--line)] text-[var(--muted)]"}`}>
      {difficulty}
    </span>
  );
}

function Meter({ label, value, max, suffix }: { label: string; value: number; max: number; suffix?: string }) {
  const pct = max > 0 ? Math.min(100, (value / max) * 100) : 0;
  return (
    <div>
      <div className="flex items-center justify-between text-xs">
        <span className="text-[var(--muted)]">{label}</span>
        <span className="font-semibold">{value}{suffix}</span>
      </div>
      <div className="mt-1 h-1.5 rounded bg-[var(--line)]">
        <div className="h-1.5 rounded bg-[var(--accent)]" style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

function RecordCard({ me }: { me: Me }) {
  const moveColor = me.movement === "Promote" ? "text-emerald-600" : me.movement === "Relegate" ? "text-rose-600" : "text-[var(--muted)]";
  return (
    <div className="rounded-2xl border border-[var(--line)] p-5">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-lg font-semibold"><Award size={18} className="text-[var(--accent)]" /> Your record</h2>
        <span className="rounded-full bg-[var(--accent)] px-3 py-1 text-xs font-semibold text-white">{me.skillLabel}</span>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <Meter label="Skill" value={Math.round(me.skill * 100)} max={100} suffix="%" />
        <Meter label="Coalition breadth (meter)" value={me.record.totalBreadth} max={Math.max(12, me.record.totalBreadth)} />
        <Meter label="Governance vs culture" value={Math.round(me.record.governanceRatio * 100)} max={100} suffix="%" />
        <Meter label="Planks passed" value={me.record.planksPassed} max={Math.max(5, me.record.planksPassed)} />
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-4 text-xs text-[var(--muted)]">
        <span className="flex items-center gap-1"><Trophy size={13} /> score {me.record.weightedScore.toFixed(1)}</span>
        <span className="flex items-center gap-1"><Scale size={13} /> {me.leagueName ?? "Unassigned"} (gap {(me.leagueGapTier * 100).toFixed(0)}%)</span>
        <span className={`flex items-center gap-1 font-semibold ${moveColor}`}><TrendingUp size={13} /> {me.movement}</span>
      </div>

      {/* Two clocks: daily reasoning XP vs scarce coalition currency */}
      <div className="mt-4 grid grid-cols-2 gap-3">
        <div className="rounded-xl border border-[var(--line)] p-3">
          <p className="text-[10px] uppercase tracking-wider text-[var(--muted)]">Daily reasoning XP</p>
          <p className="text-2xl font-bold">{me.todayReasoning}
            <span className="text-sm font-normal text-[var(--muted)]">/{me.dailyReasoningCap} today</span>
          </p>
          <p className="text-[10px] text-[var(--muted)]">{me.reasoningXp} all-time · diminishing returns</p>
        </div>
        <div className="rounded-xl border border-[var(--line)] p-3">
          <p className="text-[10px] uppercase tracking-wider text-[var(--muted)]">Coalition (scarce)</p>
          <p className="text-2xl font-bold text-[var(--accent)]">{me.scarcePoints}</p>
          <p className="text-[10px] text-[var(--muted)]">uncapped · breadth premium</p>
        </div>
      </div>

      {/* Cadence */}
      <div className="mt-4">
        <div className="flex items-center justify-between text-xs text-[var(--muted)]">
          <span className="flex items-center gap-1"><Flame size={13} /> Participation cadence</span>
          <span>{Math.round(me.cadence.score * 100)}%</span>
        </div>
        <div className="mt-1 flex gap-1">
          {me.cadence.last7Days.map((active, i) => (
            <div key={i} className="h-4 flex-1 rounded" style={{ background: active ? "var(--accent)" : "var(--line)" }} title={active ? "active" : "missed"} />
          ))}
        </div>
      </div>
    </div>
  );
}

export default function CoalitionProvisions() {
  const [items, setItems] = useState<ProvisionSummary[]>([]);
  const [me, setMe] = useState<Me | null>(null);
  const [leagues, setLeagues] = useState<League[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [seeding, setSeeding] = useState(false);

  function load() {
    void Promise.all([listProvisions(), getMe(), getLeagues()])
      .then(([p, m, l]) => { setItems(p); setMe(m); setLeagues(l); })
      .finally(() => setLoaded(true));
  }
  useEffect(load, []);

  async function reseed() {
    setSeeding(true);
    try { await seedCoalition(); await composeLeagues(); load(); } finally { setSeeding(false); }
  }

  const recommendedIds = new Set(me?.recommended.map((r) => r.id) ?? []);

  return (
    <section data-testid="coalition-page">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">Coalitions</p>
        <h1 className="display mt-1 text-4xl">Bridge the spectrum</h1>
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
          Take a position, propose a carve-out, and co-sign the version that pulls a cross-spectrum
          coalition together before the deadline. Your record, league, and standings reward breadth
          and bridging — not volume.
        </p>
      </header>

      {import.meta.env.DEV && (
        <div className="mt-6 flex items-center gap-3">
          <button onClick={reseed} disabled={seeding}
            className="inline-flex items-center gap-2 rounded-full border border-[var(--line)] px-3 py-1.5 text-xs font-semibold text-[var(--muted)] hover:text-[var(--fg)]">
            <RefreshCw size={14} className={seeding ? "animate-spin" : ""} /> Seed / recompose demo (dev)
          </button>
        </div>
      )}

      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>
      ) : (
        <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_340px]">
          {/* Left: record + provisions */}
          <div className="grid gap-6">
            {me && <RecordCard me={me} />}

            {me && me.recommended.length > 0 && (
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Recommended for your level</h3>
                <div className="mt-2 flex flex-wrap gap-2">
                  {me.recommended.map((r) => (
                    <Link key={r.id} to={`/coalition/${r.id}`}
                      className="inline-flex items-center gap-2 rounded-full border border-[var(--accent)] px-3 py-1 text-xs font-semibold text-[var(--accent)]">
                      {r.title} {difficultyBadge(r.difficulty)}
                    </Link>
                  ))}
                </div>
              </div>
            )}

            <div>
              <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">All provisions</h3>
              {items.length === 0 ? (
                <p className="py-8 text-sm text-[var(--muted)]">No provisions yet — click “Seed / recompose demo”.</p>
              ) : (
                <ul className="mt-2 grid gap-3">
                  {items.map((p) => (
                    <li key={p.id}>
                      <Link to={`/coalition/${p.id}`}
                        className="block rounded-2xl border border-[var(--line)] p-4 transition hover:border-[var(--accent)]">
                        <div className="flex items-center justify-between gap-3">
                          <h4 className="flex items-center gap-2 font-semibold">
                            <Handshake size={16} className="text-[var(--accent)]" /> {p.title}
                          </h4>
                          <span className={`text-xs font-semibold uppercase tracking-wider ${stateColor(p.state)}`}>{p.state}</span>
                        </div>
                        <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-[var(--muted)]">
                          {difficultyBadge(p.difficulty)}
                          <span className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
                            {p.governance ? "governance" : "culture"}
                          </span>
                          {recommendedIds.has(p.id) && (
                            <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-emerald-700">
                              recommended
                            </span>
                          )}
                          <span>distance {(p.distance * 100).toFixed(0)}%</span>
                          <span>breadth {p.coveredBuckets}/{p.totalBuckets}</span>
                        </div>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>

          {/* Right: standings */}
          <aside>
            <h3 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
              <Trophy size={15} /> Standings
            </h3>
            {leagues.length === 0 ? (
              <p className="mt-2 text-sm text-[var(--muted)]">No leagues yet.</p>
            ) : (
              <div className="mt-2 grid gap-4">
                {leagues.map((l) => (
                  <div key={l.id} className="rounded-2xl border border-[var(--line)] p-4">
                    <div className="flex items-center justify-between">
                      <span className="font-semibold">{l.name}</span>
                      {difficultyBadge(l.difficultyLabel)}
                    </div>
                    <p className="mt-0.5 text-[10px] uppercase tracking-wider text-[var(--muted)]">
                      spectrum: {l.buckets.join(" · ")}
                    </p>
                    <ul className="mt-2 grid gap-1">
                      {l.standings.slice(0, 6).map((s) => (
                        <li key={s.userId} className="flex items-center justify-between text-xs">
                          <span className="truncate">
                            <span className="text-[var(--muted)]">#{s.rank}</span> {s.displayName}
                          </span>
                          <span className="text-[var(--muted)]">
                            {s.score.toFixed(0)} · b{s.totalBreadth} · m{s.movedCount}
                          </span>
                        </li>
                      ))}
                      {l.standings.length === 0 && <li className="text-xs text-[var(--muted)]">No members yet.</li>}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </aside>
        </div>
      )}
    </section>
  );
}
