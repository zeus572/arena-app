import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ArrowRight, Bot, Sparkles } from "lucide-react";
import {
  getFramings,
  joinProvision,
  recordAct,
  castAcceptance,
  agentStep,
  type ProvisionDetail,
  type Framings,
} from "@/api/coalition";
import { useProvision } from "../hooks/useProvision";

const REASON_LABELS = [
  "Workable",
  "Unworkable",
  "Addresses the problem",
  "Dodges it",
  "Fair tradeoff",
  "Hidden cost",
];

function SpectrumBarView({ d }: { d: ProvisionDetail }) {
  const bar = d.spectrumBar;
  const empty = bar.coveredBuckets === 0;
  return (
    <div className="rounded-2xl border border-[var(--line)] p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-xs text-[var(--muted)]">
        <span className="font-semibold uppercase tracking-wider">Coalition status</span>
        <span>
          distance {(bar.distance * 100).toFixed(0)}% · breadth {bar.coveredBuckets}/{bar.totalBuckets}
        </span>
      </div>

      <div className="flex gap-1.5">
        {bar.cells.map((c) => (
          <div key={c.bucket} className="flex flex-1 flex-col items-center gap-1">
            <div
              className="h-7 w-full rounded"
              style={{ background: c.covered ? "var(--accent)" : "var(--line)" }}
              title={`${c.bucket} — ${c.covered ? "covered" : "open"}`}
            />
            <span className="block text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
              {c.bucket}
            </span>
            <span className={`text-[9px] uppercase tracking-wider ${c.covered ? "text-[var(--accent)]" : "text-[var(--muted)]"}`}>
              {c.covered ? "covered" : "open"}
            </span>
          </div>
        ))}
      </div>

      {/* Legend */}
      <div className="mt-3 flex items-center gap-4 text-[10px] uppercase tracking-wider text-[var(--muted)]">
        <span className="flex items-center gap-1">
          <span className="inline-block h-2.5 w-2.5 rounded" style={{ background: "var(--accent)" }} /> covered
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-2.5 w-2.5 rounded" style={{ background: "var(--line)" }} /> open
        </span>
      </div>

      {empty ? (
        <p className="mt-3 text-xs text-[var(--muted)]">
          No coalition reach yet — take a position to start covering the spectrum.
        </p>
      ) : (
        <p className="mt-3 text-xs font-semibold text-[var(--accent)]">{bar.callToAction}</p>
      )}

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
  const { d, reload, run, busy } = useProvision(id);
  const [bucket, setBucket] = useState("left");
  const [steelOpen, setSteelOpen] = useState(false);
  const [steelText, setSteelText] = useState("");
  const [framings, setFramings] = useState<Framings | null>(null);
  // The most recently awarded act — drives the "dim until earned, then light up" XP hints.
  const [lastAward, setLastAward] = useState<{ key: string; points: number; currency: string } | null>(null);

  useEffect(() => { void getFramings(id).then(setFramings).catch(() => {}); }, [id]);

  async function act(type: string, key: string, payload?: string) {
    try {
      const r = await recordAct(id, type, payload);
      setLastAward({ key, points: r.points, currency: r.currency });
      reload();
    } catch {
      /* swallow — busy guard handled per-button */
    }
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

      {/* Framing */}
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

      {/* Coalition status */}
      <div className="mt-6"><SpectrumBarView d={d} /></div>

      {/* Participate CTA */}
      {!resolved && (
        <Link
          to={`/coalition/${id}/participate`}
          data-testid="participate-cta"
          className="mt-4 flex items-center justify-between gap-3 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-4 transition hover:bg-[var(--accent)]/10"
        >
          <div>
            <p className="text-sm font-semibold text-[var(--accent)]">Take part in this coalition</p>
            <p className="mt-0.5 text-xs text-[var(--muted)]">Answer the sub-questions, co-sign versions, take a position, or propose a carve-out.</p>
          </div>
          <ArrowRight size={18} className="shrink-0 text-[var(--accent)]" />
        </Link>
      )}

      {/* Bridge probes */}
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

      {/* Daily acts — gamified */}
      <div className="mt-4 rounded-2xl border border-[var(--line)] p-4">
        <div className="flex items-center justify-between gap-2">
          <h3 className="flex items-center gap-2 text-sm font-semibold">
            <Sparkles size={16} className="text-[var(--accent)]" /> Daily acts
          </h3>
          <span className="rounded-full bg-[var(--accent)]/10 px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
            earn reasoning XP
          </span>
        </div>
        <p className="mt-1 text-xs text-[var(--muted)]">React with a reason (governance vocabulary, not like/dislike):</p>

        <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">
          {REASON_LABELS.map((label) => {
            const lit = lastAward?.key === label;
            return (
              <button
                key={label}
                onClick={() => act("ReactionWithReason", label, label)}
                disabled={busy}
                className={`flex flex-col items-start gap-1 rounded-xl border p-3 text-left text-xs font-medium transition disabled:opacity-50 ${
                  lit ? "border-emerald-400 bg-emerald-50" : "border-[var(--line)] hover:border-[var(--accent)]"
                }`}
              >
                <span>{label}</span>
                <span
                  className={`text-[10px] font-semibold uppercase tracking-wider transition-colors ${
                    lit ? "text-emerald-600" : "text-[var(--muted)]"
                  }`}
                >
                  {lit ? `+${lastAward.points} ${lastAward.currency} XP` : "+ XP"}
                </span>
              </button>
            );
          })}
        </div>

        {/* Steelman — collapsed until requested */}
        <div className="mt-3 border-t border-[var(--line)] pt-3">
          {!steelOpen ? (
            <button
              onClick={() => setSteelOpen(true)}
              className="inline-flex items-center gap-2 rounded-full border border-[var(--line)] px-3 py-1.5 text-xs font-semibold hover:border-[var(--accent)]"
            >
              Add a steelman
              <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">+ XP</span>
            </button>
          ) : (
            <div>
              <label className="text-xs text-[var(--muted)]">
                Steelman (state the strongest case the other side would accept):
              </label>
              <textarea
                value={steelText}
                onChange={(e) => setSteelText(e.target.value)}
                rows={3}
                autoFocus
                className="mt-1 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm"
              />
              <div className="mt-2 flex items-center gap-2">
                <button
                  onClick={() => {
                    if (steelText.trim()) {
                      void act("Steelman", "steelman", steelText);
                      setSteelText("");
                      setSteelOpen(false);
                    }
                  }}
                  disabled={busy || !steelText.trim()}
                  className="inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-4 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                >
                  Submit steelman
                  <span
                    className={`text-[10px] font-semibold uppercase tracking-wider ${
                      lastAward?.key === "steelman" ? "text-white" : "text-white/70"
                    }`}
                  >
                    {lastAward?.key === "steelman" ? `+${lastAward.points} XP` : "+ XP"}
                  </span>
                </button>
                <button
                  onClick={() => { setSteelOpen(false); setSteelText(""); }}
                  className="rounded-full px-3 py-1.5 text-xs font-semibold text-[var(--muted)] hover:text-[var(--fg)]"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Outcome */}
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

      {/* Join + agent ballast */}
      <div className="mt-6 flex flex-wrap items-center gap-3">
        {import.meta.env.DEV && (
          <button
            onClick={() => run(() => agentStep(id))}
            disabled={busy || resolved}
            className="inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            <Bot size={16} /> Run agents (dev)
          </button>
        )}
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

      {/* Participants */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Participants</h2>
      {d.participants.length === 0 ? (
        <p className="mt-2 text-sm text-[var(--muted)]">No one has joined yet.</p>
      ) : (
        <ul className="mt-2 flex flex-wrap gap-2">
          {d.participants.map((p) => (
            <li key={p.userId} className="inline-flex items-center gap-1 rounded-full border border-[var(--line)] px-3 py-1 text-xs">
              {p.isAgent ? <Bot size={11} className="inline" /> : null}
              <span className="font-medium">{p.isAgent ? p.userId.replace("agent:", "") : "you"}</span>
              <span className="text-[var(--muted)]">· joined as {p.bucket}</span>
              {p.hasPositioned && <span className="text-emerald-600">✓</span>}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
