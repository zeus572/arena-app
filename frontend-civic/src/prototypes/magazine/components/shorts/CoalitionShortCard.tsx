import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Handshake, Clock, Sparkles } from "lucide-react";
import { cn } from "@/lib/cn";
import { useAuth } from "@/auth/AuthContext";
import { type ProvisionSummary, recordAct } from "@/api/coalition";

// Compact subset of the stance labels used on the full provision detail page. The
// payload string is free-form on the backend, so these read as the quick "reaction
// with reason" the casual feed wants.
const STANCES = ["Workable", "Unworkable", "Addresses it", "Hidden cost"];

function daysLeft(deadline: string | null): number | null {
  if (!deadline) return null;
  const ms = new Date(deadline).getTime() - Date.now();
  return ms <= 0 ? 0 : Math.ceil(ms / 86_400_000);
}

/**
 * Full-viewport Shorts card for a coalition provision. Reads are public, but
 * recording a stance is an account-bound act: anonymous taps are routed to sign-in
 * (per product decision), while a signed-in-but-unverified tap lets recordAct's
 * 403 surface the app-wide EmailVerificationGateModal automatically.
 */
export function CoalitionShortCard({ provision }: { provision: ProvisionSummary }) {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [picked, setPicked] = useState<string | null>(null);
  const [earned, setEarned] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);

  const left = daysLeft(provision.deadline);
  const pct =
    provision.totalBuckets > 0
      ? Math.round((provision.coveredBuckets / provision.totalBuckets) * 100)
      : 0;

  async function react(label: string) {
    if (busy || picked) return;
    if (!isAuthenticated) {
      navigate("/login");
      return;
    }
    setBusy(true);
    try {
      const res = await recordAct(provision.id, "ReactionWithReason", label);
      setPicked(label);
      setEarned(res.points);
    } catch {
      // A 403 email_unverified is handled globally by the civicApi interceptor
      // (opens the verify-email gate). Any other error: leave the card untouched.
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto flex h-full w-full max-w-xl flex-col px-5 pb-8 pt-20">
      <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
        <Handshake className="h-4 w-4" /> Coalition
      </p>

      <div className="my-4 flex flex-1 flex-col justify-center">
        <h2 className="display text-3xl leading-tight text-[var(--fg)]">
          {provision.title}
        </h2>
        <p className="mt-3 text-lg leading-relaxed text-[var(--fg-soft)]">
          {provision.neutralText}
        </p>

        <div className="mt-5 flex flex-wrap items-center gap-2 text-[11px] font-semibold uppercase tracking-wider">
          <span className="rounded-full border border-[var(--border)] px-2 py-0.5 text-[var(--muted)]">
            {provision.governance ? "Governance" : "Culture"}
          </span>
          <span className="rounded-full border border-[var(--border)] px-2 py-0.5 text-[var(--muted)]">
            {provision.difficulty}
          </span>
          {provision.locality && (
            <span className="rounded-full border border-[var(--border)] px-2 py-0.5 text-[var(--muted)]">
              {provision.locality}
            </span>
          )}
          {left != null && (
            <span className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-2 py-0.5 text-[var(--muted)]">
              <Clock className="h-3 w-3" />
              {left === 0 ? "Closing" : `${left}d left`}
            </span>
          )}
        </div>

        {/* Coalition breadth so far. */}
        <div className="mt-4">
          <div className="flex items-center justify-between text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">
            <span>Coalition breadth</span>
            <span className="tabular-nums">
              {provision.coveredBuckets}/{provision.totalBuckets}
            </span>
          </div>
          <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-[var(--bg-elev)]">
            <div
              className="h-full rounded-full bg-[var(--accent)] transition-all"
              style={{ width: `${pct}%` }}
            />
          </div>
        </div>
      </div>

      {/* Quick opinion: a reason-tagged stance. */}
      <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
        {picked ? (
          <span className="inline-flex items-center gap-1.5 text-[var(--accent)]">
            <Sparkles className="h-3.5 w-3.5" /> Logged “{picked}”
            {earned ? ` · +${earned}` : ""}
          </span>
        ) : (
          "Your quick read"
        )}
      </p>
      <div className="mt-2 grid grid-cols-2 gap-2">
        {STANCES.map((label) => (
          <button
            key={label}
            type="button"
            disabled={busy || !!picked}
            onClick={() => react(label)}
            data-testid={`short-coalition-stance`}
            className={cn(
              "rounded-full border px-3 py-3 text-sm font-semibold transition",
              picked === label
                ? "border-[var(--accent)] bg-[var(--accent)] text-white"
                : "border-[var(--border)] text-[var(--fg-soft)] hover:border-[var(--accent)]",
              picked && picked !== label && "opacity-50",
            )}
          >
            {label}
          </button>
        ))}
      </div>
      <Link
        to={`/coalition/${provision.id}`}
        className="mt-3 self-end text-sm font-semibold text-[var(--accent)] hover:underline"
        data-testid="short-coalition-open"
      >
        Open in Coalition →
      </Link>
    </div>
  );
}
