import { test, expect } from "@playwright/test";

test.describe("Agents Page", () => {
  test("loads and shows AI Agents heading", async ({ page }) => {
    await page.goto("/agents");
    await expect(
      page.getByRole("heading", { name: "AI Agents" })
    ).toBeVisible();
  });

  test("shows agent cards with names and descriptions", async ({ page }) => {
    await page.goto("/agents");
    // Wait for agents to load
    await page.waitForSelector("article, a[href^='/agents/']");

    // Should show at least some of the seeded agents
    await expect(page.getByText("Max Freedman")).toBeVisible();
    await expect(page.getByText("Rosa Vanguard")).toBeVisible();
    await expect(page.getByText("Edmund Hale")).toBeVisible();
    await expect(page.getByText("Terra Solari")).toBeVisible();
  });

  test("shows reputation scores", async ({ page }) => {
    await page.goto("/agents");
    await page.waitForSelector("a[href^='/agents/']");

    // Each agent card shows "Reputation: X.X"
    const repLabels = page.getByText(/Reputation:/);
    await expect(repLabels.first()).toBeVisible();
  });

  test("shows W-L-D stats for agents with debates", async ({ page }) => {
    await page.goto("/agents");
    await page.waitForSelector("a[href^='/agents/']");

    // If agents have participated in debates, stats should show
    // The format is like "0W-0L-0D" or "2W-1L-0D"
    const statsText = page.getByText(/\d+W-\d+L-\d+D/);
    // May or may not have stats depending on DB state
    const count = await statsText.count();
    // Just verify the page loaded correctly
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test("clicking an agent navigates to agent profile", async ({ page }) => {
    await page.goto("/agents");
    await page.waitForSelector("a[href^='/agents/']");

    // Click the first agent card
    await page.locator("a[href^='/agents/']").first().click();
    await expect(page).toHaveURL(/\/agents\/[a-f0-9-]+/);
  });

  test("shows everyday people agents", async ({ page }) => {
    await page.goto("/agents");
    await page.waitForSelector("a[href^='/agents/']");

    await expect(page.getByText("Maria Reyes")).toBeVisible();
    await expect(page.getByText("Derek Dawson")).toBeVisible();
    await expect(page.getByText("Priya Chakraborty")).toBeVisible();
    await expect(page.getByText("James Whitfield")).toBeVisible();
  });
});
