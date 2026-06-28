import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "../components/Button";
import { useAuth } from "@/auth/AuthContext";

export default function MagazineLogin() {
  const { login, completeMfaChallenge } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const redirect = params.get("redirect") ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Second-factor step: set once /login reports MFA is required for this account.
  const [mfaToken, setMfaToken] = useState<string | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [rememberDevice, setRememberDevice] = useState(false);

  const errMsg = (err: unknown, fallback: string) =>
    (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const result = await login(email, password);
      if (result.status === "mfa") {
        setMfaToken(result.mfaToken);
      } else {
        navigate(redirect);
      }
    } catch (err) {
      setError(errMsg(err, "We couldn't sign you in. Check your email and password."));
    } finally {
      setSubmitting(false);
    }
  };

  const onMfaSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!mfaToken) return;
    setError(null);
    setSubmitting(true);
    try {
      await completeMfaChallenge(mfaToken, mfaCode, rememberDevice);
      navigate(redirect);
    } catch (err) {
      setError(errMsg(err, "That code didn't work. Try again."));
    } finally {
      setSubmitting(false);
    }
  };

  if (mfaToken) {
    return (
      <article className="mx-auto max-w-md" data-testid="magazine-login-mfa">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Account
        </p>
        <h1 className="display mt-2 text-4xl">Two-factor authentication</h1>
        <p className="mt-3 text-sm text-[var(--fg-soft)]">
          Enter the 6-digit code from your authenticator app, or one of your backup codes.
        </p>

        <form onSubmit={onMfaSubmit} className="mt-8 flex flex-col gap-4">
          <label className="flex flex-col gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              Authentication code
            </span>
            <input
              type="text"
              inputMode="text"
              autoComplete="one-time-code"
              autoFocus
              value={mfaCode}
              onChange={(e) => setMfaCode(e.target.value)}
              required
              className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base tracking-widest text-[var(--fg)] outline-none focus:border-[var(--accent)]"
              data-testid="login-mfa-code"
            />
          </label>

          <label className="flex items-center gap-2 text-sm text-[var(--fg-soft)]">
            <input
              type="checkbox"
              checked={rememberDevice}
              onChange={(e) => setRememberDevice(e.target.checked)}
            />
            Remember this computer for 90 days
          </label>

          {error && (
            <p className="text-sm text-red-600" data-testid="login-error">
              {error}
            </p>
          )}

          <Button type="submit" disabled={submitting} className="mt-2" data-testid="login-mfa-submit">
            {submitting ? "Verifying…" : "Verify"}
          </Button>
        </form>

        <button
          type="button"
          onClick={() => { setMfaToken(null); setMfaCode(""); setError(null); }}
          className="mt-4 text-sm text-[var(--accent)] underline"
        >
          Back to sign in
        </button>
      </article>
    );
  }

  return (
    <article className="mx-auto max-w-md" data-testid="magazine-login">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Account
      </p>
      <h1 className="display mt-2 text-4xl">Sign in to Civersify</h1>
      <p className="mt-3 text-sm text-[var(--fg-soft)]">
        One account gets you Civersify civics and the Debate Arena debate
        floor. Sign in with the same email and password you use there.
      </p>

      <form onSubmit={onSubmit} className="mt-8 flex flex-col gap-4">
        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Email
          </span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="login-email"
          />
        </label>

        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Password
          </span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="login-password"
          />
        </label>

        {error && (
          <p className="text-sm text-red-600" data-testid="login-error">
            {error}
          </p>
        )}

        <Button
          type="submit"
          disabled={submitting}
          className="mt-2"
          data-testid="login-submit"
        >
          {submitting ? "Signing you in…" : "Sign in"}
        </Button>
      </form>

      <p className="mt-4 text-sm text-[var(--fg-soft)]">
        <Link to="/forgot-password" className="text-[var(--accent)] underline">
          Forgot your password?
        </Link>
      </p>

      <p className="mt-6 text-sm text-[var(--fg-soft)]">
        New here?{" "}
        <Link
          to={`/register?redirect=${encodeURIComponent(redirect)}`}
          className="text-[var(--accent)] underline"
        >
          Create a Civersify account
        </Link>
        .
      </p>
    </article>
  );
}
