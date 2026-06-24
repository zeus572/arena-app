import { arenaApi } from "./arenaAuthClient";

// TOTP two-factor enrollment endpoints. These live on the shared Debate Arena
// backend (which owns identity) and require an authenticated session, so they go
// through arenaApi. The login challenge itself is in AuthContext (it runs pre-session).

export interface MfaStatus {
  enabled: boolean;
  enrolledAt: string | null;
  backupCodesRemaining: number;
}

export async function fetchMfaStatus() {
  const res = await arenaApi.get<MfaStatus>("/auth/mfa/status");
  return res.data;
}

export async function mfaSetup() {
  const res = await arenaApi.post<{ secret: string; otpauthUri: string }>("/auth/mfa/setup");
  return res.data;
}

export async function mfaEnable(code: string) {
  const res = await arenaApi.post<{ status: string; backupCodes: string[] }>("/auth/mfa/enable", { code });
  return res.data;
}

export async function mfaDisable(password: string) {
  const res = await arenaApi.post<{ status: string }>("/auth/mfa/disable", { password });
  return res.data;
}

export async function regenerateBackupCodes(password: string) {
  const res = await arenaApi.post<{ backupCodes: string[] }>("/auth/mfa/backup-codes", { password });
  return res.data;
}
