import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button, ButtonLink } from "../components/Button";
import TwoFactorSettings from "../components/TwoFactorSettings";
import { useAuth } from "@/auth/AuthContext";
import { arenaApi } from "@/auth/arenaAuthClient";
import {
  getMyProfile,
  setMyLocality,
  LOCALITIES,
  type Profile,
} from "@/api/profile";

/**
 * Account + settings. Deliberately separate from the Civic Compass (which lives
 * at /profile): this page is for who you are and how the app is configured —
 * name, email, locality, sign-in — and links out to the Compass.
 */
export default function MagazineSettings() {
  const { user, isAuthenticated, isLoading, logout, refreshUser } = useAuth();
  const navigate = useNavigate();

  const [profile, setProfile] = useState<Profile | null>(null);
  const [displayName, setDisplayName] = useState("");
  const [savingName, setSavingName] = useState(false);
  const [savingLocality, setSavingLocality] = useState(false);
  const [nameSaved, setNameSaved] = useState(false);
  const [nameError, setNameError] = useState<string | null>(null);
  const [localityError, setLocalityError] = useState<string | null>(null);
  const [verifying, setVerifying] = useState(false);
  const [verifyMsg, setVerifyMsg] = useState<string | null>(null);

  useEffect(() => {
    void getMyProfile().then(setProfile);
  }, []);

  useEffect(() => {
    setDisplayName(user?.displayName ?? "");
  }, [user?.displayName]);

  async function resendVerification() {
    setVerifying(true);
    setVerifyMsg(null);
    try {
      // Sends a real verification email via the shared Debate Arena backend; the
      // link in it lands on /verify-email here in Civic.
      await arenaApi.post("/auth/resend-verification", { app: "civic" });
      setVerifyMsg("Verification email sent — check your inbox.");
    } catch {
      setVerifyMsg("Couldn't send the email. Please try again.");
    } finally {
      setVerifying(false);
    }
  }

  async function saveDisplayName() {
    setSavingName(true);
    setNameSaved(false);
    setNameError(null);
    try {
      await arenaApi.put("/profile/me", { displayName });
      await refreshUser();
      setNameSaved(true);
    } catch {
      setNameError("Couldn't save your name — please try again.");
    } finally {
      setSavingName(false);
    }
  }

  async function changeLocality(value: string) {
    setSavingLocality(true);
    setLocalityError(null);
    try {
      const updated = await setMyLocality(value);
      setProfile(updated);
    } catch {
      setLocalityError("Couldn't update your locality — please try again.");
    } finally {
      setSavingLocality(false);
    }
  }

  const localityValue = profile?.localityState ?? "";

  return (
    <article className="mx-auto max-w-2xl" data-testid="settings">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mt-8">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Profile &amp; settings
        </p>
        <h1 className="display mt-3 text-5xl">Your account</h1>
        <p className="mt-4 text-sm text-[var(--fg-soft)]">
          Manage who you are and how Civersify is set up. Your civic values live
          on a separate page — your Civic Compass.
        </p>
      </header>

      {/* Account */}
      <section
        className="mt-10 border border-[var(--border)] bg-[var(--bg-elev)] p-8"
        data-testid="account-section"
      >
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
          Account
        </p>

        {isLoading ? (
          <p className="mt-4 text-sm text-[var(--muted)]">Loading…</p>
        ) : isAuthenticated && user ? (
          <div className="mt-5 space-y-6">
            <label className="block">
              <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                Display name
              </span>
              <div className="mt-2 flex flex-wrap items-center gap-3">
                <input
                  type="text"
                  value={displayName}
                  onChange={(e) => {
                    setDisplayName(e.target.value);
                    setNameSaved(false);
                    setNameError(null);
                  }}
                  data-testid="settings-displayname"
                  className="min-w-[14rem] flex-1 border-2 border-[var(--border)] bg-[var(--bg)] px-4 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
                />
                <Button
                  onClick={saveDisplayName}
                  disabled={
                    savingName || displayName.trim() === (user.displayName ?? "")
                  }
                  data-testid="settings-save-name"
                >
                  {savingName ? "Saving…" : "Save"}
                </Button>
                {nameSaved && (
                  <span className="text-xs text-[var(--accent)]">Saved</span>
                )}
                {nameError && (
                  <span className="text-xs font-semibold text-rose-700" role="alert" data-testid="settings-name-error">
                    {nameError}
                  </span>
                )}
              </div>
            </label>

            <div>
              <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                Email
              </span>
              <p className="mt-1 flex items-center gap-2 text-base text-[var(--fg)]">
                {user.email}
                <span
                  className={`rounded-sm px-1.5 py-0.5 text-[0.65rem] uppercase tracking-wider ${
                    user.emailVerified
                      ? "bg-[var(--accent)] text-white"
                      : "border border-[var(--border)] text-[var(--muted)]"
                  }`}
                >
                  {user.emailVerified ? "Verified" : "Unverified"}
                </span>
              </p>
              <p className="mt-1 text-xs text-[var(--muted)]">
                Email and password are managed through your shared Debate Arena
                account.
              </p>
              {!user.emailVerified && (
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <Button
                    type="button"
                    onClick={resendVerification}
                    disabled={verifying}
                    data-testid="settings-resend-verification"
                  >
                    {verifying ? "Sending…" : "Send verification link"}
                  </Button>
                  {verifyMsg && (
                    <span className="text-xs text-[var(--accent)]">{verifyMsg}</span>
                  )}
                </div>
              )}
            </div>

            <div className="flex items-center justify-between border-t border-[var(--border)] pt-4">
              <span className="text-xs uppercase tracking-wider text-[var(--muted)]">
                Plan · {user.plan}
              </span>
              <button
                type="button"
                onClick={async () => {
                  await logout();
                  navigate("/");
                }}
                data-testid="settings-logout"
                className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
              >
                Log out
              </button>
            </div>
          </div>
        ) : (
          <div className="mt-5">
            <p className="text-sm text-[var(--fg-soft)]">
              You're browsing as a guest. Sign in to save your profile, name, and
              email across Civersify and Debate Arena.
            </p>
            <div className="mt-4 flex gap-3">
              <ButtonLink to="/register" data-testid="settings-signup">
                Create account
              </ButtonLink>
              <ButtonLink to="/login" variant="ghost" data-testid="settings-login">
                Sign in
              </ButtonLink>
            </div>
          </div>
        )}
      </section>

      {/* Security — two-factor authentication (signed-in users only) */}
      {isAuthenticated && user && <TwoFactorSettings />}

      {/* Locality */}
      <section
        className="mt-8 border border-[var(--border)] bg-[var(--bg-elev)] p-8"
        data-testid="locality-editor"
      >
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
          Your locality
        </p>
        <h2 className="display mt-2 text-2xl">Get local stories in your feed</h2>
        <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">
          Pick your state to weave local news into the national issue. Local
          stories — and the coalitions formed around them — are shared only with
          others in your area.
        </p>
        <div className="mt-5 flex flex-wrap items-center gap-3">
          <select
            value={localityValue}
            onChange={(e) => void changeLocality(e.target.value)}
            disabled={savingLocality || !profile}
            data-testid="locality-select"
            className="border-2 border-[var(--border)] bg-[var(--bg)] px-4 py-2 text-base text-[var(--fg)]"
          >
            {LOCALITIES.map((l) => (
              <option key={l.value} value={l.value}>
                {l.label}
              </option>
            ))}
          </select>
          {savingLocality && (
            <span className="text-xs text-[var(--muted)]">Saving…</span>
          )}
          {localityError && (
            <span className="text-xs font-semibold text-rose-700" role="alert" data-testid="settings-locality-error">
              {localityError}
            </span>
          )}
        </div>
      </section>

      {/* Civic Compass link-out */}
      <section
        className="mt-8 border border-[var(--border)] bg-[var(--bg-elev)] p-8"
        data-testid="compass-link"
      >
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
          Your civic values
        </p>
        <h2 className="display mt-2 text-2xl">Civic Compass</h2>
        <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">
          Where you sit across ten value axes — built from your answers, not a
          party label. It lives on its own page.
        </p>
        <div className="mt-5">
          <ButtonLink to="/profile" data-testid="settings-view-compass">
            View your Civic Compass →
          </ButtonLink>
        </div>
      </section>
    </article>
  );
}
