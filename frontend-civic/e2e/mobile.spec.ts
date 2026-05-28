import { test, expect } from "@playwright/test";
import { seedAnonymousUser } from "./helpers";

// Use Chromium with an iPhone-class viewport so the mobile-only affordances
// (bottom tab bar, horizontal carousel) render. We don't rely on devices[]
// because that pulls in WebKit, which isn't installed for this project.
test.use({
  viewport: { width: 390, height: 844 },
  hasTouch: true,
  isMobile: true,
});

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
});

test("mobile: bottom tab bar is visible and routes to quizzes + profile", async ({
  page,
}) => {
  await page.goto("/");

  const tabs = page.getByTestId("bottom-tabs");
  await expect(tabs).toBeVisible();

  // The compact masthead replaces the giant magazine-style nameplate on mobile.
  await expect(page.getByTestId("masthead-mobile")).toBeVisible();

  await page.getByTestId("tab-quizzes").click();
  await expect(page).toHaveURL(/\/quizzes$/);
  await expect(page.getByTestId("magazine-quiz")).toBeVisible();

  await page.getByTestId("tab-you").click();
  await expect(page).toHaveURL(/\/profile$/);
});

test("mobile: explainer briefings render as a horizontal carousel", async ({
  page,
}) => {
  await page.goto("/");

  const list = page.getByTestId("explainers-list");
  await expect(list).toBeVisible();

  // On mobile we apply overflow-x: auto on the <ul>, which gives a non-zero
  // scrollWidth that exceeds clientWidth when more than one card is present.
  const box = await list.evaluate((el) => ({
    overflow: getComputedStyle(el).overflowX,
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
  }));
  expect(box.overflow).toBe("auto");
  expect(box.scrollWidth).toBeGreaterThan(box.clientWidth);
});

test("mobile: tab Home returns to root from a sub-page", async ({ page }) => {
  await page.goto("/teachers");
  await expect(page.getByTestId("magazine-teachers")).toBeVisible();

  await page.getByTestId("tab-home").click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("explainers-section")).toBeVisible();
});
