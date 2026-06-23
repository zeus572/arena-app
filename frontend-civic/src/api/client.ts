import axios from "axios";
import { attach401Refresh, getFreshAccessToken } from "@/auth/tokenManager";
import { notifyEmailUnverified } from "@/auth/emailVerificationGate";

const USER_ID_KEY = "civic-user-id";

export function getAnonymousUserId(): string {
  let id = localStorage.getItem(USER_ID_KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(USER_ID_KEY, id);
  }
  return id;
}

export const civicApi = axios.create({
  baseURL: import.meta.env.VITE_CIVIC_API_URL ?? "http://localhost:5050/api",
  headers: { "Content-Type": "application/json" },
});

civicApi.interceptors.request.use(async (config) => {
  // Proactively renew an expired/near-expiry token BEFORE attaching it. Civic's
  // open endpoints return 200-with-anonymous for a stale token (never 401), so
  // without this the caller silently degrades to the "anonymous" identity even
  // though they're signed in. getFreshAccessToken refreshes (single-flight) when
  // needed and returns null only when truly logged out / refresh failed.
  const token = await getFreshAccessToken();
  if (token) {
    // Authenticated: send Bearer; the civic backend resolves the user id from
    // the JWT 'sub' claim (minted by the debate backend).
    config.headers.set("Authorization", `Bearer ${token}`);
  } else {
    // Anonymous: fall back to the X-User-Id header derived from localStorage.
    config.headers.set("X-User-Id", getAnonymousUserId());
  }
  return config;
});

// Anti-spam gate: account-bound writes (leagues, campaign manager, coalition acts,
// petitions) return 403 { code: "email_unverified" } when the signed-in user hasn't
// verified their email. Surface a global "verify your email" prompt rather than a
// generic error, then let the original promise reject so callers' own error/loading
// handling still runs.
civicApi.interceptors.response.use(
  (response) => response,
  (error) => {
    const res = error?.response;
    if (res?.status === 403 && res?.data?.code === "email_unverified") {
      notifyEmailUnverified();
    }
    return Promise.reject(error);
  },
);

// Backstop for civic's [Authorize] endpoints (Leagues, Campaign Manager, …).
attach401Refresh(civicApi);
