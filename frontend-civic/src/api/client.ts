import axios from "axios";

const USER_ID_KEY = "civic-user-id";
const ACCESS_TOKEN_KEY = "arena-access-token";

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

civicApi.interceptors.request.use((config) => {
  const token = localStorage.getItem(ACCESS_TOKEN_KEY);
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
