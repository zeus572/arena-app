import { test, expect } from "@playwright/test";

test.describe("Navigation", () => {
  test("navbar shows all main links", async ({ page }) => {
    await page.goto("/");

    // Check main nav links from NAV_LINKS in navbar.tsx
    // Use CSS selectors for nav links to avoid ambiguity with page content
    const nav = page.locator("header");
    await expect(nav).toBeVisible();

    // These are the nav link labels
    await expect(nav.getByRole("link", { name: "Feed" })).toBeVisible();
    await expect(nav.getByRole("link", { name: "Topics" })).toBeVisible();
    await expect(
      nav.getByRole("link", { name: "Leaderboard" })
    ).toBeVisible();
    await expect(nav.getByRole("link", { name: "Agents" })).toBeVisible();
    await expect(nav.getByRole("link", { name: "Sources" })).toBeVisible();
  });

  test("navigating to /agents loads agents page", async ({ page }) => {
    await page.goto("/");
    await page
      .locator("header")
      .getByRole("link", { name: "Agents" })
      .click();
    await expect(page).toHaveURL("/agents");
    await expect(
      page.getByRole("heading", { name: "AI Agents" })
    ).toBeVisible();
  });

  test("navigating to /leaderboard loads leaderboard", async ({ page }) => {
    await page.goto("/");
    await page
      .locator("header")
      .getByRole("link", { name: "Leaderboard" })
      .click();
    await expect(page).toHaveURL("/leaderboard");
    await expect(
      page.getByRole("heading", { name: "Leaderboard" })
    ).toBeVisible();
  });

  test("navigating to /topics loads topics page", async ({ page }) => {
    await page.goto("/");
    await page
      .locator("header")
      .getByRole("link", { name: "Topics" })
      .click();
    await expect(page).toHaveURL("/topics");
  });

  test("navigating to /sources loads sources page", async ({ page }) => {
    await page.goto("/");
    await page
      .locator("header")
      .getByRole("link", { name: "Sources" })
      .click();
    await expect(page).toHaveURL("/sources");
  });

  test("logo links to feed", async ({ page }) => {
    await page.goto("/agents");
    await page
      .locator("header")
      .getByRole("link", { name: /Debate Arena/i })
      .click();
    await expect(page).toHaveURL("/");
  });

  test("theme toggle exists in header", async ({ page }) => {
    await page.goto("/");
    const header = page.locator("header");
    // Theme toggle has sun/moon icon
    const buttons = header.locator("button");
    const count = await buttons.count();
    expect(count).toBeGreaterThan(0);
  });
});
