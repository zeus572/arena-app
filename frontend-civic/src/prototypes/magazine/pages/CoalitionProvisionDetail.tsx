import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Bot, Check, X } from "lucide-react";
import {
  getProvision,
  getFramings,
  joinProvision,
  takePosition,
  proposeAmendment,
  proposeFreeformAmendment,
  recordAct,
  castAcceptance,
  agentStep,
  type ProvisionDetail,
  type Framings,
} from "@/api/coalition";

function SpectrumBarView({ d }: { d: ProvisionDetail }) {
  const bar = d.spectrumBar;
  return (
    <div className="rounded-2xl border border-[var(--line)] p-4">
      <div className="mb-2 flex items-center justify-between text-xs text-[var(--muted)]">
        <span>Coalition reach across the spectrum</span>
        <span>distance {(bar.distance * 100).toFixed(0)}% · breadth {bar.coveredBuckets}/{bar.totalBuckets}</span>
      </div>
      <div className="flex gap-1.5">
        {bar.cells.map((c) => (
          <div key={c.bucket} className="flex-1 text-center">
            <div
              className="h-6 rounded"
              style={{ background: c.covered ? "var(--accent)" : "var(--line)" }}
              title={c.bucket}
            />
            <span className="mt-1 block text-[10px] uppercase tracking-wider text-[var(--muted)]">
              {c.bucket}
            </span>
          </div>
        ))}
      </div>
      <p className="mt-3 text-xs font-semibold text-[var(--accent)]">{bar.callToAction}</p>
      {bar.deadline && (
        <p className="mt-1 text-xs text-[var(--muted)]">
          deadline {new Date(bar.deadline).toLocaleString()}
          {bar.daysLeft != null && ` · ${bar.daysLeft} day${bar.daysLeft === 1 ? "" : "s"} left`}
        </p>
      )}
    </div>
  );
}

export default function CoalitionProvisionDetail() {
  const { id = "" } = useParams();
  const [d, setD] = useState<ProvisionDetail | null>(null);
  const [busy, setBusy] = useState(false);
  const [stance, setStance] = useState("");
  const [bucket, setBucket] = useState("left");
  const [carveOut, setCarveOut] = useState<Record<string, string>>({});
  const [freeText, setFreeText] = useState("");
  const [steelText, setSteelText] = useState("");
  const [ptsMsg, setPtsMsg] = useState<string | null>(null);
  const [framings, setFramings] = useState<Framings | null>(null);

  function reload() { void getProvision(id).then(setD); }
  useEffect(reload, [id]);
  useEffect(() => { void getFramings(id).then(setFramings).catch(() => {}); }, [id]);

  async function run(fn: () => Promise<ProvisionDetail>) {
    setBusy(true);
    try { setD(await fn()); } finally { setBusy(false); }
  }

  async function act(type: string, payload?: string) {
    setBusy(true);
    try {
      const r = await recordAct(id, type, payload);
      setPtsMsg(`+${r.points} ${r.currency} pts`);
      setD(await getProvision(id));
    } finally { setBusy(false); }
  }

  if (!d) return <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>;

  const resolved = ["Passed", "Forked", "Died"].includes(d.state);

  return (
    <section data-testid="coalition-detail" className="max-w-3xl">
      <Link to="/coalition" className="inline-flex items-center gap-1 text-xs text-[var(--muted)] hover:text-[var(--fg)]">
        <ArrowLeft size={14} /> All provisions
      </Link>

      <header className="mt-3">
        <div className="flex items-center justify-between">
          <h1 className="display text-3xl">{d.title}</h1>
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{d.state}</span>
        </div>
        <p className="mt-2 text-[var(--fg-soft)]">{d.neutralText}</p>
        <div className="mt-2 flex flex-wrap items-center gap-2 text-[10px] font-semibold uppercase tracking-wider">
          <span className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[var(--muted)]">{d.difficulty} gap · {(d.gapWidth * 100).toFixed(0)}%</span>
          <span className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[var(--muted)]">{d.governance ? "governance" : "culture"}</span>
          {d.relevantAxes.map((a) => (
            <span key={a} className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[var(--muted)]">{a}</span>
          ))}
        </div>
      </header>

      {framings && (
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          <div className="rounded-2xl border border-rose-200 bg-rose-50/50 p-4">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-rose-700">Cultural framing</p>
            <p className="mt-1 text-sm text-[var(--fg-soft)]">{framings.culturalFrame}</p>
          </div>
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50/50 p-4">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-emerald-700">Governance framing</p>
            <p className="mt-1 text-sm text-[var(--fg-soft)]">{framings.governanceFrame}</p>
          </div>
        </div>
      )}

      <div className="mt-6"><SpectrumBarView d={d} /></div>

      {ptsMsg && (
        <div className="mt-3 inline-block rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">
          {ptsMsg}
        </div>
      )}

      {!resolved && d.probes.length > 0 && (
        <div className="mt-4 rounded-2xl border border-[var(--accent)] p-4">
          <h3 className="text-sm font-semibold">Bridge probes</h3>
          <p className="mt-1 text-xs text-[var(--muted)]">Precomputed variants near the gap — would you also co-sign?</p>
          {d.probes.map((pr) => (
            <div key={pr.versionId} className="mt-2 flex items-center justify-between gap-3 text-sm">
              <span>{pr.prompt}</span>
              <button onClick={() => run(() => castAcceptance(id, pr.versionId, true))} disabled={busy}
                className="rounded-full bg-emerald-600 px-3 py-1 text-xs font-semibold text-white disabled:opacity-50">
                Co-sign
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Daily acts — earn reasoning XP */}
      <div className="mt-4 rounded-2xl border border-[var(--line)] p-4">
        <h3 className="text-sm font-semibold">Daily acts <span className="text-xs font-normal text-[var(--muted)]">— earn reasoning XP</span></h3>
        <p className="mt-1 text-xs text-[var(--muted)]">React with a reason (governance vocabulary, not like/dislike):</p>
        <div className="mt-2 flex flex-wrap gap-2">
          {["Workable", "Unworkable", "Addresses the problem", "Dodges it", "Fair tradeoff", "Hidden cost"].map((label) => (
            <button key={label} onClick={() => act("ReactionWithReason", label)} disabled={busy}
              className="rounded-full border border-[var(--line)] px-3 py-1 text-xs hover:border-[var(--accent)] disabled:opacity-50">
              {label}
            </button>
          ))}
        </div>
        <div className="mt-3">
          <label className="text-xs text-[var(--muted)]">Steelman (state the strongest case the other side would accept):</label>
          <textarea value={steelText} onChange={(e) => setSteelText(e.target.value)} rows={2}
            className="mt-1 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm" />
          <button onClick={() => { if (steelText.trim()) { void act("Steelman", steelText); setSteelText(""); } }}
            disabled={busy || !steelText.trim()}
            className="mt-1 rounded-full border border-[var(--line)] px-3 py-1 text-xs font-semibold disabled:opacity-50">
            Submit steelman
          </button>
        </div>
      </div>

      {d.outcome && (
        <div className="mt-4 rounded-2xl border border-emerald-300 bg-emerald-50 p-4 text-sm text-emerald-900">
          <strong>{d.outcome.finalState}.</strong>{" "}
          {d.outcome.finalState === "Passed" && (
            <>Coalition of {d.outcome.signers?.join(", ")} — breadth {d.outcome.coveredBuckets}, teeth{" "}
              {d.outcome.specificity}, {d.outcome.movedSigners} moved.</>
          )}
          {d.outcome.diedReason && <>{d.outcome.diedReason}</>}
        </div>
      )}

      {/* Agent ballast */}
      <div className="mt-6 flex flex-wrap items-center gap-3">
        <button
          onClick={() => run(() => agentStep(id))}
          disabled={busy || resolved}
          className="inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
        >
          <Bot size={16} /> Run agents (one round)
        </button>
        {!d.youJoined && !resolved && (
          <div className="inline-flex items-center gap-2">
            <select
              value={bucket}
              onChange={(e) => setBucket(e.target.value)}
              className="rounded-full border border-[var(--line)] px-3 py-1.5 text-sm"
            >
              {["left", "center", "right"].map((b) => <option key={b} value={b}>{b}</option>)}
            </select>
            <button
              onClick={() => run(() => joinProvision(id, bucket))}
              disabled={busy}
              className="rounded-full border border-[var(--line)] px-4 py-1.5 text-sm font-semibold"
            >
              Join as {bucket}
            </button>
          </div>
        )}
      </div>

      {/* Sub-questions */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Sub-questions</h2>
      <ul className="mt-2 grid gap-2">
        {d.subQuestions.map((sq) => (
          <li key={sq.key} className="rounded-xl border border-[var(--line)] p-3 text-sm">
            <p className="font-medium">{sq.prompt}</p>
            <p className="mt-1 text-xs text-[var(--muted)]">
              {sq.key} · options: {sq.options.join(" / ") || "(free)"}
            </p>
          </li>
        ))}
      </ul>

      {/* Versions */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Versions</h2>
      <ul className="mt-2 grid gap-2">
        {d.versions.map((v) => (
          <li key={v.id} className="rounded-xl border border-[var(--line)] p-3 text-sm">
            <div className="flex items-center justify-between">
              <span className="font-medium">{v.label ?? "version"}</span>
              <span className="text-xs text-[var(--muted)]">✓ {v.accepts} · ✕ {v.declines}</span>
            </div>
            <p className="mt-1 text-xs text-[var(--muted)]">
              {Object.entries(v.positions).map(([k, val]) => `${k} = ${val}`).join("; ") || "(no positions)"}
            </p>
            {!resolved && (
              <div className="mt-2 flex gap-2">
                <button
                  onClick={() => run(() => castAcceptance(id, v.id, true))}
                  disabled={busy}
                  className="inline-flex items-center gap-1 rounded-full bg-emerald-600 px-3 py-1 text-xs font-semibold text-white disabled:opacity-50"
                >
                  <Check size={12} /> Co-sign
                </button>
                <button
                  onClick={() => run(() => castAcceptance(id, v.id, false))}
                  disabled={busy}
                  className="inline-flex items-center gap-1 rounded-full border border-[var(--line)] px-3 py-1 text-xs font-semibold disabled:opacity-50"
                >
                  <X size={12} /> Decline
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>

      {/* Acts */}
      {!resolved && (
        <div className="mt-8 grid gap-6 md:grid-cols-2">
          <div className="rounded-2xl border border-[var(--line)] p-4">
            <h3 className="text-sm font-semibold">Take a position</h3>
            <input
              value={stance}
              onChange={(e) => setStance(e.target.value)}
              placeholder="e.g. for, but only with a carve-out"
              className="mt-2 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm"
            />
            <button
              onClick={() => run(() => takePosition(id, { stance, intensity: "Medium", bucket }))}
              disabled={busy || !stance.trim()}
              className="mt-3 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
            >
              Post position
            </button>
          </div>

          <div className="rounded-2xl border border-[var(--line)] p-4">
            <h3 className="text-sm font-semibold">Propose a carve-out</h3>
            <p className="mt-1 text-xs text-[var(--muted)]">Write it in your own words — it's extracted into positions:</p>
            <textarea value={freeText} onChange={(e) => setFreeText(e.target.value)} rows={2}
              placeholder="I'd sign it if existing facilities are exempt and it stays large-only."
              className="mt-1 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm" />
            <button
              onClick={() => { if (freeText.trim()) { void run(() => proposeFreeformAmendment(id, freeText)); setFreeText(""); } }}
              disabled={busy || !freeText.trim()}
              className="mt-1 rounded-full bg-[var(--accent)] px-4 py-1.5 text-xs font-semibold text-white disabled:opacity-50">
              Propose (free-form)
            </button>
            <p className="mt-3 text-xs text-[var(--muted)]">…or pick a position on each sub-question.</p>
            {d.subQuestions.map((sq) => (
              <div key={sq.key} className="mt-2">
                <label className="text-xs text-[var(--muted)]">{sq.key}</label>
                <select
                  value={carveOut[sq.key] ?? ""}
                  onChange={(e) => setCarveOut({ ...carveOut, [sq.key]: e.target.value })}
                  className="mt-1 w-full rounded-lg border border-[var(--line)] px-2 py-1.5 text-sm"
                >
                  <option value="">(leave silent)</option>
                  {sq.options.map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
            ))}
            <button
              onClick={() => {
                const positions = Object.fromEntries(
                  Object.entries(carveOut).filter(([, v]) => v),
                );
                if (Object.keys(positions).length > 0)
                  void run(() => proposeAmendment(id, positions, "carve-out"));
              }}
              disabled={busy}
              className="mt-3 rounded-full border border-[var(--line)] px-4 py-2 text-sm font-semibold disabled:opacity-50"
            >
              Propose amendment
            </button>
          </div>
        </div>
      )}

      {/* Participants */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Participants</h2>
      <ul className="mt-2 flex flex-wrap gap-2">
        {d.participants.map((p) => (
          <li key={p.userId} className="rounded-full border border-[var(--line)] px-3 py-1 text-xs">
            {p.isAgent ? <Bot size={11} className="mr-1 inline" /> : null}
            {p.isAgent ? p.userId.replace("agent:", "") : "you"} · {p.bucket}
            {p.hasPositioned ? " ✓" : ""}
          </li>
        ))}
      </ul>
    </section>
  );
}
