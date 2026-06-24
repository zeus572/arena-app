import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { Button } from "./Button";
import {
  fetchMfaStatus,
  mfaSetup,
  mfaEnable,
  mfaDisable,
  regenerateBackupCodes,
  type MfaStatus,
} from "@/auth/mfaClient";

type Phase = "loading" | "idle" | "setup" | "enabled";

function apiError(err: unknown, fallback: string) {
  return (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback;
}

/** Security section for the Settings page: enroll in / manage TOTP two-factor auth. */
export default function TwoFactorSettings() {
  const [phase, setPhase] = useState<Phase>("loading");
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [secret, setSecret] = useState("");
  const [otpauthUri, setOtpauthUri] = useState("");
  const [enableCode, setEnableCode] = useState("");
  const [backupCodes, setBackupCodes] = useState<string[] | null>(null);
  const [password, setPassword] = useState("");
  const [showDisable, setShowDisable] = useState(false);

  const loadStatus = async () => {
    try {
      const s = await fetchMfaStatus();
      setStatus(s);
      setPhase(s.enabled ? "enabled" : "idle");
    } catch {
      setPhase("idle");
    }
  };

  useEffect(() => {
    void loadStatus();
  }, []);

  const beginSetup = async () => {
    setError(null);
    setBusy(true);
    try {
      const res = await mfaSetup();
      setSecret(res.secret);
      setOtpauthUri(res.otpauthUri);
      setPhase("setup");
    } catch (err) {
      setError(apiError(err, "Couldn't start setup. Try again."));
    } finally {
      setBusy(false);
    }
  };

  const confirmEnable = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await mfaEnable(enableCode);
      setBackupCodes(res.backupCodes);
      setEnableCode("");
      await loadStatus();
    } catch (err) {
      setError(apiError(err, "That code was incorrect. Try again."));
    } finally {
      setBusy(false);
    }
  };

  const confirmDisable = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await mfaDisable(password);
      setPassword("");
      setShowDisable(false);
      setBackupCodes(null);
      await loadStatus();
    } catch (err) {
      setError(apiError(err, "Couldn't disable. Check your password."));
    } finally {
      setBusy(false);
    }
  };

  const confirmRegenerate = async () => {
    if (!password) {
      setError("Enter your password to regenerate codes.");
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const res = await regenerateBackupCodes(password);
      setBackupCodes(res.backupCodes);
      setPassword("");
      await loadStatus();
    } catch (err) {
      setError(apiError(err, "Couldn't regenerate codes. Check your password."));
    } finally {
      setBusy(false);
    }
  };

  const copyCodes = () => {
    if (backupCodes) void navigator.clipboard.writeText(backupCodes.join("\n"));
  };

  const inputClass =
    "border-2 border-[var(--border)] bg-[var(--bg)] px-4 py-2 text-base tracking-widest text-[var(--fg)] outline-none focus:border-[var(--accent)]";

  return (
    <section
      className="mt-8 border border-[var(--border)] bg-[var(--bg-elev)] p-8"
      data-testid="two-factor-section"
    >
      <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
        Security
      </p>
      <h2 className="display mt-2 text-2xl">
        Two-factor authentication
        {status?.enabled && (
          <span className="ml-3 align-middle rounded-sm bg-[var(--accent)] px-1.5 py-0.5 text-[0.65rem] uppercase tracking-wider text-white">
            On
          </span>
        )}
      </h2>

      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}

      {backupCodes && (
        <div className="mt-5 border border-[var(--border)] bg-[var(--bg)] p-5">
          <p className="text-sm font-semibold text-[var(--fg)]">Save your backup codes</p>
          <p className="mt-1 text-xs text-[var(--fg-soft)]">
            Each code works once if you lose your authenticator. They won't be shown again.
          </p>
          <div className="mt-3 grid grid-cols-2 gap-2 font-mono text-sm text-[var(--fg)]">
            {backupCodes.map((c) => (
              <span key={c} className="border border-[var(--border)] px-2 py-1 text-center tracking-wider">{c}</span>
            ))}
          </div>
          <Button type="button" variant="ghost" onClick={copyCodes} className="mt-3">
            Copy all
          </Button>
        </div>
      )}

      {phase === "loading" && <p className="mt-4 text-sm text-[var(--muted)]">Loading…</p>}

      {phase === "idle" && (
        <div className="mt-4">
          <p className="text-sm leading-relaxed text-[var(--fg-soft)]">
            Add a second factor with an authenticator app (Google Authenticator, Authy, 1Password)
            to protect the account you share with Debate Arena.
          </p>
          <Button type="button" onClick={beginSetup} disabled={busy} className="mt-4" data-testid="enable-2fa">
            {busy ? "Starting…" : "Enable 2FA"}
          </Button>
        </div>
      )}

      {phase === "setup" && (
        <form onSubmit={confirmEnable} className="mt-4 flex flex-col gap-4">
          <p className="text-sm text-[var(--fg-soft)]">
            Scan this QR code with your authenticator app, then enter the 6-digit code it shows.
          </p>
          <div className="self-center bg-white p-3">
            <QRCodeSVG value={otpauthUri} size={168} />
          </div>
          <p className="text-center text-xs text-[var(--muted)]">
            Or enter this key manually:
            <br />
            <code className="break-all font-mono text-[var(--fg)]">{secret}</code>
          </p>
          <input
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            placeholder="123456"
            value={enableCode}
            onChange={(e) => setEnableCode(e.target.value)}
            required
            className={inputClass}
            data-testid="enable-2fa-code"
          />
          <div className="flex gap-3">
            <Button type="submit" disabled={busy} data-testid="confirm-enable-2fa">
              {busy ? "Verifying…" : "Verify & enable"}
            </Button>
            <Button type="button" variant="ghost" onClick={() => { setPhase("idle"); setError(null); }}>
              Cancel
            </Button>
          </div>
        </form>
      )}

      {phase === "enabled" && (
        <div className="mt-4 flex flex-col gap-4">
          <p className="text-sm text-[var(--fg-soft)]">
            2FA is on{status?.enrolledAt ? ` since ${new Date(status.enrolledAt).toLocaleDateString()}` : ""}.
            {" "}{status?.backupCodesRemaining ?? 0} backup codes remaining.
          </p>
          <input
            type="password"
            placeholder="Current password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="border-2 border-[var(--border)] bg-[var(--bg)] px-4 py-2 text-base text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            data-testid="2fa-password"
          />
          <div className="flex flex-wrap gap-3">
            <Button type="button" variant="ghost" onClick={confirmRegenerate} disabled={busy}>
              Regenerate backup codes
            </Button>
            {!showDisable ? (
              <Button type="button" variant="danger" onClick={() => { setShowDisable(true); setError(null); }} data-testid="disable-2fa">
                Disable 2FA
              </Button>
            ) : (
              <form onSubmit={confirmDisable} className="flex gap-3">
                <Button type="submit" variant="danger" disabled={busy} data-testid="confirm-disable-2fa">
                  {busy ? "Disabling…" : "Confirm disable"}
                </Button>
                <Button type="button" variant="ghost" onClick={() => setShowDisable(false)}>
                  Cancel
                </Button>
              </form>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
