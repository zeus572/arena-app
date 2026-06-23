import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "../components/Button";
import { arenaApi } from "@/auth/arenaAuthClient";

export default function MagazineResetPassword() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const token = params.get("token") ?? "";

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    if (password !== confirm) {
      setError("Passwords don't match.");
      return;
    }
    setSubmitting(true);
    try {
      await arenaApi.post("/auth/reset-password", { token, newPassword: password });
      setDone(true);
      setTimeout(() => navigate("/login"), 2000);
    } catch (err) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? "Could not reset your password. The link may have expired.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <article className="mx-auto max-w-md" data-testid="magazine-reset-password">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Account
      </p>
      <h1 className="display mt-2 text-4xl">Choose a new password</h1>

      {!token ? (
        <p className="mt-3 text-sm text-red-600">
          This reset link is missing its token. Request a new one from{" "}
          <Link to="/forgot-password" className="text-[var(--accent)] underline">
            the reset page
          </Link>
          .
        </p>
      ) : done ? (
        <p className="mt-3 text-sm text-[var(--fg-soft)]">
          Your password has been reset. Taking you to sign in…
        </p>
      ) : (
        <form onSubmit={onSubmit} className="mt-8 flex flex-col gap-4">
          <label className="flex flex-col gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              New password
            </span>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
              data-testid="reset-password"
            />
          </label>
          <label className="flex flex-col gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              Confirm new password
            </span>
            <input
              type="password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              required
              className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
              data-testid="reset-confirm"
            />
          </label>
          {error && (
            <p className="text-sm text-red-600" data-testid="reset-error">
              {error}
            </p>
          )}
          <Button type="submit" disabled={submitting} className="mt-2" data-testid="reset-submit">
            {submitting ? "Resetting…" : "Reset password"}
          </Button>
        </form>
      )}
    </article>
  );
}
