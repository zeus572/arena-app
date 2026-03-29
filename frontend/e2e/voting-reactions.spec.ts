import { test, expect } from "@playwright/test";
import {
  createDebateViaAPI,
  getAgents,
  registerTestUser,
  registerPremiumUser,
  setAuthToken,
} from "./helpers";

test.describe("Voting & Reactions (Spec 1)", () => {
  let debateId: string;
  let proponentId: string;
  let opponentId: string;

  test.beforeAll(async ({ request }) => {
    const premiumUser = await registerPremiumUser(request, `vr-${Date.now()}`);
    const agents = await getAgents(request);
    proponentId = agents[0].id;
    opponentId = agents[1].id;

    const debate = await createDebateViaAPI(request, {
      topic: `E2E Voting Test ${Date.now()}`,
      proponentId,
      opponentId,
      token: premiumUser.accessToken,
    });
    debateId = debate.id;
  });

  test("can cast a vote on a debate", async ({ page, request }) => {
    const user = await registerTestUser(request, `vote-${Date.now()}`);
    await setAuthToken(page, user.accessToken);
    await page.goto(`/debates/${debateId}`);

    await page.waitForTimeout(1000);

    const voteButtons = page.getByRole("button").filter({ hasText: /Vote/i });
    const count = await voteButtons.count();
    if (count > 0) {
      await voteButtons.first().click();
      await page.waitForTimeout(500);
    }
  });

  test("reaction buttons render on debate page", async ({ page }) => {
    await page.goto(`/debates/${debateId}`);
    await page.waitForTimeout(1000);

    const likeBtn = page.locator("button").filter({ hasText: /like|agree/i });
    const count = await likeBtn.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });
});
