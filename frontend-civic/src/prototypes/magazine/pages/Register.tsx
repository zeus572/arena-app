import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "../components/Button";
import { useAuth } from "@/auth/AuthContext";
import { setMyDemographics } from "@/api/profile";
import { computeAge, ageRangeFromDob, MINIMUM_SIGNUP_AGE } from "@/lib/age";
import { TERMS_VERSION } from "@/lib/terms";

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
  const [zipCode, setZipCode] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [agreedToTerms, setAgreedToTerms] = useState(false);
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
    if (!/^\d{5}$/.test(zipCode)) {
      setError("Please enter a 5-digit ZIP code.");
      return;
    }
    const age = computeAge(dateOfBirth);
    if (age === null) {
      setError("Please enter a valid date of birth.");
      return;
    }
    if (age < MINIMUM_SIGNUP_AGE) {
      setError(`You must be at least ${MINIMUM_SIGNUP_AGE} to create an account.`);
      return;
    }
    if (!agreedToTerms) {
      setError("Please agree to the Terms of Service and Privacy Policy.");
      return;
    }
    setSubmitting(true);
    try {
      await register(email, password, displayName, inviteCode, dateOfBirth, TERMS_VERSION);
      // Persist the sign-up personalization fields to the civic profile (keyed by
      // the new user identity). The local-news region is derived from the ZIP
      // server-side, and the age bucket is derived from the DOB. Non-fatal: a
      // failure here shouldn't block sign-up.
      try {
        await setMyDemographics(zipCode, ageRangeFromDob(dateOfBirth));
      } catch {
        /* editable later from the profile page */
      }
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

        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            ZIP code
          </span>
          <input
            type="text"
            inputMode="numeric"
            value={zipCode}
            onChange={(e) => setZipCode(e.target.value.replace(/\D/g, "").slice(0, 5))}
            required
            maxLength={5}
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base tracking-widest text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="register-zip"
          />
          <span className="text-xs text-[var(--muted)]">
            Surfaces the races and local stories that affect you. You can change
            this anytime in your profile.
          </span>
        </label>

        <label className="flex flex-col gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Date of birth
          </span>
          <input
            type="date"
            value={dateOfBirth}
            onChange={(e) => setDateOfBirth(e.target.value)}
            required
            max={new Date().toISOString().slice(0, 10)}
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="register-dob"
          />
          <span className="text-xs text-[var(--muted)]">
            You must be at least {MINIMUM_SIGNUP_AGE} to create an account. Helps
            us tune the experience to your perspective.
          </span>
        </label>

        <label className="flex items-start gap-2 text-sm text-[var(--fg-soft)]">
          <input
            type="checkbox"
            checked={agreedToTerms}
            onChange={(e) => setAgreedToTerms(e.target.checked)}
            required
            className="mt-1 h-4 w-4 shrink-0 accent-[var(--accent)]"
            data-testid="register-terms"
          />
          <span>
            I agree to the{" "}
            <Link to="/terms" target="_blank" className="text-[var(--accent)] underline">
              Terms of Service
            </Link>{" "}
            and{" "}
            <Link to="/privacy" target="_blank" className="text-[var(--accent)] underline">
              Privacy Policy
            </Link>
            .
          </span>
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
