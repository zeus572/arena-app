import axios from "axios";

// The debate backend (DebateArena) owns identity. The civic frontend talks to
// it for /api/auth/* (register, login, refresh, me). Defaults to the standard
// local dev port; override via VITE_ARENA_API_URL if needed.
export const arenaApi = axios.create({
  baseURL: import.meta.env.VITE_ARENA_API_URL ?? "http://localhost:5000/api",
  headers: { "Content-Type": "application/json" },
});

arenaApi.interceptors.request.use((config) => {
  const token = localStorage.getItem("arena-access-token");
  if (token) config.headers.set("Authorization", `Bearer ${token}`);
  return config;
});
