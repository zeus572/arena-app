import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, ArrowRight, Bot, Check, Compass, Sparkles, ScrollText, Clock, Users, Newspaper } from "lucide-react";
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
import { Term } from "../components/Term";

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

/** Compact deadline chip shown in the rail. */
function DeadlineRail({ bar }: { bar: ProvisionDetail["spectrumBar"] }) {
  if (!bar.deadline) return null;
  const end = new Date(bar.deadline);
  const ms = end.getTime();
  if (Number.isNaN(ms)) return null;
  const remaining = ms - Date.now();
  const urgent = remaining > 0 && remaining < 2 * 86_400_000;
  const daysLeft = bar.daysLeft;
  const label =
    remaining <= 0
      ? "Deadline passed"
      : daysLeft != null
        ? `${daysLeft} day${daysLeft === 1 ? "" : "s"} left`
        : end.toLocaleDateString(undefined, { month: "short", day: "numeric" });

  return (
    <div className="rounded-2xl border border-[var(--line)] p-4">
      <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
        <Clock size={12} /> Deadline
      </p>
      <p className={`mt-1 text-lg font-semibold ${urgent ? "text-rose-600" : "text-[var(--fg)]"}`}>
        {label}
      </p>
      {bar.deadline && (
        <p className="text-xs text-[var(--muted)]">
          {end.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" })}
        </p>
      )}
    </div>
  );
}

export default function CoalitionProvisionDetail() {
  const { id = "" } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  // A just-completed co-sign (or the participate back-link) hands us the fresh detail
  // via router state so the page reflects it without a refetch race. Guard on the id
  // so leftover history state from another bill can't seed the wrong provision.
  const seeded = location.state as { provision?: ProvisionDetail } | null;
  const seededProvision = seeded?.provision && seeded.provision.id === id ? seeded.provision : null;
  const { d, reload, run, busy, error } = useProvision(id, seededProvision);

  // Consume the one-time seed: clear it from history so a later manual refresh does a
  // normal fetch (router navigation state otherwise survives a reload).
  useEffect(() => {
    if (seededProvision) navigate(location.pathname, { replace: true, state: null });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const [steelOpen, setSteelOpen] = useState(false);
  const [steelText, setSteelText] = useState("");
  const [framings, setFramings] = useState<Framings | null>(null);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [lastAward, setLastAward] = useState<{ key: string; points: number; currency: string } | null>(null);
  // Failure of a Daily-act / Bridge / Steelman write (recordAct), so a dead or 403'd
  // tap shows something instead of the "+XP" chip simply never lighting.
  const [actError, setActError] = useState<string | null>(null);

  useEffect(() => { void getFramings(id).then(setFramings).catch(() => {}); }, [id]);
  useEffect(() => { void getMyProfile().then(setProfile).catch(() => {}); }, []);

  async function act(type: string, key: string, payload?: string, versionId?: string): Promise<boolean> {
    setActError(null);
    try {
      const r = await recordAct(id, type, payload, versionId);
      setLastAward({ key, points: r.points, currency: r.currency });
      reload();
      return true;
    } catch {
      setActError("That didn't go through — please try again.");
      return false;
    }
  }

  if (!d) return <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>;

  const resolved = ["Passed", "Forked", "Died"].includes(d.state);

  const compass = deriveCompassPosition(profile);

  const leadingVersion =
    d.versions.find((v) => v.id === d.spectrumBar.leadingVersionId) ??
    [...d.versions].sort(
      (a, b) => b.accepts - b.declines - (a.accepts - a.declines),
    )[0] ??
    null;
  // How many cohort-presented positions are in play (drafts are neutral seeds, not "on the table").
  const presentedCount = d.versions.filter((v) => v.authorUserId).length;

  // How the current user has engaged this bill, most to least committed:
  //  · presented  — put their own version on the table (`hasPositioned`)
  //  · co-signed  — accepted someone's version (`youCoSigned`)
  //  · joined     — declared a Compass bucket but hasn't acted on a wording yet
  // `youJoined` is the umbrella flag (true for all three); the finer flags let us
  // name exactly what they did instead of the vague "you're in this coalition".
  const youParticipant = d.yourUserId ? d.participants.find((pp) => pp.userId === d.yourUserId) : undefined;
  const youPositioned = !!youParticipant?.hasPositioned;
  const engagement = youPositioned ? "presented" : d.youCoSigned ? "cosigned" : "joined";
  const engagementCopy = {
    presented: {
      title: "Your position is on the table",
      body: "You've put a version up. Revisit to co-sign another wording or refine yours.",
    },
    cosigned: {
      title: "You co-signed this bill",
      body: "Your name is on a version. Revisit to co-sign another wording or put your own on the table.",
    },
    joined: {
      title: "You've joined this coalition",
      body: "You're in with your Compass position — revisit to co-sign a version or put your own on the table.",
    },
  }[engagement];
  // A prevailing position only exists once someone has actually presented one; until
  // then a seeded draft's wording must not masquerade as it — fall back to neutral text.
  const hasAgreedWording =
    presentedCount > 0 &&
    !!leadingVersion &&
    !!leadingVersion.text &&
    !leadingVersion.text.trimStart().startsWith("Version —");
  const prevailingText = hasAgreedWording ? leadingVersion!.text.trim() : d.neutralText;

  // ─── Compass join widget — shared between inline (mobile) and rail (desktop) ───
  const compassJoin = !d.youJoined && !resolved && (
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
  );

  return (
    // Removed max-w-3xl — the Layout's max-w-5xl constrains the page;
    // on desktop we want the full width for the two-column grid.
    <section data-testid="coalition-detail">
      <Link to="/coalition" className="inline-flex items-center gap-1 text-xs text-[var(--muted)] hover:text-[var(--fg)]">
        <ArrowLeft size={14} /> All bills
      </Link>

      {/* ── Two-column grid on md+; single column on mobile ── */}
      <div className="mt-3 md:grid md:grid-cols-[1fr_320px] md:gap-10 md:items-start">

        {/* ═══════════════════════════════════════════════
            MAIN READING COLUMN
        ═══════════════════════════════════════════════ */}
        <div className="min-w-0">
          <header>
            <div className="flex items-center justify-between">
              <h1 className="display text-3xl">{d.title}</h1>
              <span className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">{d.state}</span>
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-2 text-[10px] font-semibold uppercase tracking-wider">
              <span className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[var(--muted)]">{d.difficulty} gap · {(d.gapWidth * 100).toFixed(0)}%</span>
              <span className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[var(--muted)]">{d.governance ? "governance" : "culture"}</span>
              {d.relevantAxes.map((a) => (
                <span key={a} className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[var(--muted)]">{a}</span>
              ))}
            </div>
          </header>

          {/* Origin story — the briefing this bill was born from. Lets a reader trace
              the coalition back to the news that prompted it. Hidden for seeded bills
              with no source briefing. */}
          {d.sourceBriefingSlug && (
            <Link
              to={`/briefings/${d.sourceBriefingSlug}`}
              data-testid="origin-story-link"
              className="group mt-4 flex items-start gap-2.5 rounded-2xl border border-[var(--line)] p-3 transition hover:border-[var(--accent)]"
            >
              <Newspaper size={16} className="mt-0.5 shrink-0 text-[var(--accent)]" />
              <span className="min-w-0">
                <span className="block text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
                  Originated from this story
                </span>
                <span className="mt-0.5 flex items-center gap-1 text-sm font-medium text-[var(--fg)] group-hover:text-[var(--accent)]">
                  <span className="min-w-0 truncate">
                    {d.sourceBriefingHeadline ?? "Read the briefing"}
                  </span>
                  <ArrowRight size={14} className="shrink-0 transition-transform group-hover:translate-x-0.5" />
                </span>
              </span>
            </Link>
          )}

          {/* A single visible home for any failed action on this page, so no mutation
              (join, co-sign probe, reaction, bridge, steelman) can fail silently. */}
          {(error || actError) && (
            <div
              role="alert"
              data-testid="detail-error"
              className="mt-4 flex items-start gap-2 rounded-2xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-800"
            >
              <span className="mt-0.5 shrink-0 font-bold text-rose-600">!</span>
              <span>{error ?? actError}</span>
            </div>
          )}

          {/* Framing — only on desktop does this become the "lede" above the fold */}
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

          {/* Bridge the divide — culture bills only. Sorting the culture-war framing
              toward a carve-out both sides can sign records a CultureGovernanceSort act,
              which earns reasoning XP and completes the "Bridge a culture-war bill"
              daily quest (see PlayerHome / GetQuestsAsync). */}
          {!resolved && !d.governance && (
            <div className="mt-4 rounded-2xl border border-[var(--accent)]/40 p-4" data-testid="bridge-divide">
              <div className="flex items-center justify-between gap-2">
                <h3 className="flex items-center gap-2 text-sm font-semibold">
                  <Compass size={16} className="text-[var(--accent)]" /> Bridge the divide
                </h3>
                <span className="rounded-full bg-[var(--accent)]/10 px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
                  earn reasoning XP
                </span>
              </div>
              <p className="mt-1 text-xs text-[var(--muted)]">
                This is a culture-war bill. Sort it toward a carve-out both sides can sign — is the
                workable path a governance mechanism, or shared cultural ground?
              </p>
              <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
                {[
                  { key: "bridge-governance", label: "Route to a governance mechanism", payload: "governance" },
                  { key: "bridge-culture", label: "Name the shared cultural value", payload: "culture" },
                ].map((opt) => {
                  const lit = lastAward?.key === opt.key;
                  return (
                    <button
                      key={opt.key}
                      onClick={() => act("CultureGovernanceSort", opt.key, opt.payload, leadingVersion?.id)}
                      disabled={busy}
                      className={`flex flex-col items-start gap-1 rounded-xl border p-3 text-left text-xs font-medium transition disabled:opacity-50 ${
                        lit ? "border-emerald-400 bg-emerald-50" : "border-[var(--line)] hover:border-[var(--accent)]"
                      }`}
                    >
                      <span>{opt.label}</span>
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
            </div>
          )}

          {/* Prevailing position */}
          <div
            className="mt-6 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-4"
            data-testid="prevailing-position"
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
                <ScrollText size={14} /> {presentedCount === 0 ? "Starting point" : "Prevailing coalition position"}
              </p>
              {presentedCount > 0 && (
                <span
                  className="rounded-full bg-[var(--accent)] px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-white"
                  data-testid="on-the-table-count"
                >
                  {presentedCount} on the table
                </span>
              )}
            </div>
            <p className="mt-1.5 text-[15px] font-medium leading-snug text-[var(--fg)]">
              {prevailingText}
            </p>
            {presentedCount === 0 ? (
              <p className="mt-1.5 text-xs text-[var(--muted)]">
                The bill's neutral starting text — no one has taken a position yet. Be the first to move it.
              </p>
            ) : hasAgreedWording ? (
              <p className="mt-1.5 text-xs text-[var(--muted)]">
                Leading wording · {leadingVersion!.accepts} co-sign{leadingVersion!.accepts === 1 ? "" : "s"}
                {leadingVersion!.declines > 0 && ` · ${leadingVersion!.declines} decline${leadingVersion!.declines === 1 ? "" : "s"}`}
                {presentedCount > 1 && ` · leading ${presentedCount} positions`}
              </p>
            ) : (
              <p className="mt-1.5 text-xs text-[var(--muted)]">
                No agreed wording yet — this is the neutral starting point. Take a position to move it.
              </p>
            )}
          </div>

          {/* ── Mobile-only: spectrum status + join ── */}
          <div className="mt-6 md:hidden"><SpectrumBarView d={d} /></div>
          {!resolved && (
            <div className="mt-4 md:hidden">{compassJoin}</div>
          )}

          {/* Participate CTA — once you've engaged (joined / co-signed / presented),
              it flips to a "you're in" confirmation so the page reflects your act
              instead of still inviting you to take a position for the first time. */}
          {!resolved && (
            d.youJoined ? (
              <div
                data-testid="participated-banner"
                className="mt-6 rounded-2xl border border-emerald-300 bg-emerald-50 p-5"
              >
                <div className="flex items-start gap-2.5">
                  <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-100">
                    <Check size={15} className="text-emerald-700" />
                  </span>
                  <div className="min-w-0">
                    <p className="text-base font-semibold text-emerald-900">{engagementCopy.title}</p>
                    <p className="mt-0.5 text-xs text-emerald-700">{engagementCopy.body}</p>
                  </div>
                </div>
                <Link
                  to={`/coalition/${id}/participate`}
                  data-testid="participate-cta"
                  className="group mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-emerald-800 hover:text-emerald-900"
                >
                  Revisit your position
                  <ArrowRight size={16} className="transition-transform group-hover:translate-x-0.5" />
                </Link>
              </div>
            ) : (
              <Link
                to={`/coalition/${id}/participate`}
                data-testid="participate-cta"
                className="group mt-6 flex items-center justify-between gap-3 rounded-2xl bg-[var(--accent)] p-5 text-white shadow-sm transition hover:opacity-95"
              >
                <div>
                  <p className="text-base font-semibold">
                    {presentedCount === 0 ? "Be the first to take a position" : "Take your position"}
                  </p>
                  <p className="mt-0.5 text-xs text-white/80">
                    Answer the sub-questions, co-sign the closest version, or put your own carve-out on the table.
                  </p>
                </div>
                <ArrowRight size={20} className="shrink-0 transition-transform group-hover:translate-x-0.5" />
              </Link>
            )
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

          {/* Daily acts */}
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

            <div className="mt-3 rounded-xl border border-[var(--line)] bg-[var(--line)]/20 p-3">
              <p className="text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
                {hasAgreedWording ? "Reacting to the leading wording" : "Reacting to the proposal as proposed"}
              </p>
              <p className="mt-1 text-sm leading-snug text-[var(--fg-soft)]">{prevailingText}</p>
            </div>

            <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">
              {REASON_LABELS.map((label) => {
                const lit = lastAward?.key === label;
                return (
                  <button
                    key={label}
                    onClick={() => act("ReactionWithReason", label, label, leadingVersion?.id)}
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

            <div className="mt-3 border-t border-[var(--line)] pt-3">
              {!steelOpen ? (
                <div className="flex flex-wrap items-center gap-2">
                  <Button variant="ghost" size="sm" onClick={() => setSteelOpen(true)}>
                    Add a steelman
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">+ XP</span>
                  </Button>
                  {/* Confirmation persists after the form collapses, so the reward the
                      user just earned is actually visible (the form closing was the
                      only prior signal, and it vanished instantly). */}
                  {lastAward?.key === "steelman" && (
                    <span
                      className="inline-flex items-center gap-1 text-xs font-semibold text-emerald-700"
                      data-testid="steelman-saved"
                    >
                      <Check size={13} /> Steelman recorded +{lastAward.points} {lastAward.currency} XP
                    </span>
                  )}
                </div>
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
                      onClick={async () => {
                        if (!steelText.trim()) return;
                        // Await so we only clear + collapse on success; a failure keeps
                        // the text and surfaces the error banner (via actError).
                        const ok = await act("Steelman", "steelman", steelText, leadingVersion?.id);
                        if (ok) {
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
                <>Coalition of {d.outcome.signers?.join(", ")} — breadth {d.outcome.coveredBuckets}, <Term term="teeth">teeth</Term>{" "}
                  {d.outcome.specificity}, {d.outcome.movedSigners} moved.</>
              )}
              {d.outcome.diedReason && <>{d.outcome.diedReason}</>}
            </div>
          )}

          {/* Dev agent button — mobile inline, hidden in rail on desktop */}
          {import.meta.env.DEV && (
            <div className="mt-6 md:hidden">
              <Button
                onClick={() => run(() => agentStep(id))}
                disabled={busy || resolved}
              >
                <Bot size={16} /> Run agents (dev)
              </Button>
            </div>
          )}

          {/* Participants */}
          <h2 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
            <span className="inline-flex items-center gap-1.5"><Users size={13} /> Participants</span>
          </h2>
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
        </div>

        {/* ═══════════════════════════════════════════════
            STICKY RAIL — desktop only (hidden on mobile)
        ═══════════════════════════════════════════════ */}
        <aside className="hidden md:block">
          <div className="sticky top-4 grid gap-4">

            {/* Coalition status */}
            <SpectrumBarView d={d} />

            {/* Compass join */}
            {compassJoin}

            {/* Deadline */}
            <DeadlineRail bar={d.spectrumBar} />

            {/* Participate shortcut — mirrors the main CTA's participated state. */}
            {!resolved && (
              <Link
                to={`/coalition/${id}/participate`}
                className={`group flex items-center justify-between gap-2 rounded-2xl p-4 shadow-sm transition hover:opacity-95 ${
                  d.youJoined
                    ? "border border-emerald-300 bg-emerald-50 text-emerald-900"
                    : "bg-[var(--accent)] text-white"
                }`}
              >
                <p className="flex items-center gap-1.5 text-sm font-semibold">
                  {d.youJoined && <Check size={14} className="shrink-0 text-emerald-700" />}
                  {d.youJoined ? "Revisit your position" : "Take your position"}
                </p>
                <ArrowRight size={16} className="shrink-0 transition-transform group-hover:translate-x-0.5" />
              </Link>
            )}

            {/* Dev — agent button in rail */}
            {import.meta.env.DEV && (
              <Button
                onClick={() => run(() => agentStep(id))}
                disabled={busy || resolved}
              >
                <Bot size={16} /> Run agents (dev)
              </Button>
            )}

          </div>
        </aside>

      </div>{/* end two-column grid */}
    </section>
  );
}
