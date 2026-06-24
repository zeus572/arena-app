import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { Button } from "@/components/ui/button";
import {
  fetchMfaStatus,
  mfaSetup,
  mfaEnable,
  mfaDisable,
  regenerateBackupCodes,
  type MfaStatus,
} from "@/api/client";
import { ShieldCheck, ShieldOff, KeyRound, Copy, CheckCircle } from "lucide-react";

type Phase = "loading" | "idle" | "setup" | "enabled";

function apiError(err: unknown, fallback: string) {
  return (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback;
}

/** Account-security card: enroll in / manage TOTP two-factor authentication. */
export default function TwoFactorSettings() {
  const [phase, setPhase] = useState<Phase>("loading");
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Setup-phase state (after /setup, before /enable).
  const [secret, setSecret] = useState("");
  const [otpauthUri, setOtpauthUri] = useState("");
  const [enableCode, setEnableCode] = useState("");

  // Codes shown exactly once, right after enabling or regenerating.
  const [backupCodes, setBackupCodes] = useState<string[] | null>(null);

  // Disable / regenerate require re-entering the password.
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
    loadStatus();
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
    if (backupCodes) navigator.clipboard.writeText(backupCodes.join("\n"));
  };

  return (
    <div className="rounded-xl border border-border bg-card p-6 mb-4">
      <div className="flex items-center gap-2 mb-4">
        <KeyRound size={16} className="text-primary" />
        <h2 className="text-sm font-bold text-card-foreground">Two-Factor Authentication</h2>
        {status?.enabled && (
          <span className="ml-auto flex items-center gap-1 rounded-full bg-green-500/10 px-2 py-0.5 text-[10px] font-semibold text-green-600">
            <ShieldCheck size={10} /> On
          </span>
        )}
      </div>

      {error && <p className="text-xs text-destructive mb-3">{error}</p>}

      {/* One-time backup codes display (after enable / regenerate) */}
      {backupCodes && (
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-4 mb-4">
          <p className="text-xs font-semibold text-amber-600 dark:text-amber-400 mb-1">
            Save your backup codes
          </p>
          <p className="text-[11px] text-muted-foreground mb-3">
            Each code works once if you lose your authenticator. Store them somewhere safe —
            they won't be shown again.
          </p>
          <div className="grid grid-cols-2 gap-1.5 font-mono text-xs text-foreground mb-3">
            {backupCodes.map((c) => (
              <span key={c} className="rounded bg-secondary/60 px-2 py-1 text-center tracking-wider">{c}</span>
            ))}
          </div>
          <Button variant="outline" size="sm" className="text-xs gap-1" onClick={copyCodes}>
            <Copy size={12} /> Copy all
          </Button>
        </div>
      )}

      {phase === "loading" && <p className="text-xs text-muted-foreground">Loading…</p>}

      {phase === "idle" && (
        <>
          <p className="text-xs text-muted-foreground mb-3">
            Add a second factor with an authenticator app (Google Authenticator, Authy, 1Password)
            to protect your account.
          </p>
          <Button size="sm" className="text-xs" disabled={busy} onClick={beginSetup}>
            {busy ? "Starting…" : "Enable 2FA"}
          </Button>
        </>
      )}

      {phase === "setup" && (
        <div className="flex flex-col gap-4">
          <p className="text-xs text-muted-foreground">
            Scan this QR code with your authenticator app, then enter the 6-digit code it shows.
          </p>
          <div className="self-center rounded-lg bg-white p-3">
            <QRCodeSVG value={otpauthUri} size={160} />
          </div>
          <div className="text-center">
            <p className="text-[11px] text-muted-foreground mb-1">Or enter this key manually:</p>
            <code className="text-xs font-mono break-all text-foreground">{secret}</code>
          </div>
          <form onSubmit={confirmEnable} className="flex flex-col gap-3">
            <input
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              placeholder="123456"
              value={enableCode}
              onChange={(e) => setEnableCode(e.target.value)}
              required
              className="rounded-lg border border-border bg-background px-3 py-2 text-sm tracking-widest text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
            <div className="flex gap-2">
              <Button type="submit" size="sm" className="text-xs" disabled={busy}>
                {busy ? "Verifying…" : "Verify & enable"}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="text-xs"
                onClick={() => { setPhase("idle"); setError(null); }}
              >
                Cancel
              </Button>
            </div>
          </form>
        </div>
      )}

      {phase === "enabled" && (
        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <CheckCircle size={14} className="text-green-600" />
            <span>
              2FA is on{status?.enrolledAt ? ` since ${new Date(status.enrolledAt).toLocaleDateString()}` : ""}.
              {" "}{status?.backupCodesRemaining ?? 0} backup codes remaining.
            </span>
          </div>

          {/* Password gate reused for regenerate + disable */}
          <input
            type="password"
            placeholder="Current password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" className="text-xs gap-1" disabled={busy} onClick={confirmRegenerate}>
              <KeyRound size={12} /> Regenerate backup codes
            </Button>
            {!showDisable ? (
              <Button
                variant="outline"
                size="sm"
                className="text-xs gap-1 text-destructive"
                onClick={() => { setShowDisable(true); setError(null); }}
              >
                <ShieldOff size={12} /> Disable 2FA
              </Button>
            ) : (
              <form onSubmit={confirmDisable} className="flex gap-2">
                <Button type="submit" variant="destructive" size="sm" className="text-xs" disabled={busy}>
                  {busy ? "Disabling…" : "Confirm disable"}
                </Button>
                <Button type="button" variant="ghost" size="sm" className="text-xs" onClick={() => setShowDisable(false)}>
                  Cancel
                </Button>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
