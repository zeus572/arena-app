import { useEffect, useState } from "react";
import { Button, ButtonLink } from "@/prototypes/magazine/components/Button";
import { arenaApi } from "@/auth/arenaAuthClient";
import { useAuth } from "@/auth/AuthContext";
import { subscribeEmailUnverified } from "@/auth/emailVerificationGate";

/**
 * Mounted once at the app root. Listens for the global "email_unverified" signal
 * (raised by the civic API interceptor when an account-bound write is rejected
 * with 403) and shows a prompt explaining the user must verify their email to
 * participate — with a one-click resend and a "retry" that re-fetches the user so
 * a freshly verified account can act without a full reload.
 */
export default function EmailVerificationGateModal() {
  const { user, refreshUser } = useAuth();
  const [open, setOpen] = useState(false);
  const [resending, setResending] = useState(false);
  const [checking, setChecking] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(
    () =>
      subscribeEmailUnverified(() => {
        setMessage(null);
        setOpen(true);
      }),
    [],
  );

  if (!open) return null;

  async function resend() {
    setResending(true);
    setMessage(null);
    try {
      // Same call Settings uses: a real verification email via the shared Arena
      // backend, whose link lands on /verify-email here in Civic.
      await arenaApi.post("/auth/resend-verification", { app: "civic" });
      setMessage("Verification email sent — check your inbox.");
    } catch {
      setMessage("Couldn't send the email. Please try again.");
    } finally {
      setResending(false);
    }
  }

  async function recheck() {
    setChecking(true);
    setMessage(null);
    try {
      // Re-fetch the profile (and rotate the access token) so a just-verified
      // account picks up email_verified=true. If it's now verified, dismiss;
      // otherwise the next write attempt will re-open this prompt.
      await refreshUser();
      setOpen(false);
    } catch {
      setMessage("Couldn't refresh your account. Please try again.");
    } finally {
      setChecking(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="verify-email-gate-title"
    >
      <div className="w-full max-w-md rounded-2xl border border-[var(--border)] bg-[var(--bg)] p-6 shadow-xl">
        <h2
          id="verify-email-gate-title"
          className="font-serif text-xl text-[var(--fg)]"
        >
          Verify your email to participate
        </h2>
        <p className="mt-3 text-[14px] leading-relaxed text-[var(--fg-soft)]">
          To keep the community spam-free, joining leagues, running campaigns,
          taking coalition actions, and starting petitions are reserved for
          verified accounts. Confirm your email
          {user?.email ? (
            <>
              {" "}
              (<span className="font-medium text-[var(--fg)]">{user.email}</span>)
            </>
          ) : null}{" "}
          to unlock these.
        </p>

        {message ? (
          <p className="mt-3 text-[13px] text-[var(--accent)]">{message}</p>
        ) : null}

        <div className="mt-5 flex flex-wrap items-center gap-2">
          <Button onClick={resend} disabled={resending}>
            {resending ? "Sending…" : "Resend verification email"}
          </Button>
          <Button variant="secondary" onClick={recheck} disabled={checking}>
            {checking ? "Checking…" : "I've verified — retry"}
          </Button>
          <ButtonLink
            variant="ghost"
            to="/settings"
            onClick={() => setOpen(false)}
          >
            Account settings
          </ButtonLink>
          <Button variant="link" onClick={() => setOpen(false)}>
            Dismiss
          </Button>
        </div>
      </div>
    </div>
  );
}
