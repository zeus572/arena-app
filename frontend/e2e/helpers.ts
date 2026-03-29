import { type Page, type APIRequestContext } from "@playwright/test";

const API_BASE = "http://localhost:5000/api";
const DEV_BASE = "http://localhost:5000/dev";

/**
 * Register a fresh test user and return their access token + user info.
 */
export async function registerTestUser(
  request: APIRequestContext,
  suffix?: string
) {
  const id = suffix ?? Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
  const email = `test-${id}@e2e.local`;
  const password = "TestPass123!";
  const displayName = `E2E User ${id}`;

  const res = await request.post(`${API_BASE}/auth/register`, {
    data: { email, password, displayName, inviteCode: "ARENA7X" },
  });

  const body = await res.json();
  return {
    accessToken: body.accessToken as string,
    refreshToken: body.refreshToken as string,
    user: body.user as { id: string; email: string; displayName: string },
    email,
    password,
  };
}

/**
 * Register a fresh test user and upgrade them to Premium via dev endpoint.
 * Returns the user with a refreshed token that includes the Premium claim.
 */
export async function registerPremiumUser(
  request: APIRequestContext,
  suffix?: string
) {
  const user = await registerTestUser(request, suffix);

  // Upgrade to premium via dev endpoint
  await request.post(`${DEV_BASE}/set-premium/${user.user.id}`);

  // Refresh the token so it includes the Premium claim
  const refreshRes = await request.post(`${API_BASE}/auth/refresh`, {
    data: { refreshToken: user.refreshToken },
  });
  const refreshBody = await refreshRes.json();

  return {
    ...user,
    accessToken: refreshBody.accessToken as string,
    refreshToken: refreshBody.refreshToken as string,
  };
}

/**
 * Log in to the app via the UI.
 */
export async function loginViaUI(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByPlaceholder("Email").fill(email);
  await page.getByPlaceholder("Password").fill(password);
  await page.getByRole("button", { name: /sign in|log in/i }).click();
  await page.waitForURL("/");
}

/**
 * Set auth token directly in localStorage so tests can skip the login UI.
 */
export async function setAuthToken(page: Page, token: string) {
  await page.goto("/");
  await page.evaluate((t) => {
    localStorage.setItem("arena-access-token", t);
  }, token);
}

/**
 * Fetch list of agents from the API.
 */
export async function getAgents(request: APIRequestContext) {
  const res = await request.get(`${API_BASE}/agents`);
  return (await res.json()) as Array<{
    id: string;
    name: string;
    persona: string;
  }>;
}

/**
 * Create a debate via the API (requires Premium token) and return its id.
 */
export async function createDebateViaAPI(
  request: APIRequestContext,
  opts: {
    topic: string;
    description?: string;
    proponentId?: string;
    opponentId?: string;
    token: string;
  }
) {
  const res = await request.post(`${API_BASE}/debates`, {
    data: {
      topic: opts.topic,
      description: opts.description,
      proponentId: opts.proponentId,
      opponentId: opts.opponentId,
    },
    headers: {
      Authorization: `Bearer ${opts.token}`,
    },
  });
  const body = await res.json();
  return body as { id: string; topic: string; status: string };
}

/**
 * Fetch a specific debate by id.
 */
export async function getDebate(request: APIRequestContext, debateId: string) {
  const res = await request.get(`${API_BASE}/debates/${debateId}`);
  return await res.json();
}

/**
 * Cast a vote on a debate via API.
 */
export async function castVoteViaAPI(
  request: APIRequestContext,
  debateId: string,
  votedForAgentId: string,
  token?: string
) {
  const headers: Record<string, string> = {};
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  } else {
    headers["X-User-Id"] = crypto.randomUUID();
  }
  const res = await request.post(`${API_BASE}/debates/${debateId}/votes`, {
    data: { votedForAgentId },
    headers,
  });
  return await res.json();
}

/**
 * Add a reaction via API.
 */
export async function addReactionViaAPI(
  request: APIRequestContext,
  targetType: "debates" | "turns",
  targetId: string,
  reactionType: string,
  token?: string
) {
  const headers: Record<string, string> = {};
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  } else {
    headers["X-User-Id"] = crypto.randomUUID();
  }
  const res = await request.post(
    `${API_BASE}/${targetType}/${targetId}/reactions`,
    {
      data: { type: reactionType },
      headers,
    }
  );
  return await res.json();
}
