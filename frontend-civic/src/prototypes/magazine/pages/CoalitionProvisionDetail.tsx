import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ArrowRight, Bot, Compass, Sparkles, ScrollText } from "lucide-react";
import {
  getFramings,
  joinProvision,
  recordAct,
  castAcceptance,
  agentStep,
  type ProvisionDetail,
  type Framings,
} from "@/api/coalition";
import { getMyProfile, type Profile } from "@/api/profile";
import { deriveCompassPosition, prettyBucket } from "@/lib/compass";
import { useProvision } from "../hooks/useProvision";
import { Button, ButtonLink } from "../components/Button";

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
  const [steelOpen, setSteelOpen] = useState(false);
  const [steelText, setSteelText] = useState("");
  const [framings, setFramings] = useState<Framings | null>(null);
  const [profile, setProfile] = useState<Profile | null>(null);
  // The most recently awarded act — drives the "dim until earned, then light up" XP hints.
  const [lastAward, setLastAward] = useState<{ key: string; points: number; currency: string } | null>(null);

  useEffect(() => { void getFramings(id).then(setFramings).catch(() => {}); }, [id]);
  useEffect(() => { void getMyProfile().then(setProfile).catch(() => {}); }, []);

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

  // The position this person speaks for now comes from their Civic Compass, not a
  // left/center/right self-label.
  const compass = deriveCompassPosition(profile);

  // The current prevailing coalition wording: the leading version (by coalition reach),
  // falling back to the highest net-cosigned version, then the neutral starting text.
  const leadingVersion =
    d.versions.find((v) => v.id === d.spectrumBar.leadingVersionId) ??
    [...d.versions].sort(
      (a, b) => b.accepts - b.declines - (a.accepts - a.declines),
    )[0] ??
    null;
  const hasAgreedWording =
    !!leadingVersion &&
    !!leadingVersion.text &&
    !leadingVersion.text.trimStart().startsWith("Version —");
  const prevailingText = hasAgreedWording ? leadingVersion!.text.trim() : d.neutralText;

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

      {/* Current prevailing coalition position */}
      <div
        className="mt-6 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-4"
        data-testid="prevailing-position"
      >
        <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
          <ScrollText size={14} /> Prevailing coalition position
        </p>
        <p className="mt-1.5 text-[15px] font-medium leading-snug text-[var(--fg)]">
          {prevailingText}
        </p>
        {hasAgreedWording ? (
          <p className="mt-1.5 text-xs text-[var(--muted)]">
            Leading wording · {leadingVersion!.accepts} co-sign{leadingVersion!.accepts === 1 ? "" : "s"}
            {leadingVersion!.declines > 0 && ` · ${leadingVersion!.declines} decline${leadingVersion!.declines === 1 ? "" : "s"}`}
          </p>
        ) : (
          <p className="mt-1.5 text-xs text-[var(--muted)]">
            No agreed wording yet — this is the neutral starting point. Take a position to move it.
          </p>
        )}
      </div>

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
              <Button variant="positive" size="sm" onClick={() => run(() => castAcceptance(id, pr.versionId, true))} disabled={busy}>
                Co-sign
              </Button>
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
            <Button variant="ghost" size="sm" onClick={() => setSteelOpen(true)}>
              Add a steelman
              <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">+ XP</span>
            </Button>
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
                <Button
                  size="sm"
                  onClick={() => {
                    if (steelText.trim()) {
                      void act("Steelman", "steelman", steelText);
                      setSteelText("");
                      setSteelOpen(false);
                    }
                  }}
                  disabled={busy || !steelText.trim()}
                >
                  Submit steelman
                  <span
                    className={`text-[10px] font-semibold uppercase tracking-wider ${
                      lastAward?.key === "steelman" ? "text-white" : "text-white/70"
                    }`}
                  >
                    {lastAward?.key === "steelman" ? `+${lastAward.points} XP` : "+ XP"}
                  </span>
                </Button>
                <Button
                  variant="link"
                  size="sm"
                  onClick={() => { setSteelOpen(false); setSteelText(""); }}
                >
                  Cancel
                </Button>
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
          <Button
            onClick={() => run(() => agentStep(id))}
            disabled={busy || resolved}
          >
            <Bot size={16} /> Run agents (dev)
          </Button>
        )}
        {!d.youJoined && !resolved && (
          <div
            className="flex flex-col gap-2 rounded-2xl border border-[var(--line)] p-3 sm:flex-row sm:items-center sm:gap-3"
            data-testid="compass-join"
          >
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
                <Compass size={13} /> Your Civic Compass position
              </p>
              <p className="text-sm font-semibold" data-testid="compass-join-label">{compass.label}</p>
              <p className="text-xs text-[var(--muted)]">{compass.detail}</p>
            </div>
            {compass.hasData ? (
              <Button
                onClick={() => run(() => joinProvision(id, compass.bucket))}
                disabled={busy}
                className="shrink-0"
                data-testid="compass-join-button"
              >
                Join with this position
              </Button>
            ) : (
              <ButtonLink
                to="/onboarding"
                variant="secondary"
                className="shrink-0"
                data-testid="compass-join-build"
              >
                Build your Compass →
              </ButtonLink>
            )}
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
              <span className="text-[var(--muted)]">· {prettyBucket(p.bucket)}</span>
              {p.hasPositioned && <span className="text-emerald-600">✓</span>}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
