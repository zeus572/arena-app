import axios, {
  AxiosHeaders,
  type AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from "axios";
import { getStoredItem, removeStoredItem, setStoredItem } from "@/lib/persistentStorage";

// ---------------------------------------------------------------------------
// Shared access-token lifecycle for BOTH API clients (civic + arena).
//
// The access token is a short-lived (60-min) JWT minted by the debate backend.
// Before this, nothing refreshed it during a live SPA session: AuthContext only
// refreshed on mount, and neither axios client renewed it. So once the token
// aged out, civic's open endpoints silently downgraded the caller to the
// "anonymous" identity (200, not 401 — so no error surfaced) while the UI still
// showed the user as signed in. See the HAR investigation: pre-refresh civic
// calls resolved `anonymous`, post-refresh the same calls resolved the real id.
//
// Fix: proactively refresh BEFORE attaching the token when it's expired/near
// expiry (covers the silent 200-anonymous case the request never 401s on), with
// a single-flight guard so concurrent requests share one network refresh, plus a
// 401 response backstop for the [Authorize] endpoints.
// ---------------------------------------------------------------------------

const ACCESS_KEY = "arena-access-token";
const REFRESH_KEY = "arena-refresh-token";
// Identifies a "remembered" device for the 2FA bypass. Deliberately NOT cleared on
// logout — it identifies the device, not the session — so 2FA stays bypassed here.
const TRUSTED_DEVICE_KEY = "arena-trusted-device-token";

// The debate backend owns identity + token issuance/refresh. Keep this in sync
// with arenaAuthClient's baseURL (we use a bare axios call here to avoid routing
// the refresh through an intercepted client, which would recurse).
const ARENA_BASE = import.meta.env.VITE_ARENA_API_URL ?? "http://localhost:5000/api";

// Refresh when the token is within this many seconds of expiring, so we renew
// slightly early and absorb client/server clock skew.
const REFRESH_SKEW_SECONDS = 60;

type Tokens = { accessToken: string; refreshToken: string };

let refreshInFlight: Promise<string | null> | null = null;

/** HTTP status of an (axios-shaped) error response, or null for network-level failures. */
export function errorStatus(err: unknown): number | null {
  const res = (err as { response?: { status?: number } } | null)?.response;
  return typeof res?.status === "number" ? res.status : null;
}

/**
 * True when the backend saw the credentials and refused them (expired/revoked
 * refresh token, bad request). False for anything transient — offline, DNS,
 * or a 5xx such as the backend's 503 startup readiness gate — where the
 * session may still be perfectly valid.
 */
function isDefinitiveAuthRejection(err: unknown): boolean {
  const status = errorStatus(err);
  return status === 400 || status === 401 || status === 403;
}

export function getAccessToken(): string | null {
  return getStoredItem(ACCESS_KEY);
}

export function getRefreshToken(): string | null {
  return getStoredItem(REFRESH_KEY);
}

export function storeTokens({ accessToken, refreshToken }: Tokens): void {
  setStoredItem(ACCESS_KEY, accessToken);
  setStoredItem(REFRESH_KEY, refreshToken);
}

export function clearTokens(): void {
  removeStoredItem(ACCESS_KEY);
  removeStoredItem(REFRESH_KEY);
}

export function getTrustedDeviceToken(): string | null {
  return getStoredItem(TRUSTED_DEVICE_KEY);
}

export function storeTrustedDeviceToken(token: string): void {
  setStoredItem(TRUSTED_DEVICE_KEY, token);
}

/** Decode a JWT's `exp` (seconds since epoch), or null if it can't be read. */
function readExp(token: string): number | null {
  try {
    const part = token.split(".")[1];
    if (!part) return null;
    let b64 = part.replace(/-/g, "+").replace(/_/g, "/");
    b64 += "=".repeat((4 - (b64.length % 4)) % 4);
    const claims = JSON.parse(atob(b64)) as { exp?: number };
    return typeof claims.exp === "number" ? claims.exp : null;
  } catch {
    return null;
  }
}

function isExpiredOrNear(token: string): boolean {
  const exp = readExp(token);
  if (exp === null) return false; // unreadable — let the server be the judge
  return exp - Date.now() / 1000 <= REFRESH_SKEW_SECONDS;
}

/**
 * Refresh the access token against the debate backend. Single-flight: concurrent
 * callers share one in-flight network request. Returns the new access token, or
 * null when there's no refresh token or the refresh failed (tokens are cleared).
 */
export function refreshAccessToken(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    const refreshToken = getRefreshToken();
    if (!refreshToken) return null;
    try {
      // Bare axios — must NOT go through an intercepted client (would recurse).
      const { data } = await axios.post<Tokens>(
        `${ARENA_BASE}/auth/refresh`,
        { refreshToken },
        { headers: { "Content-Type": "application/json" } },
      );
      storeTokens(data);
      return data.accessToken;
    } catch (err) {
      // Refresh token expired/revoked → the session is over. Clear so callers
      // fall back to anonymous rather than replaying a dead token. But ONLY on
      // a definitive rejection: a transient failure (offline, 503 while the
      // backend cold-starts) must keep the tokens so a later attempt recovers
      // instead of silently logging the user out.
      if (isDefinitiveAuthRejection(err)) clearTokens();
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

/**
 * Return a usable (non-expired) access token, refreshing proactively if the
 * current one is missing/expired/near-expiry. Null when unauthenticated.
 */
export async function getFreshAccessToken(): Promise<string | null> {
  const token = getAccessToken();
  if (!token) return null;
  if (isExpiredOrNear(token)) return refreshAccessToken();
  return token;
}

/**
 * Backstop for the `[Authorize]` endpoints: on a 401, refresh once and replay
 * the request. The proactive request interceptor handles the common case (and
 * the silent 200-anonymous case the response interceptor can't see); this covers
 * the residual race where a token expires server-side mid-flight. Auth-flow
 * endpoints are skipped — their 401 is a credential failure, not a stale token,
 * and `/auth/refresh` is a bare call that never reaches here anyway.
 */
export function attach401Refresh(instance: AxiosInstance): void {
  instance.interceptors.response.use(
    (res) => res,
    async (error: AxiosError) => {
      const config = error.config as
        | (InternalAxiosRequestConfig & { _retried?: boolean })
        | undefined;
      const url = config?.url ?? "";
      const isAuthFlow = /\/auth\/(login|register|refresh)/.test(url);

      if (error.response?.status === 401 && config && !config._retried && !isAuthFlow) {
        const fresh = await refreshAccessToken();
        if (fresh) {
          config._retried = true;
          config.headers = AxiosHeaders.from(config.headers);
          config.headers.set("Authorization", `Bearer ${fresh}`);
          return instance(config);
        }
      }
      return Promise.reject(error);
    },
  );
}
