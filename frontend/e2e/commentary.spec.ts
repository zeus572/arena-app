import { test, expect } from "@playwright/test";

const FAKE_DEBATE_ID = "00000000-0000-0000-0000-000000000099";
const PROPONENT_ID = "00000000-0000-0000-0000-000000000001";
const OPPONENT_ID = "00000000-0000-0000-0000-000000000002";

function makeDebateResponse(turns: object[]) {
  return {
    id: FAKE_DEBATE_ID,
    topic: "Should we fund space exploration?",
    description: null,
    status: "Active",
    source: "bot",
    newsInfo: null,
    createdAt: new Date().toISOString(),
    proponent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null, persona: "progressive" },
    opponent: { id: OPPONENT_ID, name: "Opponent Agent", avatarUrl: null, persona: "conservative" },
    proponentVotes: 0,
    opponentVotes: 0,
    reactions: {},
    turns,
  };
}

function makeTurn(overrides: Record<string, unknown>) {
  return {
    debateId: FAKE_DEBATE_ID,
    citationsJson: null,
    analysisJson: null,
    createdAt: new Date().toISOString(),
    reactions: {},
    ...overrides,
  };
}

test.describe("Commentary Booth (TLDR)", () => {
  test("commentary booth renders at the top with both commentators", async ({ page }) => {
    const debate = makeDebateResponse([
      makeTurn({
        id: "turn-arg-1", agentId: PROPONENT_ID,
        agent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null },
        turnNumber: 1, type: "Argument",
        content: "Here is my opening argument for this debate topic.",
      }),
      makeTurn({
        id: "turn-arg-2", agentId: OPPONENT_ID,
        agent: { id: OPPONENT_ID, name: "Opponent Agent", avatarUrl: null },
        turnNumber: 2, type: "Argument",
        content: "Here is my counter-argument on this important issue.",
      }),
      makeTurn({
        id: "turn-comm-1", agentId: "a1a00000-0000-0000-0000-000000000020",
        agent: { id: "a1a00000-0000-0000-0000-000000000020", name: "Alex 'The Analyst' Chen", avatarUrl: null },
        turnNumber: 3, type: "Commentary",
        content: "Strong opening from both sides — Proponent came out swinging with data.",
      }),
      makeTurn({
        id: "turn-comm-2", agentId: "a1a00000-0000-0000-0000-000000000021",
        agent: { id: "a1a00000-0000-0000-0000-000000000021", name: "Jordan 'Hype' Williams", avatarUrl: null },
        turnNumber: 4, type: "Commentary",
        content: "Oh this is going to be a GREAT debate, folks! Both sides bringing the heat!",
      }),
    ]);

    await page.route(`**/api/debates/${FAKE_DEBATE_ID}`, (route) =>
      route.fulfill({ json: debate })
    );

    await page.goto(`/debates/${FAKE_DEBATE_ID}`);
    await page.waitForTimeout(1000);

    // Commentary booth should be visible with TLDR header
    const boothHeader = page.getByText("TLDR — Commentary Booth");
    await expect(boothHeader).toBeVisible();

    // Both commentators should be visible inside the booth
    await expect(page.getByText("Alex 'The Analyst' Chen")).toBeVisible();
    await expect(page.getByText("Jordan 'Hype' Williams")).toBeVisible();

    // Commentary content should be visible
    await expect(page.getByText(/Strong opening from both sides/)).toBeVisible();
    await expect(page.getByText(/GREAT debate, folks/)).toBeVisible();
  });

  test("commentary booth appears before debate turns in the DOM", async ({ page }) => {
    const debate = makeDebateResponse([
      makeTurn({
        id: "turn-a1", agentId: PROPONENT_ID,
        agent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null },
        turnNumber: 1, type: "Argument",
        content: "First argument text here.",
      }),
      makeTurn({
        id: "turn-c1", agentId: "a1a00000-0000-0000-0000-000000000020",
        agent: { id: "a1a00000-0000-0000-0000-000000000020", name: "Alex 'The Analyst' Chen", avatarUrl: null },
        turnNumber: 2, type: "Commentary",
        content: "Interesting opening move.",
      }),
      makeTurn({
        id: "turn-c2", agentId: "a1a00000-0000-0000-0000-000000000021",
        agent: { id: "a1a00000-0000-0000-0000-000000000021", name: "Jordan 'Hype' Williams", avatarUrl: null },
        turnNumber: 3, type: "Commentary",
        content: "Let's see where this goes!",
      }),
    ]);

    await page.route(`**/api/debates/${FAKE_DEBATE_ID}`, (route) =>
      route.fulfill({ json: debate })
    );

    await page.goto(`/debates/${FAKE_DEBATE_ID}`);
    await page.waitForTimeout(1000);

    // Booth should be positioned before the turns section
    const booth = page.getByText("TLDR — Commentary Booth");
    const turnsSection = page.locator('section[aria-label="Debate turns"]');
    await expect(booth).toBeVisible();
    await expect(turnsSection).toBeVisible();

    const boothBox = await booth.boundingBox();
    const turnsBox = await turnsSection.boundingBox();
    expect(boothBox!.y).toBeLessThan(turnsBox!.y);
  });

  test("commentary turns are not rendered inline in the turn list", async ({ page }) => {
    const debate = makeDebateResponse([
      makeTurn({
        id: "turn-x1", agentId: PROPONENT_ID,
        agent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null },
        turnNumber: 1, type: "Argument",
        content: "Argument turn content.",
      }),
      makeTurn({
        id: "turn-x2", agentId: "a1a00000-0000-0000-0000-000000000020",
        agent: { id: "a1a00000-0000-0000-0000-000000000020", name: "Alex 'The Analyst' Chen", avatarUrl: null },
        turnNumber: 2, type: "Commentary",
        content: "Commentary only in the booth.",
      }),
    ]);

    await page.route(`**/api/debates/${FAKE_DEBATE_ID}`, (route) =>
      route.fulfill({ json: debate })
    );

    await page.goto(`/debates/${FAKE_DEBATE_ID}`);
    await page.waitForTimeout(1000);

    // Commentary should be in the booth
    await expect(page.getByText("TLDR — Commentary Booth")).toBeVisible();

    // Inside the turns section, commentary should NOT appear
    const turnsSection = page.locator('section[aria-label="Debate turns"]');
    const commentaryInTurns = turnsSection.getByText("Commentary only in the booth.");
    await expect(commentaryInTurns).toHaveCount(0);
  });

  test("debate with no commentary does not show the booth", async ({ page }) => {
    const debate = makeDebateResponse([
      makeTurn({
        id: "turn-nocomm", agentId: PROPONENT_ID,
        agent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null },
        turnNumber: 1, type: "Argument",
        content: "Just an argument, no commentary.",
      }),
    ]);

    await page.route(`**/api/debates/${FAKE_DEBATE_ID}`, (route) =>
      route.fulfill({ json: debate })
    );

    await page.goto(`/debates/${FAKE_DEBATE_ID}`);
    await page.waitForTimeout(1000);

    // Booth should not appear
    await expect(page.getByText("TLDR — Commentary Booth")).toHaveCount(0);
  });

  test("multiple commentary rounds all appear in the booth", async ({ page }) => {
    const debate = makeDebateResponse([
      makeTurn({
        id: "turn-a1", agentId: PROPONENT_ID,
        agent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null },
        turnNumber: 1, type: "Argument", content: "First argument.",
      }),
      makeTurn({
        id: "turn-a2", agentId: OPPONENT_ID,
        agent: { id: OPPONENT_ID, name: "Opponent Agent", avatarUrl: null },
        turnNumber: 2, type: "Argument", content: "First counter.",
      }),
      makeTurn({
        id: "turn-c1", agentId: "a1a00000-0000-0000-0000-000000000020",
        agent: { id: "a1a00000-0000-0000-0000-000000000020", name: "Alex 'The Analyst' Chen", avatarUrl: null },
        turnNumber: 3, type: "Commentary", content: "Round 1 analysis from Alex.",
      }),
      makeTurn({
        id: "turn-c2", agentId: "a1a00000-0000-0000-0000-000000000021",
        agent: { id: "a1a00000-0000-0000-0000-000000000021", name: "Jordan 'Hype' Williams", avatarUrl: null },
        turnNumber: 4, type: "Commentary", content: "Round 1 hype from Jordan.",
      }),
      makeTurn({
        id: "turn-a3", agentId: PROPONENT_ID,
        agent: { id: PROPONENT_ID, name: "Proponent Agent", avatarUrl: null },
        turnNumber: 5, type: "Argument", content: "Second argument.",
      }),
      makeTurn({
        id: "turn-a4", agentId: OPPONENT_ID,
        agent: { id: OPPONENT_ID, name: "Opponent Agent", avatarUrl: null },
        turnNumber: 6, type: "Argument", content: "Second counter.",
      }),
      makeTurn({
        id: "turn-c3", agentId: "a1a00000-0000-0000-0000-000000000020",
        agent: { id: "a1a00000-0000-0000-0000-000000000020", name: "Alex 'The Analyst' Chen", avatarUrl: null },
        turnNumber: 7, type: "Commentary", content: "Round 2 analysis from Alex.",
      }),
      makeTurn({
        id: "turn-c4", agentId: "a1a00000-0000-0000-0000-000000000021",
        agent: { id: "a1a00000-0000-0000-0000-000000000021", name: "Jordan 'Hype' Williams", avatarUrl: null },
        turnNumber: 8, type: "Commentary", content: "Round 2 hype from Jordan.",
      }),
    ]);

    await page.route(`**/api/debates/${FAKE_DEBATE_ID}`, (route) =>
      route.fulfill({ json: debate })
    );

    await page.goto(`/debates/${FAKE_DEBATE_ID}`);
    await page.waitForTimeout(1000);

    // All 4 commentary entries should appear in the booth
    await expect(page.getByText("Round 1 analysis from Alex.")).toBeVisible();
    await expect(page.getByText("Round 1 hype from Jordan.")).toBeVisible();
    await expect(page.getByText("Round 2 analysis from Alex.")).toBeVisible();
    await expect(page.getByText("Round 2 hype from Jordan.")).toBeVisible();

    // Turns section should have 4 argument turns, no commentary
    const turnsSection = page.locator('section[aria-label="Debate turns"]');
    await expect(turnsSection.getByText("First argument.")).toBeVisible();
    await expect(turnsSection.getByText("Second counter.")).toBeVisible();
    await expect(turnsSection.getByText(/Round.*analysis/)).toHaveCount(0);
  });
});
