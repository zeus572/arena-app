import { beforeEach, describe, expect, it, vi } from "vitest";

// Mock axios so the single-flight refresh hits a fake /auth/refresh. `postMock`
// is hoisted so the (hoisted) vi.mock factory can close over it.
const { postMock } = vi.hoisted(() => ({ postMock: vi.fn() }));
vi.mock("axios", () => {
  class AxiosHeaders {
    static from(h: unknown) {
      return h ?? new AxiosHeaders();
    }
    set() {}
  }
  return { default: { post: postMock }, AxiosHeaders };
});

import {
  clearTokens,
  getFreshAccessToken,
  refreshAccessToken,
  storeTokens,
} from "./tokenManager";

// Minimal in-memory localStorage for the node test environment.
const store: Record<string, string> = {};
beforeEach(() => {
  for (const k of Object.keys(store)) delete store[k];
  postMock.mockReset();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => store[k] ?? null,
    setItem: (k: string, v: string) => {
      store[k] = v;
    },
    removeItem: (k: string) => {
      delete store[k];
    },
  });
});

/** Build a JWT whose `exp` is `secondsFromNow` away (only the payload matters). */
function jwt(secondsFromNow: number): string {
  const head = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = btoa(JSON.stringify({ sub: "u1", exp: Math.floor(Date.now() / 1000) + secondsFromNow }));
  return `${head}.${body}.sig`;
}

function refreshResponse() {
  return { data: { accessToken: jwt(3600), refreshToken: "rotated-refresh" } };
}

describe("tokenManager", () => {
  it("returns the existing token without refreshing when it's not near expiry", async () => {
    const token = jwt(3600);
    storeTokens({ accessToken: token, refreshToken: "r1" });

    const result = await getFreshAccessToken();

    expect(result).toBe(token);
    expect(postMock).not.toHaveBeenCalled();
  });

  it("returns null and never refreshes when there is no access token", async () => {
    expect(await getFreshAccessToken()).toBeNull();
    expect(postMock).not.toHaveBeenCalled();
  });

  it("refreshes proactively when the token is expired and stores the rotated pair", async () => {
    postMock.mockResolvedValue(refreshResponse());
    storeTokens({ accessToken: jwt(-10), refreshToken: "r1" });

    const result = await getFreshAccessToken();

    expect(postMock).toHaveBeenCalledTimes(1);
    expect(postMock.mock.calls[0][0]).toMatch(/\/auth\/refresh$/);
    expect(result).toMatch(/^[^.]+\.[^.]+\./); // a fresh JWT
    expect(localStorage.getItem("arena-refresh-token")).toBe("rotated-refresh");
  });

  it("coalesces concurrent refreshes into a single network call (single-flight)", async () => {
    postMock.mockResolvedValue(refreshResponse());
    storeTokens({ accessToken: jwt(-10), refreshToken: "r1" });

    const [a, b, c] = await Promise.all([
      getFreshAccessToken(),
      getFreshAccessToken(),
      getFreshAccessToken(),
    ]);

    expect(postMock).toHaveBeenCalledTimes(1);
    expect(a).toBe(b);
    expect(b).toBe(c);
  });

  it("clears tokens and returns null when the refresh fails", async () => {
    postMock.mockRejectedValue(new Error("refresh token revoked"));
    storeTokens({ accessToken: jwt(-10), refreshToken: "dead" });

    expect(await refreshAccessToken()).toBeNull();
    expect(localStorage.getItem("arena-access-token")).toBeNull();
    expect(localStorage.getItem("arena-refresh-token")).toBeNull();
  });

  it("does not attempt a refresh when there is no refresh token", async () => {
    clearTokens();
    expect(await refreshAccessToken()).toBeNull();
    expect(postMock).not.toHaveBeenCalled();
  });
});
