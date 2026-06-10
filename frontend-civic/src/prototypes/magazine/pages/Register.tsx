import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "../components/Button";
import { useAuth } from "@/auth/AuthContext";

export default function MagazineRegister() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const redirect = params.get("redirect") ?? "/";

  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [inviteCode, setInviteCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    // Catch typos before hitting the server — the password field is masked.
    if (password !== confirmPassword) {
      setError("Passwords don't match.");
      return;
    }
    setSubmitting(true);
    try {
      await register(email, password, displayName, inviteCode);
      navigate(redirect);
    } catch (err) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? "We couldn't create your account.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <article className="mx-auto max-w-md" data-testid="magazine-register">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Account
      </p>
      <h1 className="display mt-2 text-4xl">Create your Civersify account</h1>
      <p className="mt-3 text-sm text-[var(--fg-soft)]">
        Your account works in both Civersify and the Debate Arena debate
        floor. Your civic profile, election timers, and saved progress travel
        with you.
      </p>

      <form onSubmit={onSubmit} className="mt-8 flex flex-col gap-4">
        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Display name
          </span>
          <input
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            required
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="register-displayname"
          />
        </label>

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
            data-testid="register-email"
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
            minLength={8}
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="register-password"
          />
          <span className="text-xs text-[var(--muted)]">At least 8 characters.</span>
        </label>

        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Confirm password
          </span>
          <input
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            minLength={8}
            aria-invalid={confirmPassword.length > 0 && confirmPassword !== password}
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="register-confirm-password"
          />
          {confirmPassword.length > 0 && confirmPassword !== password && (
            <span className="text-xs text-red-600" data-testid="register-password-mismatch">
              Passwords don't match.
            </span>
          )}
        </label>

        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Invite code
          </span>
          <input
            type="text"
            value={inviteCode}
            onChange={(e) => setInviteCode(e.target.value)}
            required
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base uppercase tracking-widest text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="register-invite"
          />
        </label>

        {error && (
          <p className="text-sm text-red-600" data-testid="register-error">
            {error}
          </p>
        )}

        <Button
          type="submit"
          disabled={submitting}
          className="mt-2"
          data-testid="register-submit"
        >
          {submitting ? "Creating…" : "Create account"}
        </Button>
      </form>

      <p className="mt-6 text-sm text-[var(--fg-soft)]">
        Already have an account?{" "}
        <Link
          to={`/login?redirect=${encodeURIComponent(redirect)}`}
          className="text-[var(--accent)] underline"
        >
          Sign in
        </Link>
        .
      </p>
    </article>
  );
}
