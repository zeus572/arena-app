import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Check, X, Compass, ScrollText, Sparkles, PenLine, Star, Flag } from "lucide-react";
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
import { Button } from "../components/Button";

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

type RankedVersion = { v: CoalitionVersion; matches: number; considered: number; score: number };

/** Numbered step label so the page reads as a clear answer → compare → propose funnel. */
function StepHeader({ n, title, aside }: { n: number; title: string; aside?: ReactNode }) {
  return (
    <div className="flex items-center gap-2.5">
      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[var(--accent)] text-xs font-bold text-white">
        {n}
      </span>
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--fg)]">{title}</h2>
      {aside && <span className="ml-auto text-xs font-semibold text-[var(--accent)]">{aside}</span>}
    </div>
  );
}

function MatchBar({ score }: { score: number }) {
  return (
    <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-[var(--line)]">
      <div className="h-full rounded-full bg-[var(--accent)] transition-all" style={{ width: `${Math.round(score * 100)}%` }} />
    </div>
  );
}

/**
 * A single version "on the table" — a cohort-presented position or a neutral
 * starting draft. Drafts get a dashed, de-emphasized frame but live in the same
 * ranked list as real positions, so the player can always see how many wordings
 * are in play (the old design buried drafts in a chevron). Each answer the player
 * has picked is checked against the version's positions inline, so "3/4 match"
 * is shown, not just asserted.
 */
function VersionRow({
  entry,
  isDraft,
  highlight,
  answers,
  promptByKey,
  resolved,
  isAuthenticated,
  busy,
  onAccept,
}: {
  entry: RankedVersion;
  isDraft: boolean;
  highlight: boolean;
  answers: Record<string, string>;
  promptByKey: Map<string, string>;
  resolved: boolean;
  isAuthenticated: boolean;
  busy: boolean;
  onAccept: (versionId: string, accept: boolean) => void;
}) {
  const { v, matches, considered } = entry;
  const positions = Object.entries(v.positions);
  return (
    <li
      data-testid={isDraft ? "draft-row" : "presented-row"}
      className={`rounded-xl border p-3.5 transition ${
        highlight
          ? "border-[var(--accent)] bg-[var(--accent)]/5 shadow-sm"
          : isDraft
            ? "border-dashed border-[var(--line)]"
            : "border-[var(--line)]"
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {highlight && (
            <span className="inline-flex items-center gap-1 rounded-full bg-[var(--accent)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-white">
              <Star size={10} /> Closest to you
            </span>
          )}
          <span className="rounded-full bg-[var(--accent)]/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
            {isDraft ? `neutral draft${v.label ? ` · ${v.label}` : ""}` : v.label ?? "version"}
          </span>
          {considered > 0 && (
            <span className="text-xs font-semibold text-[var(--accent)]" data-testid={`match-${v.id}`}>
              {matches}/{considered} match
            </span>
          )}
        </div>
        <span className="shrink-0 text-xs text-[var(--muted)]" title="co-signs · declines">
          ✓ {v.accepts} · ✕ {v.declines}
        </span>
      </div>

      {considered > 0 && <MatchBar score={considered ? matches / considered : 0} />}

      {positions.length > 0 && (
        <div className="mt-2.5 grid gap-1">
          {positions.map(([k, val]) => {
            const mine = answers[k];
            const match = !!mine && mine.toLowerCase() === val.toLowerCase();
            const conflict = !!mine && !match;
            return (
              <p key={k} className="flex items-center gap-1.5 text-xs leading-snug">
                <span className="text-[var(--muted)]">{promptByKey.get(k) ?? k}:</span>
                <span className={match ? "font-semibold text-emerald-700" : conflict ? "text-rose-700" : "text-[var(--fg-soft)]"}>
                  {val}
                </span>
                {match && <Check size={12} className="shrink-0 text-emerald-600" />}
              </p>
            );
          })}
        </div>
      )}

      {!resolved && isAuthenticated && (
        <div className="mt-3 flex gap-2">
          <Button variant="positive" size="sm" onClick={() => onAccept(v.id, true)} disabled={busy}>
            <Check size={12} /> Co-sign
          </Button>
          <Button variant="danger" size="sm" onClick={() => onAccept(v.id, false)} disabled={busy}>
            <X size={12} /> Decline
          </Button>
        </div>
      )}
    </li>
  );
}

export default function CoalitionProvisionParticipate() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { d, run, busy, error } = useProvision(id);

  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [profile, setProfile] = useState<Profile | null>(null);
  const [freeOpen, setFreeOpen] = useState(false);
  const [freeText, setFreeText] = useState("");
  // True right after a successful present, so we can confirm it landed. Cleared the
  // moment the player edits an answer (they're now drafting a different version).
  const [justPresented, setJustPresented] = useState(false);
  // Set right after a successful co-sign. Co-signing completes the activity, so we
  // show an unmistakable confirmation overlay and then return to the overview.
  const [coSigned, setCoSigned] = useState(false);

  useEffect(() => { void getMyProfile().then(setProfile).catch(() => {}); }, []);

  // After a co-sign lands, let the confirmation register, then back out to the
  // coalition overview — the participate task is done for this bill.
  useEffect(() => {
    if (!coSigned) return;
    // Carry the post-co-sign detail we already hold so the overview reflects the
    // co-sign immediately, instead of racing a refetch that can return a stale
    // (pre-co-sign) snapshot until a manual refresh.
    const t = setTimeout(() => navigate(`/coalition/${id}`, { state: { provision: d } }), 1500);
    return () => clearTimeout(t);
  }, [coSigned, id, navigate, d]);

  const answeredKeys = useMemo(() => Object.keys(answers).filter((k) => answers[k]), [answers]);

  if (!d) return <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>;

  const resolved = ["Passed", "Forked", "Died"].includes(d.state);
  const promptByKey = new Map<string, string>(d.subQuestions.map((sq) => [sq.key, sq.prompt]));
  const compass = deriveCompassPosition(profile);

  // Versions a cohort member presented vs. the neutral starting drafts the system seeded.
  const presented = d.versions.filter((v) => v.authorUserId);
  const drafts = d.versions.filter((v) => !v.authorUserId);
  const noOneHasPresented = presented.length === 0;

  // The prevailing wording: the leading version (server-chosen, else best net co-signs),
  // falling back to the neutral text when nobody has agreed wording yet. Mirrors the
  // overview page so the same "current answer" reads identically across both routes.
  const leadingVersion =
    d.versions.find((v) => v.id === d.spectrumBar.leadingVersionId) ??
    [...d.versions].sort((a, b) => b.accepts - b.declines - (a.accepts - a.declines))[0] ??
    null;
  // A prevailing position only exists once someone in the cohort has actually
  // presented one — until then a seeded draft's wording must not masquerade as it.
  const hasAgreedWording =
    !noOneHasPresented && !!leadingVersion && !!leadingVersion.text && !leadingVersion.text.trimStart().startsWith("Version —");
  const prevailingText = hasAgreedWording ? leadingVersion!.text.trim() : d.neutralText;

  // Rank presented positions and neutral drafts separately by closeness to the
  // user's current answers; the closest presented position is highlighted.
  const rankBy = (vs: CoalitionVersion[]): RankedVersion[] =>
    vs.map((v) => ({ v, ...closeness(answers, v) })).sort((a, b) => b.score - a.score || b.v.accepts - a.v.accepts);
  const rankedPresented = rankBy(presented);
  const rankedDrafts = rankBy(drafts);
  // Only crown a "Closest to you" when the leading presented position actually shares
  // at least one of your answers. Guarding on `considered > 0` alone (you answered
  // something) let the accepts tiebreak pin the badge on a 0-match version when your
  // answers matched none of them — labelling the literally farthest position closest.
  const closestId =
    answeredKeys.length > 0 && (rankedPresented[0]?.matches ?? 0) > 0 ? rankedPresented[0]!.v.id : null;

  const progressPct = d.subQuestions.length ? (answeredKeys.length / d.subQuestions.length) * 100 : 0;
  const onAccept = async (versionId: string, accept: boolean) => {
    const ok = await run(() => castAcceptance(id, versionId, accept));
    // Only confirm + back out if the co-sign actually landed. A co-sign completes the
    // activity; a decline keeps you here to co-sign another version or present your own.
    if (ok && accept) setCoSigned(true);
  };

  function pick(key: string, option: string) {
    setJustPresented(false);
    setAnswers((prev) => ({ ...prev, [key]: prev[key] === option ? "" : option }));
  }

  async function present() {
    if (!isAuthenticated) return;
    const positions = Object.fromEntries(answeredKeys.map((k) => [k, answers[k]]));
    if (Object.keys(positions).length === 0) return;
    const label = noOneHasPresented ? "first reading" : "carve-out";
    const ok = await run(async () => {
      // Join with your Civic Compass position the first time you act on this bill.
      if (!d!.youJoined && compass.hasData) await joinProvision(id, compass.bucket);
      return proposeAmendment(id, positions, label);
    });
    if (!ok) return; // failure already surfaced via `error`; don't fake a success banner
    // Keep the answers in place rather than blanking them: the version we just added
    // is built from these answers, so it now ranks top as a 4/4 "Closest to you" and
    // stays on screen as the confirmation. (Clearing them collapsed the whole compare
    // panel back to the "pick an answer" placeholder, so the submit looked like a
    // silent no-op.) The success banner makes the outcome explicit.
    setJustPresented(true);
  }

  return (
    <section data-testid="coalition-participate" className="max-w-3xl">
      {/* Co-sign confirmation — an unmistakable full-view overlay before we route
          back to the overview, so the action never feels like a silent no-op. */}
      {coSigned && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-6 backdrop-blur-sm"
          data-testid="cosign-success"
          role="status"
          aria-live="polite"
        >
          <div className="flex max-w-sm flex-col items-center gap-3 rounded-3xl border border-emerald-300 bg-[var(--bg-elev)] p-8 text-center shadow-xl">
            <span className="flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100">
              <Check size={30} className="text-emerald-600" />
            </span>
            <p className="text-lg font-semibold text-[var(--fg)]">You co-signed this version.</p>
            <p className="text-sm text-[var(--muted)]">
              Your name is on it — taking you back to the coalition.
            </p>
          </div>
        </div>
      )}

      <Link
        to={`/coalition/${id}`}
        state={{ provision: d }}
        className="inline-flex items-center gap-1 text-xs text-[var(--muted)] hover:text-[var(--fg)]"
      >
        <ArrowLeft size={14} /> {d.title}
      </Link>

      <header className="mt-3 flex items-center justify-between gap-3">
        <h1 className="display text-3xl">Take your position</h1>
        <span className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{d.state}</span>
      </header>
      <p className="mt-2 text-base text-[var(--fg-soft)]">
        Answer the questions to say where you stand — then co-sign the version closest to you, or put your
        own on the table for the cohort to rally behind.
      </p>

      {/* ── Lay of the land: the bill's neutral starting text, or — once someone has
          presented — the prevailing position and how many wordings are in play ── */}
      <div
        className="mt-5 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-4"
        data-testid="prevailing-position"
      >
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
            <ScrollText size={14} /> {noOneHasPresented ? "Starting point" : "Prevailing coalition position"}
          </p>
          {!noOneHasPresented && (
            <span
              className="rounded-full bg-[var(--accent)] px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-white"
              data-testid="on-the-table-count"
            >
              {presented.length} on the table
            </span>
          )}
        </div>
        <p className="mt-1.5 text-[15px] font-medium leading-snug text-[var(--fg)]">{prevailingText}</p>
        {noOneHasPresented ? (
          <p className="mt-1.5 text-xs text-[var(--muted)]">
            The bill's neutral starting text — no one has taken a position yet.
          </p>
        ) : hasAgreedWording ? (
          <p className="mt-1.5 text-xs text-[var(--muted)]">
            Leading wording · {leadingVersion!.accepts} co-sign{leadingVersion!.accepts === 1 ? "" : "s"}
            {leadingVersion!.declines > 0 && ` · ${leadingVersion!.declines} decline${leadingVersion!.declines === 1 ? "" : "s"}`}
          </p>
        ) : (
          <p className="mt-1.5 text-xs text-[var(--muted)]">
            No agreed wording yet — take a position below to move it.
          </p>
        )}
      </div>

      {noOneHasPresented && !resolved && (
        <div className="mt-4 flex items-start gap-2.5 rounded-2xl border border-[var(--accent)]/40 bg-[var(--bg-elev)] p-4" data-testid="be-first-banner">
          <Sparkles size={18} className="mt-0.5 shrink-0 text-[var(--accent)]" />
          <div>
            <p className="text-sm font-semibold text-[var(--accent)]">You can set the agenda.</p>
            <p className="mt-0.5 text-xs text-[var(--muted)]">
              No one in your cohort has presented this bill yet. Answer below and be the first version everyone reacts to.
            </p>
          </div>
        </div>
      )}

      {/* ═══ Step 1 — answer the sub-questions ═══ */}
      <div className="mt-8">
        <StepHeader n={1} title="Your answers" aside={`${answeredKeys.length}/${d.subQuestions.length} answered`} />
        <div className="mt-2 h-1 w-full overflow-hidden rounded-full bg-[var(--line)]">
          <div className="h-full rounded-full bg-[var(--accent)] transition-all" style={{ width: `${progressPct}%` }} />
        </div>

        <ul className="mt-4 grid gap-3" data-testid="subquestion-cards">
          {d.subQuestions.map((sq) => {
            const answered = !!answers[sq.key];
            return (
              <li
                key={sq.key}
                className={`rounded-2xl border p-4 transition ${
                  answered ? "border-[var(--accent)]/50 bg-[var(--accent)]/[0.03]" : "border-[var(--line)]"
                }`}
              >
                <p className="flex items-start gap-2 text-lg font-semibold">
                  <span className="flex-1">{sq.prompt}</span>
                  {answered && <Check size={18} className="mt-1 shrink-0 text-emerald-600" />}
                </p>
                {sq.tradeoff && <p className="mt-1 text-sm text-[var(--fg-soft)]">Tradeoff: {sq.tradeoff}</p>}
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
                          className={`rounded-full border px-3 py-1 text-xs font-semibold transition disabled:opacity-50 ${
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
                  <p className="mt-2 text-xs text-[var(--muted)]">Open question — describe your take in your own words in Step 2.</p>
                )}
              </li>
            );
          })}
        </ul>
      </div>

      {/* Sign-in gate: anyone can pick answers, but co-signing / presenting needs an account. */}
      {!resolved && !isAuthenticated && (
        <div className="mt-6" data-testid="participate-signin">
          <SignInPrompt
            compact
            title="Sign in to save your position"
            message="Pick your answers above — then sign in to co-sign a version or put your own on the table."
          />
        </div>
      )}

      {/* ═══ Step 2 — where you land + act ═══ */}
      {!resolved && isAuthenticated && (
        <div className="mt-8">
          <StepHeader n={2} title="Where you land" />

          {error && (
            <div
              role="alert"
              data-testid="participate-error"
              className="mt-3 flex items-start gap-2 rounded-2xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-800"
            >
              <X size={16} className="mt-0.5 shrink-0 text-rose-600" />
              <span>{error}</span>
            </div>
          )}

          {answeredKeys.length === 0 ? (
            <p
              className="mt-3 rounded-2xl border border-dashed border-[var(--line)] p-5 text-center text-sm text-[var(--muted)]"
              data-testid="compare-empty"
            >
              Pick at least one answer above to see which of the{" "}
              <span className="font-semibold text-[var(--fg)]">{d.versions.length}</span> wordings on the table line up with you.
            </p>
          ) : (
            <>
              {/* Compare — presented positions, then neutral drafts, ranked by match */}
              <div className="mt-3" data-testid="compare-panel">
                {compass.hasData && (
                  <p className="mb-2 flex items-center gap-1 text-[11px] text-[var(--muted)]">
                    <Compass size={12} className="text-[var(--accent)]" /> You'll act as {compass.label}
                  </p>
                )}

                {presented.length > 0 && (
                  <>
                    <p className="text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">
                      Positions on the table ({presented.length})
                    </p>
                    <ul className="mt-2 grid gap-2" data-testid="closeness-list">
                      {rankedPresented.map((e) => (
                        <VersionRow
                          key={e.v.id}
                          entry={e}
                          isDraft={false}
                          highlight={e.v.id === closestId}
                          answers={answers}
                          promptByKey={promptByKey}
                          resolved={resolved}
                          isAuthenticated={isAuthenticated}
                          busy={busy}
                          onAccept={onAccept}
                        />
                      ))}
                    </ul>
                  </>
                )}

                {rankedDrafts.length > 0 && (
                  <div className="mt-4" data-testid="starting-drafts">
                    <p className="text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">
                      Neutral starting drafts ({rankedDrafts.length})
                    </p>
                    <p className="mt-1 text-xs text-[var(--muted)]">
                      Reference wordings to react to — not anyone's position. Co-signing one nudges it toward becoming the bill.
                    </p>
                    <ul className="mt-2 grid gap-2">
                      {rankedDrafts.map((e) => (
                        <VersionRow
                          key={e.v.id}
                          entry={e}
                          isDraft
                          highlight={false}
                          answers={answers}
                          promptByKey={promptByKey}
                          resolved={resolved}
                          isAuthenticated={isAuthenticated}
                          busy={busy}
                          onAccept={onAccept}
                        />
                      ))}
                    </ul>
                  </div>
                )}

                {d.versions.length === 0 && (
                  <p className="text-sm text-[var(--muted)]">No versions yet — yours will be the first.</p>
                )}
              </div>

              {/* Confirmation that a just-presented version actually landed */}
              {justPresented && (
                <div
                  className="mt-5 flex items-start gap-2.5 rounded-2xl border border-emerald-300 bg-emerald-50 p-4"
                  data-testid="present-success"
                >
                  <Check size={18} className="mt-0.5 shrink-0 text-emerald-600" />
                  <div>
                    <p className="text-sm font-semibold text-emerald-800">Your position is on the table.</p>
                    <p className="mt-0.5 text-xs text-emerald-700">
                      It's listed under “Positions on the table” above and marked closest to you — the cohort can
                      co-sign it now. Change an answer to put up a different version.
                    </p>
                  </div>
                </div>
              )}

              {/* Primary CTA — put your own version on the table */}
              <div className="mt-5 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-5" data-testid="present-cta">
                <p className="flex items-center gap-2 text-sm font-semibold text-[var(--fg)]">
                  <Flag size={16} className="text-[var(--accent)]" />
                  {noOneHasPresented ? "Be the first to put a version on the table" : "None of these fit? Put yours on the table"}
                </p>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  Your {answeredKeys.length} answer{answeredKeys.length === 1 ? "" : "s"} become a version the whole cohort can
                  co-sign — {noOneHasPresented ? "and the one everyone reacts to." : "a carve-out that can overtake the lead."}
                </p>
                <Button
                  fullWidth
                  onClick={present}
                  disabled={busy || answeredKeys.length === 0}
                  data-testid="present-version"
                  className="mt-3"
                >
                  <Flag size={14} /> {noOneHasPresented ? "Present this as the bill" : "Put my version on the table"}
                </Button>
              </div>

              {/* Secondary: describe in your own words (free-form) */}
              <div className="mt-3">
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
                      <Button
                        size="sm"
                        onClick={async () => {
                          if (!freeText.trim()) return;
                          const ok = await run(() => proposeFreeformAmendment(id, freeText));
                          if (!ok) return; // keep the text so they can retry; error banner shows
                          setFreeText("");
                          setFreeOpen(false);
                        }}
                        disabled={busy || !freeText.trim()}
                      >
                        Present in my words
                      </Button>
                      <Button variant="link" size="sm" onClick={() => { setFreeOpen(false); setFreeText(""); }}>
                        Cancel
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      )}
    </section>
  );
}
