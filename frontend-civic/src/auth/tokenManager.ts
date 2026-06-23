import axios, {
  AxiosHeaders,
  type AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from "axios";

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

// The debate backend owns identity + token issuance/refresh. Keep this in sync
// with arenaAuthClient's baseURL (we use a bare axios call here to avoid routing
// the refresh through an intercepted client, which would recurse).
const ARENA_BASE = import.meta.env.VITE_ARENA_API_URL ?? "http://localhost:5000/api";

// Refresh when the token is within this many seconds of expiring, so we renew
// slightly early and absorb client/server clock skew.
const REFRESH_SKEW_SECONDS = 60;

type Tokens = { accessToken: string; refreshToken: string };

let refreshInFlight: Promise<string | null> | null = null;

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY);
}

export function storeTokens({ accessToken, refreshToken }: Tokens): void {
  localStorage.setItem(ACCESS_KEY, accessToken);
  localStorage.setItem(REFRESH_KEY, refreshToken);
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_KEY);
  localStorage.removeItem(REFRESH_KEY);
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
    } catch {
      // Refresh token expired/revoked → the session is over. Clear so callers
      // fall back to anonymous rather than replaying a dead token.
      clearTokens();
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
