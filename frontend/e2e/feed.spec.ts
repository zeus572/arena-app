import { test, expect } from "@playwright/test";
import { createDebateViaAPI, getAgents, registerPremiumUser } from "./helpers";

test.describe("Feed Page", () => {
  let premiumToken: string;

  test.beforeAll(async ({ request }) => {
    const user = await registerPremiumUser(request, `feed-${Date.now()}`);
    premiumToken = user.accessToken;
  });

  test("loads and shows the feed heading", async ({ page }) => {
    await page.goto("/");
    await expect(
      page.getByText("Latest from the Arena")
    ).toBeVisible();
  });

  test("shows search bar and sort buttons", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByPlaceholder("Search debates...")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Hot", exact: true })
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "New", exact: true })
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Top", exact: true })
    ).toBeVisible();
  });

  test("sidebar shows Start a Debate and Shape the Debate cards", async ({
    page,
  }) => {
    await page.goto("/");
    await expect(page.getByText("Start a Debate")).toBeVisible();
    await expect(page.getByText("Shape the Debate")).toBeVisible();
  });

  test("debate card shows proponent vs opponent", async ({
    page,
    request,
  }) => {
    const agents = await getAgents(request);

    await createDebateViaAPI(request, {
      topic: `E2E Feed Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    await page.goto("/");
    await page.waitForSelector("article");

    const card = page.locator("article").first();
    await expect(card).toBeVisible();
    await expect(card.getByText(/vs/)).toBeVisible();
  });

  test("search filters debates", async ({ page, request }) => {
    const agents = await getAgents(request);
    const uniqueTopic = `UniqueSearchTerm_${Date.now()}`;
    await createDebateViaAPI(request, {
      topic: uniqueTopic,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    await page.goto("/");
    await page.waitForSelector("article");

    await page.getByPlaceholder("Search debates...").fill(uniqueTopic);
    await page.waitForTimeout(500);

    await expect(page.getByText(/result/)).toBeVisible();
    await expect(page.getByText("Clear filters")).toBeVisible();
  });

  test("sort buttons change active sort", async ({ page }) => {
    await page.goto("/");

    const newBtn = page.getByRole("button", { name: "New", exact: true });
    await newBtn.click();
    await expect(newBtn).toHaveClass(/text-primary/);

    const topBtn = page.getByRole("button", { name: "Top", exact: true });
    await topBtn.click();
    await expect(topBtn).toHaveClass(/text-primary/);
  });

  test("clicking a debate card navigates to debate view", async ({
    page,
    request,
  }) => {
    const agents = await getAgents(request);
    await createDebateViaAPI(request, {
      topic: `E2E Navigate Test ${Date.now()}`,
      proponentId: agents[0].id,
      opponentId: agents[1].id,
      token: premiumToken,
    });

    await page.goto("/");
    await page.waitForSelector("article");

    await page.locator("article").first().click();
    await expect(page).toHaveURL(/\/debates\//);
  });
});
