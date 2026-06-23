import axios from "axios";
import { attach401Refresh, getFreshAccessToken } from "./tokenManager";

// The debate backend (DebateArena) owns identity. The civic frontend talks to
// it for /api/auth/* (register, login, refresh, me). Defaults to the standard
// local dev port; override via VITE_ARENA_API_URL if needed.
export const arenaApi = axios.create({
  baseURL: import.meta.env.VITE_ARENA_API_URL ?? "http://localhost:5000/api",
  headers: { "Content-Type": "application/json" },
});

arenaApi.interceptors.request.use(async (config) => {
  // Attach a proactively-refreshed token (single-flight). Login/register run
  // with no token yet → no header, which is correct. The bare /auth/refresh call
  // lives in tokenManager and never passes through this interceptor.
  const token = await getFreshAccessToken();
  if (token) config.headers.set("Authorization", `Bearer ${token}`);
  return config;
});

attach401Refresh(arenaApi);
