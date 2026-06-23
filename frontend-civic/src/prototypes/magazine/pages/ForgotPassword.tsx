import { useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../components/Button";
import { arenaApi } from "@/auth/arenaAuthClient";

export default function MagazineForgotPassword() {
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      // Neutral by design: the API never reveals whether the address is registered.
      await arenaApi.post("/auth/forgot-password", { email, app: "civic" });
    } catch {
      // Swallow — still show the same confirmation.
    } finally {
      setSubmitting(false);
      setSubmitted(true);
    }
  };

  return (
    <article className="mx-auto max-w-md" data-testid="magazine-forgot-password">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Account
      </p>
      <h1 className="display mt-2 text-4xl">Reset your password</h1>

      {submitted ? (
        <>
          <p className="mt-3 text-sm text-[var(--fg-soft)]">
            If an account exists for <span className="text-[var(--fg)]">{email}</span>, a reset
            link is on its way. Check your inbox and spam folder.
          </p>
          <p className="mt-6 text-sm text-[var(--fg-soft)]">
            <Link to="/login" className="text-[var(--accent)] underline">
              Back to sign in
            </Link>
          </p>
        </>
      ) : (
        <>
          <p className="mt-3 text-sm text-[var(--fg-soft)]">
            Enter the email on your account and we'll send you a link to choose a new password.
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
                data-testid="forgot-email"
              />
            </label>
            <Button type="submit" disabled={submitting} className="mt-2" data-testid="forgot-submit">
              {submitting ? "Sending…" : "Send reset link"}
            </Button>
          </form>
          <p className="mt-6 text-sm text-[var(--fg-soft)]">
            <Link to="/login" className="text-[var(--accent)] underline">
              Back to sign in
            </Link>
          </p>
        </>
      )}
    </article>
  );
}
