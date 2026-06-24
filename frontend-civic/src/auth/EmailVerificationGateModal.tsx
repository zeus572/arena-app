import { useEffect, useState } from "react";
import { Button, ButtonLink } from "@/prototypes/magazine/components/Button";
import { arenaApi } from "@/auth/arenaAuthClient";
import { useAuth } from "@/auth/AuthContext";
import { subscribeEmailUnverified } from "@/auth/emailVerificationGate";
import { refreshAccessToken } from "@/auth/tokenManager";

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
      // FORCE a token rotation. The civic write endpoints authorize off the JWT
      // `email_verified` claim, which only updates when the access token is
      // re-minted via /auth/refresh. refreshUser() alone won't do it: it calls
      // getFreshAccessToken(), which keeps the existing (still-valid) token and
      // skips the refresh — so the just-verified account would keep sending a
      // stale email_verified=false token and stay gated. Rotate explicitly first.
      const rotated = await refreshAccessToken();
      if (!rotated) {
        setMessage("Couldn't refresh your account. Please try again.");
        return;
      }
      // Now sync the user object off the freshly-rotated token and dismiss. If
      // the email still isn't verified, the next write attempt re-opens this.
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
      <div className="theme-magazine w-full max-w-md rounded-2xl border border-[var(--border)] bg-[var(--bg)] p-6 shadow-xl">
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
