import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Handshake, RefreshCw } from "lucide-react";
import { listProvisions, seedCoalition, type ProvisionSummary } from "@/api/coalition";

function stateColor(state: string): string {
  switch (state) {
    case "Passed": return "text-emerald-600";
    case "Forked": return "text-amber-600";
    case "Died": return "text-[var(--muted)]";
    default: return "text-[var(--accent)]";
  }
}

export default function CoalitionProvisions() {
  const [items, setItems] = useState<ProvisionSummary[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [seeding, setSeeding] = useState(false);

  function load() {
    void listProvisions().then(setItems).finally(() => setLoaded(true));
  }
  useEffect(load, []);

  async function reseed() {
    setSeeding(true);
    try { await seedCoalition(); load(); } finally { setSeeding(false); }
  }

  return (
    <section data-testid="coalition-page">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
          Coalitions
        </p>
        <h1 className="display mt-1 text-4xl">Bridge the spectrum</h1>
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
          Each provision is a real tradeoff. Take a position, propose a carve-out, and co-sign the
          version that pulls a cross-spectrum coalition together before the deadline. Agents seed the
          thin early rooms — nudge them along with “Run agents”.
        </p>
      </header>

      <div className="mt-6 flex items-center gap-3">
        <button
          onClick={reseed}
          disabled={seeding}
          className="inline-flex items-center gap-2 rounded-full border border-[var(--line)] px-3 py-1.5 text-xs font-semibold text-[var(--muted)] hover:text-[var(--fg)]"
        >
          <RefreshCw size={14} className={seeding ? "animate-spin" : ""} /> Seed demo provisions
        </button>
      </div>

      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>
      ) : items.length === 0 ? (
        <p className="py-12 text-sm text-[var(--muted)]">
          No provisions yet — click “Seed demo provisions”.
        </p>
      ) : (
        <ul className="mt-8 grid gap-4">
          {items.map((p) => (
            <li key={p.id}>
              <Link
                to={`/coalition/${p.id}`}
                className="block rounded-2xl border border-[var(--line)] p-5 transition hover:border-[var(--accent)]"
              >
                <div className="flex items-center justify-between">
                  <h2 className="flex items-center gap-2 text-lg font-semibold">
                    <Handshake size={18} className="text-[var(--accent)]" /> {p.title}
                  </h2>
                  <span className={`text-xs font-semibold uppercase tracking-wider ${stateColor(p.state)}`}>
                    {p.state}
                  </span>
                </div>
                <div className="mt-3 flex items-center gap-4 text-xs text-[var(--muted)]">
                  <span>distance {(p.distance * 100).toFixed(0)}%</span>
                  <span>breadth {p.coveredBuckets}/{p.totalBuckets}</span>
                  {p.deadline && <span>deadline {new Date(p.deadline).toLocaleDateString()}</span>}
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
