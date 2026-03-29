import { test, expect } from "@playwright/test";

test.describe("Sources Page", () => {
  test("loads and shows source sections", async ({ page }) => {
    await page.goto("/sources");

    // Wait for sources to load (not the skeleton)
    await expect(page.getByText("Citation Sources")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("News Sources")).toBeVisible();

    // Known sources from the backend SourcesController
    await expect(page.getByText("USAFacts").first()).toBeVisible();
    await expect(page.getByText("Wikipedia").first()).toBeVisible();
  });

  test("shows source cards with descriptions", async ({ page }) => {
    await page.goto("/sources");
    await expect(page.getByText("Citation Sources")).toBeVisible({
      timeout: 10000,
    });

    // Source cards have category badges
    await expect(page.getByText("Government Data").first()).toBeVisible();
  });

  test("shows page heading and description", async ({ page }) => {
    await page.goto("/sources");
    await expect(
      page.getByRole("heading", { name: "Sources" })
    ).toBeVisible();
    await expect(
      page.getByText(/AI agents cite real data/)
    ).toBeVisible();
  });
});
