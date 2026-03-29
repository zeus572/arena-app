import { test, expect } from "@playwright/test";
import {
  getAgents,
  createDebateViaAPI,
  castVoteViaAPI,
  addReactionViaAPI,
  registerTestUser,
  registerPremiumUser,
} from "./helpers";

const API = "http://localhost:5000";

test.describe("API Integration Tests", () => {
  let premiumToken: string;

  test.beforeAll(async ({ request }) => {
    const user = await registerPremiumUser(request, `api-${Date.now()}`);
    premiumToken = user.accessToken;
  });

  test("health endpoint returns healthy status", async ({ request }) => {
    const res = await request.get(`${API}/health`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.status).toBe("healthy");
    expect(body).toHaveProperty("totalDebates");
    expect(body).toHaveProperty("totalTurns");
  });

  test("GET /api/agents returns seeded agents", async ({ request }) => {
    const agents = await getAgents(request);
    expect(agents.length).toBeGreaterThanOrEqual(10);

    const names = agents.map((a) => a.name);
    expect(names).toContain("Max Freedman");
    expect(names).toContain("Rosa Vanguard");
    expect(names).toContain("Edmund Hale");
    expect(names).toContain("Terra Solari");
    expect(names).toContain("Maria Reyes");
    expect(names).toContain("Derek Dawson");
    expect(names).toContain("Priya Chakraborty");
    expect(names).toContain("James Whitfield");
    expect(names).toContain("Danny Roast");
    expect(names).toContain("Professor Dialectica");
  });

  test("GET /api/agents/{id} returns agent detail with personality", async ({
    request,
  }) => {
    const agents = await getAgents(request);
    const res = await request.get(`${API}/api/agents/${agents[0].id}`);
    expect(res.ok()).toBeTruthy();
    const detail = await res.json();

    expect(detail).toHaveProperty("personality");
    expect(detail.personality).toHaveProperty("aggressiveness");
    expect(detail.personality).toHaveProperty("eloquence");
    expect(detail.personality).toHaveProperty("factReliance");
    expect(detail.personality).toHaveProperty("empathy");
    expect(detail.personality).toHaveProperty("wit");
    expect(detail).toHaveProperty("stats");
    expect(detail).toHaveProperty("reactionBreakdown");
    expect(detail).toHaveProperty("topTags");
  });

  test("POST /api/debates creates a new debate (premium)", async ({
    request,
  }) => {
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `API Test Debate ${Date.now()}`,
      description: "Testing debate creation via API",
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    expect(debate.id).toBeTruthy();
    expect(debate.topic).toContain("API Test Debate");
  });

  test("POST /api/debates rejects free users (403)", async ({ request }) => {
    const freeUser = await registerTestUser(request, `free-${Date.now()}`);
    const agents = await getAgents(request);

    const res = await request.post(`${API}/api/debates`, {
      data: {
        topic: "Should be rejected",
        proponentId: agents[0].id,
        opponentId: agents[1].id,
      },
      headers: { Authorization: `Bearer ${freeUser.accessToken}` },
    });
    expect(res.status()).toBe(403);
  });

  test("POST /api/debates rejects unauthenticated users (401)", async ({
    request,
  }) => {
    const res = await request.post(`${API}/api/debates`, {
      data: { topic: "Should be rejected" },
    });
    expect(res.status()).toBe(401);
  });

  test("GET /api/debates/{id} returns debate detail", async ({ request }) => {
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `API Detail Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const res = await request.get(`${API}/api/debates/${debate.id}`);
    expect(res.ok()).toBeTruthy();
    const detail = await res.json();

    expect(detail.topic).toContain("API Detail Test");
    expect(detail.proponent).toBeTruthy();
    expect(detail.opponent).toBeTruthy();
    expect(detail).toHaveProperty("turns");
    expect(detail).toHaveProperty("proponentVotes");
    expect(detail).toHaveProperty("opponentVotes");
    expect(detail).toHaveProperty("reactions");
  });

  test("GET /api/feed returns paginated debates", async ({ request }) => {
    const res = await request.get(`${API}/api/feed?page=1&pageSize=5`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();

    expect(body).toHaveProperty("items");
    expect(body).toHaveProperty("totalCount");
    expect(Array.isArray(body.items)).toBeTruthy();
  });

  test("GET /api/feed supports search query", async ({ request }) => {
    const agents = await getAgents(request);
    const uniqueTerm = `SearchableDebate_${Date.now()}`;
    await createDebateViaAPI(request, {
      topic: uniqueTerm,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const res = await request.get(`${API}/api/feed?q=${uniqueTerm}`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.totalCount).toBeGreaterThanOrEqual(1);
    expect(body.items[0].topic).toContain("SearchableDebate");
  });

  test("GET /api/feed supports sort modes", async ({ request }) => {
    for (const sort of ["hot", "new", "top"]) {
      const res = await request.get(`${API}/api/feed?sort=${sort}`);
      expect(res.ok()).toBeTruthy();
      const body = await res.json();
      expect(body).toHaveProperty("items");
    }
  });

  test("POST /api/debates/{id}/votes casts a vote", async ({ request }) => {
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `Vote Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const result = await castVoteViaAPI(
      request,
      debate.id,
      agents[0].id
    );
    expect(result).toHaveProperty("id");
  });

  test("POST /api/debates/{id}/reactions adds a reaction", async ({
    request,
  }) => {
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `Reaction Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const result = await addReactionViaAPI(
      request,
      "debates",
      debate.id,
      "like"
    );
    expect(result).toHaveProperty("id");
  });

  test("GET /api/agents/leaderboard returns ranked agents", async ({
    request,
  }) => {
    const res = await request.get(
      `${API}/api/agents/leaderboard?sort=top&period=all`
    );
    expect(res.ok()).toBeTruthy();
    const body = await res.json();

    expect(body).toHaveProperty("sort", "top");
    expect(body).toHaveProperty("period", "all");
    expect(body).toHaveProperty("agents");
    expect(Array.isArray(body.agents)).toBeTruthy();
  });

  test("GET /api/agents/leaderboard supports all sort modes", async ({
    request,
  }) => {
    for (const sort of [
      "top",
      "controversial",
      "underrated",
      "winrate",
      "reactions",
    ]) {
      const res = await request.get(
        `${API}/api/agents/leaderboard?sort=${sort}&period=all`
      );
      expect(res.ok()).toBeTruthy();
    }
  });

  test("GET /api/feed/trending returns trending topics", async ({
    request,
  }) => {
    const res = await request.get(`${API}/api/feed/trending`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(Array.isArray(body)).toBeTruthy();
  });

  test("GET /api/sources returns citation and news sources", async ({
    request,
  }) => {
    const res = await request.get(`${API}/api/sources`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();

    expect(body).toHaveProperty("citations");
    expect(body).toHaveProperty("news");
    expect(Array.isArray(body.citations)).toBeTruthy();
    expect(Array.isArray(body.news)).toBeTruthy();
  });

  test("GET /api/topics returns topic proposals", async ({ request }) => {
    const res = await request.get(`${API}/api/topics`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toHaveProperty("items");
    expect(body).toHaveProperty("totalCount");
  });

  test("POST /api/auth/register creates a new user", async ({ request }) => {
    const user = await registerTestUser(request);
    expect(user.accessToken).toBeTruthy();
    expect(user.user.email).toContain("e2e.local");
  });

  test("GET /api/debates/{id}/predictions returns prediction data", async ({
    request,
  }) => {
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `Prediction Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const res = await request.get(
      `${API}/api/debates/${debate.id}/predictions`
    );
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toHaveProperty("totalPredictions");
    expect(body).toHaveProperty("proponentOdds");
    expect(body).toHaveProperty("opponentOdds");
  });

  test("GET /api/debates/{id}/interventions returns interventions", async ({
    request,
  }) => {
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `Intervention Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const res = await request.get(
      `${API}/api/debates/${debate.id}/interventions`
    );
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(Array.isArray(body)).toBeTruthy();
  });

  test("POST /api/debates/{id}/interventions rejects free users (403)", async ({
    request,
  }) => {
    const freeUser = await registerTestUser(request, `int-free-${Date.now()}`);
    const agents = await getAgents(request);
    const debate = await createDebateViaAPI(request, {
      topic: `Intervention Auth Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    const res = await request.post(
      `${API}/api/debates/${debate.id}/interventions`,
      {
        data: { content: "This is a test crowd question?" },
        headers: { Authorization: `Bearer ${freeUser.accessToken}` },
      }
    );
    expect(res.status()).toBe(403);
  });

  test("POST /api/debates/{id}/interventions rejects unauthenticated (401)", async ({
    request,
  }) => {
    const res = await request.post(
      `${API}/api/debates/00000000-0000-0000-0000-000000000001/interventions`,
      {
        data: { content: "This should be rejected" },
      }
    );
    expect(res.status()).toBe(401);
  });

  test("GET /api/share/turn/{turnId} returns 404 for nonexistent turn", async ({
    request,
  }) => {
    const res = await request.get(
      `${API}/api/share/turn/00000000-0000-0000-0000-000000000000`
    );
    expect(res.status()).toBe(404);
  });
});
