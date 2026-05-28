import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/auth/AuthContext";

export default function MagazineLogin() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const redirect = params.get("redirect") ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate(redirect);
    } catch (err) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? "We couldn't sign you in. Check your email and password.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <article className="mx-auto max-w-md" data-testid="magazine-login">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Account
      </p>
      <h1 className="display mt-2 text-4xl">Sign in to Public Lab</h1>
      <p className="mt-3 text-sm text-[var(--fg-soft)]">
        One account gets you Public Lab civics and the Political Arena debate
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

        <button
          type="submit"
          disabled={submitting}
          className="mt-2 rounded-full bg-[var(--accent)] px-6 py-3 text-sm font-semibold text-white disabled:opacity-60"
          data-testid="login-submit"
        >
          {submitting ? "Signing you in…" : "Sign in"}
        </button>
      </form>

      <p className="mt-6 text-sm text-[var(--fg-soft)]">
        New here?{" "}
        <Link
          to={`/register?redirect=${encodeURIComponent(redirect)}`}
          className="text-[var(--accent)] underline"
        >
          Create a Public Lab account
        </Link>
        .
      </p>
    </article>
  );
}
