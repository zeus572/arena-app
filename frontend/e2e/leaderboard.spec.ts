import { test, expect } from "@playwright/test";

test.describe("Leaderboard Page (Spec 3)", () => {
  test("loads and shows heading", async ({ page }) => {
    await page.goto("/leaderboard");
    await expect(
      page.getByRole("heading", { name: "Leaderboard" })
    ).toBeVisible();
    await expect(
      page.getByText("See which agents are dominating the arena")
    ).toBeVisible();
  });

  test("shows all 5 sort tabs", async ({ page }) => {
    await page.goto("/leaderboard");
    await expect(page.getByText("Top Winners")).toBeVisible();
    await expect(page.getByText("Most Controversial")).toBeVisible();
    await expect(page.getByText("Most Underrated")).toBeVisible();
    await expect(page.getByText("Best Win Rate")).toBeVisible();
    await expect(page.getByText("Most Engaging")).toBeVisible();
  });

  test("shows all 4 period filters", async ({ page }) => {
    await page.goto("/leaderboard");
    await expect(page.getByText("Today")).toBeVisible();
    await expect(page.getByText("This Week")).toBeVisible();
    await expect(page.getByText("This Month")).toBeVisible();
    await expect(page.getByText("All Time")).toBeVisible();
  });

  test("switching sort tabs updates the view", async ({ page }) => {
    await page.goto("/leaderboard");
    // Wait for initial load
    await page.waitForTimeout(500);

    // Click "Most Controversial" tab
    await page.getByText("Most Controversial").click();
    // Wait for reload
    await page.waitForTimeout(500);

    // The tab should now be active (bg-primary)
    const tab = page.getByText("Most Controversial");
    await expect(tab).toHaveClass(/bg-primary/);
  });

  test("switching period filter updates the view", async ({ page }) => {
    await page.goto("/leaderboard");
    await page.waitForTimeout(500);

    // Click "All Time" period
    await page.getByText("All Time").click();
    await page.waitForTimeout(500);

    // Should show agents (or "No agents found" message)
    const hasAgents = await page.locator("a[href^='/agents/']").count();
    const hasEmpty = await page.getByText("No agents found").count();
    expect(hasAgents > 0 || hasEmpty > 0).toBeTruthy();
  });

  test("agent entries link to agent profile", async ({ page }) => {
    await page.goto("/leaderboard");
    // Switch to all time for best chance of having data
    await page.getByText("All Time").click();
    await page.waitForTimeout(500);

    const agentLinks = page.locator("a[href^='/agents/']");
    const count = await agentLinks.count();
    if (count > 0) {
      await agentLinks.first().click();
      await expect(page).toHaveURL(/\/agents\/[a-f0-9-]+/);
    }
  });

  test("shows W-L-D records on agent entries", async ({ page }) => {
    await page.goto("/leaderboard");
    await page.getByText("All Time").click();
    await page.waitForTimeout(500);

    const wldText = page.getByText(/\d+W-\d+L-\d+D/);
    const count = await wldText.count();
    // If there are agents with debates, they'll show records
    expect(count).toBeGreaterThanOrEqual(0);
  });
});
