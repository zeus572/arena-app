import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Check, X, Flag, Scissors, Compass } from "lucide-react";
import {
  takePosition,
  proposeAmendment,
  proposeFreeformAmendment,
  castAcceptance,
  type CoalitionSubQuestion,
  type ProvisionDetail,
} from "@/api/coalition";
import { getMyProfile, type Profile } from "@/api/profile";
import { deriveCompassPosition } from "@/lib/compass";
import { useProvision } from "../hooks/useProvision";
import Flyout from "../components/Flyout";

export default function CoalitionProvisionParticipate() {
  const { id = "" } = useParams();
  const { d, run, busy } = useProvision(id);

  const [positionOpen, setPositionOpen] = useState(false);
  const [carveOpen, setCarveOpen] = useState(false);
  const [stance, setStance] = useState("");
  const [profile, setProfile] = useState<Profile | null>(null);
  const [freeText, setFreeText] = useState("");
  const [carveOut, setCarveOut] = useState<Record<string, string>>({});

  useEffect(() => { void getMyProfile().then(setProfile).catch(() => {}); }, []);

  if (!d) return <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>;

  // Speak for the position discovered through your Civic Compass, not a partisan label.
  const compass = deriveCompassPosition(profile);

  const resolved = ["Passed", "Forked", "Died"].includes(d.state);
  // key -> prompt, so versions can spell out each position the way sub-questions do.
  const promptByKey = new Map<string, string>(d.subQuestions.map((sq) => [sq.key, sq.prompt]));

  async function runAndClose(fn: () => Promise<ProvisionDetail>, close: () => void) {
    await run(fn);
    close();
  }

  return (
    <section data-testid="coalition-participate" className="max-w-3xl">
      <Link to={`/coalition/${id}`} className="inline-flex items-center gap-1 text-xs text-[var(--muted)] hover:text-[var(--fg)]">
        <ArrowLeft size={14} /> {d.title}
      </Link>

      <header className="mt-3 flex items-center justify-between gap-3">
        <h1 className="display text-3xl">Participate</h1>
        <span className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{d.state}</span>
      </header>
      <p className="mt-2 text-sm text-[var(--fg-soft)]">
        Answer the sub-questions, co-sign the versions that bridge the gap, or contribute your own.
      </p>

      {/* Action buttons → flyouts */}
      {!resolved && (
        <div className="mt-5 flex flex-wrap gap-3">
          <button
            onClick={() => setPositionOpen(true)}
            data-testid="open-take-position"
            className="inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white"
          >
            <Flag size={15} /> Take a position
          </button>
          <button
            onClick={() => setCarveOpen(true)}
            data-testid="open-propose-carveout"
            className="inline-flex items-center gap-2 rounded-full border border-[var(--accent)] px-4 py-2 text-sm font-semibold text-[var(--accent)]"
          >
            <Scissors size={15} /> Propose a carve-out
          </button>
        </div>
      )}

      {/* Sub-questions */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Sub-questions</h2>
      <ul className="mt-2 grid gap-2">
        {d.subQuestions.map((sq) => (
          <li key={sq.key} className="rounded-xl border border-[var(--line)] p-4 text-sm">
            <p className="font-medium">{sq.prompt}</p>
            {sq.tradeoff && <p className="mt-1 text-xs text-[var(--fg-soft)]">Tradeoff: {sq.tradeoff}</p>}
            <div className="mt-2 flex flex-wrap gap-1.5">
              {sq.options.length > 0 ? (
                sq.options.map((o) => (
                  <span key={o} className="rounded-full bg-[var(--line)] px-2.5 py-0.5 text-[11px] font-medium text-[var(--fg-soft)]">
                    {o}
                  </span>
                ))
              ) : (
                <span className="text-[11px] text-[var(--muted)]">free response</span>
              )}
            </div>
          </li>
        ))}
      </ul>

      {/* Versions — richer, spelled-out */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Versions</h2>
      <ul className="mt-2 grid gap-3">
        {d.versions.length === 0 && (
          <li className="rounded-2xl border border-dashed border-[var(--line)] p-4 text-sm text-[var(--muted)]">
            No versions yet — take a position or propose a carve-out to start one.
          </li>
        )}
        {d.versions.map((v) => {
          const positions = Object.entries(v.positions);
          // Auto-generated versions store a "Version — key = val; …" dump in `text`, which just
          // repeats the spelled-out breakdown below. Show a short summary instead; only render
          // `text` verbatim when it's a real freeform proposal.
          const isAutoText = !v.text || v.text.trimStart().startsWith("Version —");
          const summary = isAutoText
            ? positions.length > 0
              ? `A position on ${positions.length} question${positions.length === 1 ? "" : "s"}`
              : null
            : v.text;
          return (
            <li key={v.id} className="rounded-2xl border border-[var(--line)] p-4 text-sm">
              <div className="flex items-start justify-between gap-3">
                <span className="rounded-full bg-[var(--accent)]/10 px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
                  {v.label ?? "proposal"}
                </span>
                <span className="flex shrink-0 items-center gap-3 text-xs text-[var(--muted)]">
                  <span className="text-emerald-600">✓ {v.accepts} co-sign{v.accepts === 1 ? "" : "s"}</span>
                  <span>✕ {v.declines} decline{v.declines === 1 ? "" : "s"}</span>
                </span>
              </div>

              {/* Freeform proposals render verbatim; synthetic versions get a short summary,
                  since the full breakdown is spelled out under "Where this version lands". */}
              {summary && (
                <p className={`mt-2 leading-snug ${isAutoText ? "text-[13px] text-[var(--muted)]" : "text-[15px] font-medium"}`}>
                  {summary}
                </p>
              )}

              {positions.length > 0 && (
                <>
                  <p className="mt-3 text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">Where this version lands</p>
                  <div className="mt-1.5 grid gap-1.5">
                    {positions.map(([k, val]) => (
                      <div key={k} className="rounded-lg bg-[var(--line)]/40 px-3 py-2">
                        <p className="text-[11px] uppercase tracking-wider text-[var(--muted)]">{promptByKey.get(k) ?? k}</p>
                        <p className="mt-0.5 font-medium">{val}</p>
                      </div>
                    ))}
                  </div>
                </>
              )}

              {!resolved && (
                <div className="mt-3 flex gap-2">
                  <button
                    onClick={() => run(() => castAcceptance(id, v.id, true))}
                    disabled={busy}
                    className="inline-flex items-center gap-1 rounded-full bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                  >
                    <Check size={12} /> Co-sign
                  </button>
                  <button
                    onClick={() => run(() => castAcceptance(id, v.id, false))}
                    disabled={busy}
                    className="inline-flex items-center gap-1 rounded-full border border-[var(--line)] px-3 py-1.5 text-xs font-semibold disabled:opacity-50"
                  >
                    <X size={12} /> Decline
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {/* Take a position flyout */}
      <Flyout
        open={positionOpen}
        onClose={() => setPositionOpen(false)}
        title="Take a position"
        subtitle="Say where you stand and which part of the spectrum you speak for."
      >
        <label className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">Your stance</label>
        <input
          value={stance}
          onChange={(e) => setStance(e.target.value)}
          placeholder="e.g. for, but only with a carve-out"
          className="mt-1 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm"
        />

        <p className="mt-4 block text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">Speaking for</p>
        <div
          className="mt-1 flex items-center gap-2 rounded-lg border border-[var(--line)] px-3 py-2"
          data-testid="participate-compass"
        >
          <Compass size={15} className="shrink-0 text-[var(--accent)]" />
          <div className="min-w-0">
            <p className="text-sm font-semibold">{compass.label}</p>
            <p className="text-[11px] text-[var(--muted)]">{compass.detail}</p>
          </div>
        </div>
        {!compass.hasData && (
          <Link to="/onboarding" className="mt-1 inline-block text-xs font-semibold text-[var(--accent)]">
            Build your Civic Compass →
          </Link>
        )}

        <button
          onClick={() =>
            runAndClose(
              () => takePosition(id, { stance, intensity: "Medium", bucket: compass.bucket }),
              () => { setPositionOpen(false); setStance(""); },
            )
          }
          disabled={busy || !stance.trim()}
          className="mt-5 w-full rounded-full bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
        >
          Post position
        </button>
      </Flyout>

      {/* Propose a carve-out flyout */}
      <Flyout
        open={carveOpen}
        onClose={() => setCarveOpen(false)}
        title="Propose a carve-out"
        subtitle="Offer an amendment that could pull a broader coalition together."
      >
        <label className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">In your own words</label>
        <p className="mt-0.5 text-xs text-[var(--muted)]">It's extracted into structured positions automatically.</p>
        <textarea
          value={freeText}
          onChange={(e) => setFreeText(e.target.value)}
          rows={3}
          placeholder="I'd sign it if existing facilities are exempt and it stays large-only."
          className="mt-1 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm"
        />
        <button
          onClick={() =>
            runAndClose(
              () => proposeFreeformAmendment(id, freeText),
              () => { setCarveOpen(false); setFreeText(""); },
            )
          }
          disabled={busy || !freeText.trim()}
          className="mt-2 w-full rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
        >
          Propose (free-form)
        </button>

        <div className="my-5 flex items-center gap-3 text-[10px] uppercase tracking-wider text-[var(--muted)]">
          <span className="h-px flex-1 bg-[var(--line)]" /> or pick per sub-question <span className="h-px flex-1 bg-[var(--line)]" />
        </div>

        {d.subQuestions.map((sq: CoalitionSubQuestion) => (
          <div key={sq.key} className="mb-3">
            <label className="text-xs font-medium">{sq.prompt}</label>
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
            const positions = Object.fromEntries(Object.entries(carveOut).filter(([, val]) => val));
            if (Object.keys(positions).length > 0)
              void runAndClose(
                () => proposeAmendment(id, positions, "carve-out"),
                () => { setCarveOpen(false); setCarveOut({}); },
              );
          }}
          disabled={busy}
          className="mt-2 w-full rounded-full border border-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-[var(--accent)] disabled:opacity-50"
        >
          Propose amendment
        </button>
      </Flyout>
    </section>
  );
}
