import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Check, X, Compass, ScrollText, Sparkles, PenLine } from "lucide-react";
import {
  proposeAmendment,
  proposeFreeformAmendment,
  castAcceptance,
  joinProvision,
  type CoalitionVersion,
} from "@/api/coalition";
import { getMyProfile, type Profile } from "@/api/profile";
import { deriveCompassPosition } from "@/lib/compass";
import { useAuth } from "@/auth/AuthContext";
import { SignInPrompt } from "../components/SignInPrompt";
import { useProvision } from "../hooks/useProvision";

/** How well a version matches the answers the user has chosen so far. */
function closeness(answers: Record<string, string>, v: CoalitionVersion) {
  const keys = Object.keys(answers).filter((k) => answers[k]);
  let matches = 0;
  for (const k of keys) {
    const vp = v.positions[k];
    if (vp && vp.toLowerCase() === answers[k].toLowerCase()) matches++;
  }
  return { matches, considered: keys.length, score: keys.length ? matches / keys.length : 0 };
}

export default function CoalitionProvisionParticipate() {
  const { id = "" } = useParams();
  const { isAuthenticated } = useAuth();
  const { d, run, busy } = useProvision(id);

  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [saved, setSaved] = useState(false);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [freeOpen, setFreeOpen] = useState(false);
  const [freeText, setFreeText] = useState("");

  useEffect(() => { void getMyProfile().then(setProfile).catch(() => {}); }, []);

  const answeredKeys = useMemo(() => Object.keys(answers).filter((k) => answers[k]), [answers]);

  if (!d) return <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>;

  const resolved = ["Passed", "Forked", "Died"].includes(d.state);
  const promptByKey = new Map<string, string>(d.subQuestions.map((sq) => [sq.key, sq.prompt]));
  const compass = deriveCompassPosition(profile);

  // Versions a cohort member presented vs. the neutral starting drafts the system seeded.
  const presented = d.versions.filter((v) => v.authorUserId);
  const drafts = d.versions.filter((v) => !v.authorUserId);
  const noOneHasPresented = presented.length === 0;

  // Rank existing versions by how close they are to the user's chosen answers.
  const ranked = [...d.versions]
    .map((v) => ({ v, ...closeness(answers, v) }))
    .sort((a, b) => b.score - a.score || b.v.accepts - a.v.accepts);

  function pick(key: string, option: string) {
    setAnswers((prev) => ({ ...prev, [key]: prev[key] === option ? "" : option }));
    setSaved(false);
  }

  async function present() {
    if (!isAuthenticated) return;
    const positions = Object.fromEntries(answeredKeys.map((k) => [k, answers[k]]));
    if (Object.keys(positions).length === 0) return;
    const label = noOneHasPresented ? "first reading" : "carve-out";
    await run(async () => {
      // Join with your Civic Compass position the first time you act on this bill.
      if (!d!.youJoined && compass.hasData) await joinProvision(id, compass.bucket);
      return proposeAmendment(id, positions, label);
    });
    setAnswers({});
    setSaved(false);
  }

  return (
    <section data-testid="coalition-participate" className="max-w-3xl">
      <Link to={`/coalition/${id}`} className="inline-flex items-center gap-1 text-xs text-[var(--muted)] hover:text-[var(--fg)]">
        <ArrowLeft size={14} /> {d.title}
      </Link>

      <header className="mt-3 flex items-center justify-between gap-3">
        <h1 className="display text-3xl">Take your position</h1>
        <span className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{d.state}</span>
      </header>
      <p className="mt-2 text-sm text-[var(--fg-soft)]">
        Answer each question below to say where you stand. When you save, we'll show you which existing
        versions are closest — and let you put your own on the table.
      </p>

      {noOneHasPresented && !resolved && (
        <div className="mt-4 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-4" data-testid="be-first-banner">
          <p className="flex items-center gap-2 text-sm font-semibold text-[var(--accent)]">
            <Sparkles size={16} /> No one in your cohort has presented this bill yet.
          </p>
          <p className="mt-1 text-xs text-[var(--muted)]">
            Set your answers below and be the first to put a version on the table for everyone to react to.
          </p>
        </div>
      )}

      {/* Sub-questions — answer inline */}
      <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Your answers</h2>
      <ul className="mt-2 grid gap-3" data-testid="subquestion-cards">
        {d.subQuestions.map((sq) => (
          <li key={sq.key} className="rounded-2xl border border-[var(--line)] p-4">
            <p className="font-medium">{sq.prompt}</p>
            {sq.tradeoff && <p className="mt-1 text-xs text-[var(--fg-soft)]">Tradeoff: {sq.tradeoff}</p>}
            {sq.options.length > 0 ? (
              <div className="mt-3 flex flex-wrap gap-2">
                {sq.options.map((o) => {
                  const selected = answers[sq.key] === o;
                  return (
                    <button
                      key={o}
                      type="button"
                      disabled={resolved}
                      onClick={() => pick(sq.key, o)}
                      data-testid={`opt-${sq.key}-${o}`}
                      aria-pressed={selected}
                      className={`rounded-full border-2 px-4 py-1.5 text-sm font-semibold transition disabled:opacity-50 ${
                        selected
                          ? "border-[var(--accent)] bg-[var(--accent)] text-white"
                          : "border-[var(--line)] bg-[var(--bg-elev)] text-[var(--fg)] hover:border-[var(--accent)]"
                      }`}
                    >
                      {o}
                    </button>
                  );
                })}
              </div>
            ) : (
              <p className="mt-2 text-[11px] text-[var(--muted)]">Open question — describe your take in your own words below.</p>
            )}
          </li>
        ))}
      </ul>

      {/* Sign-in gate: anyone can pick answers, but saving/co-signing needs an account. */}
      {!resolved && !isAuthenticated && (
        <div className="mt-5" data-testid="participate-signin">
          <SignInPrompt
            compact
            title="Sign in to save your position"
            message="Pick your answers above — then sign in to save a version and co-sign bills."
          />
        </div>
      )}

      {/* Save → compare + present */}
      {!resolved && isAuthenticated && (
        <div className="mt-5">
          {!saved ? (
            <button
              type="button"
              onClick={() => setSaved(true)}
              disabled={answeredKeys.length === 0}
              data-testid="save-answers"
              className="w-full rounded-full bg-[var(--accent)] py-3 text-sm font-semibold text-white disabled:opacity-50"
            >
              Save my answers & see where I land
            </button>
          ) : (
            <div className="rounded-2xl border border-[var(--line)] p-4" data-testid="compare-panel">
              <div className="flex items-center gap-2">
                <ScrollText size={16} className="text-[var(--accent)]" />
                <h3 className="text-sm font-semibold">How your answers compare</h3>
              </div>
              {compass.hasData && (
                <p className="mt-1 flex items-center gap-1 text-[11px] text-[var(--muted)]">
                  <Compass size={12} className="text-[var(--accent)]" /> Presenting as {compass.label}
                </p>
              )}

              {d.versions.length === 0 ? (
                <p className="mt-3 text-xs text-[var(--muted)]">No versions yet — yours will be the first.</p>
              ) : (
                <ul className="mt-3 grid gap-2" data-testid="closeness-list">
                  {ranked.map(({ v, matches, considered }) => (
                    <li key={v.id} className="rounded-xl border border-[var(--line)] p-3 text-sm">
                      <div className="flex items-center justify-between gap-3">
                        <span className="flex items-center gap-2">
                          <span className="rounded-full bg-[var(--accent)]/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
                            {v.authorUserId ? v.label ?? "version" : `draft · ${v.label ?? "starting point"}`}
                          </span>
                          <span className="text-xs font-semibold text-[var(--accent)]" data-testid={`match-${v.id}`}>
                            {considered > 0 ? `${matches}/${considered} of your answers match` : "—"}
                          </span>
                        </span>
                        <span className="shrink-0 text-xs text-[var(--muted)]">✓ {v.accepts} · ✕ {v.declines}</span>
                      </div>
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
                    </li>
                  ))}
                </ul>
              )}

              <button
                onClick={present}
                disabled={busy || answeredKeys.length === 0}
                data-testid="present-version"
                className="mt-4 w-full rounded-full bg-[var(--accent)] py-2.5 text-sm font-semibold text-white disabled:opacity-50"
              >
                {noOneHasPresented ? "Present this as the bill" : "Propose this as a carve-out"}
              </button>
              <p className="mt-1.5 text-center text-[11px] text-[var(--muted)]">
                Puts your answers on the table as a version others can co-sign.
              </p>
            </div>
          )}
        </div>
      )}

      {/* Secondary: describe in your own words (free-form) — also account-gated. */}
      {!resolved && isAuthenticated && (
        <div className="mt-4">
          {!freeOpen ? (
            <button
              type="button"
              onClick={() => setFreeOpen(true)}
              className="inline-flex items-center gap-2 text-xs font-semibold text-[var(--muted)] hover:text-[var(--accent)]"
            >
              <PenLine size={13} /> Or describe your position in your own words
            </button>
          ) : (
            <div className="rounded-2xl border border-[var(--line)] p-4">
              <label className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">In your own words</label>
              <p className="mt-0.5 text-xs text-[var(--muted)]">We'll extract it into structured answers automatically.</p>
              <textarea
                value={freeText}
                onChange={(e) => setFreeText(e.target.value)}
                rows={3}
                placeholder="I'd sign it if existing facilities are exempt and it stays large-only."
                className="mt-2 w-full rounded-lg border border-[var(--line)] px-3 py-2 text-sm"
              />
              <div className="mt-2 flex items-center gap-2">
                <button
                  onClick={async () => {
                    if (!freeText.trim()) return;
                    await run(() => proposeFreeformAmendment(id, freeText));
                    setFreeText("");
                    setFreeOpen(false);
                  }}
                  disabled={busy || !freeText.trim()}
                  className="rounded-full bg-[var(--accent)] px-4 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                >
                  Present in my words
                </button>
                <button
                  onClick={() => { setFreeOpen(false); setFreeText(""); }}
                  className="rounded-full px-3 py-1.5 text-xs font-semibold text-[var(--muted)] hover:text-[var(--fg)]"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Starting drafts — collapsed context, de-emphasized */}
      {drafts.length > 0 && (
        <details className="mt-8" data-testid="starting-drafts">
          <summary className="cursor-pointer text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
            Starting drafts ({drafts.length})
          </summary>
          <p className="mt-2 text-xs text-[var(--muted)]">
            Neutral reference wordings to react to — not anyone's position. Your cohort's presented versions lead the bill.
          </p>
          <ul className="mt-2 grid gap-2">
            {drafts.map((v) => (
              <li key={v.id} className="rounded-xl border border-dashed border-[var(--line)] p-3 text-sm">
                <p className="text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">{v.label ?? "draft"}</p>
                {Object.entries(v.positions).length > 0 && (
                  <div className="mt-1.5 grid gap-1">
                    {Object.entries(v.positions).map(([k, val]) => (
                      <p key={k} className="text-xs text-[var(--fg-soft)]">
                        <span className="text-[var(--muted)]">{promptByKey.get(k) ?? k}:</span> {val}
                      </p>
                    ))}
                  </div>
                )}
                {!resolved && isAuthenticated && (
                  <button
                    onClick={() => run(() => castAcceptance(id, v.id, true))}
                    disabled={busy}
                    className="mt-2 inline-flex items-center gap-1 rounded-full border border-[var(--line)] px-3 py-1 text-xs font-semibold hover:border-[var(--accent)] disabled:opacity-50"
                  >
                    <Check size={12} /> Co-sign this draft
                  </button>
                )}
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
  );
}
