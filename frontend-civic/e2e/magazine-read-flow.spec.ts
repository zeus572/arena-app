import { test, expect } from "@playwright/test";
import { seedAnonymousUser } from "./helpers";

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
});

test("home renders a seeded cover story and explainer cards", async ({ page }) => {
  await page.goto("/");

  // Cover story is rendered as a link to a briefing detail page
  const cover = page.locator('a[href^="/briefings/"]').first();
  await expect(cover).toBeVisible();
  await expect(cover.getByText("Cover story", { exact: false })).toBeVisible();

  // At least three explainer cards in the grid
  const cards = page.locator('section ul li a[href^="/briefings/"]');
  await expect(cards.first()).toBeVisible();
  expect(await cards.count()).toBeGreaterThanOrEqual(3);
});

test("cover story navigates to a briefing detail with matching headline", async ({
  page,
}) => {
  await page.goto("/");

  const cover = page.locator('a[href^="/briefings/"]').first();
  const headlineOnHome = (await cover.locator("h2").innerText()).trim();
  await cover.click();

  await expect(page).toHaveURL(/\/briefings\/[^/]+$/);
  const heading = page.locator("article h1").first();
  await expect(heading).toBeVisible();
  await expect(heading).toHaveText(headlineOnHome);

  // Briefing detail surfaces values-in-conflict sidebar
  await expect(page.getByText("Values in conflict", { exact: false })).toBeVisible();
});

test("think-deeper page renders values and toggles a chip", async ({ page }) => {
  await page.goto("/think-deeper/student-data-privacy");

  const heading = page.locator("article h1").first();
  await expect(heading).toBeVisible();
  await expect(heading).toContainText("student data privacy", { ignoreCase: true });

  // Values chips render and the first one is interactive
  const chips = page.locator(
    'section:has-text("Which values matter to you here") button',
  );
  await expect(chips.first()).toBeVisible();

  const initialClass = (await chips.first().getAttribute("class")) ?? "";
  await chips.first().click();
  const afterClass = (await chips.first().getAttribute("class")) ?? "";
  expect(afterClass).not.toBe(initialClass);
});

test("unknown briefing slug shows graceful not-found state", async ({ page }) => {
  await page.goto("/briefings/this-briefing-does-not-exist");

  await expect(page.getByTestId("briefing-not-found")).toBeVisible();
  await expect(
    page.getByText("We don't have that briefing yet."),
  ).toBeVisible();
});
