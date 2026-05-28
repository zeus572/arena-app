import { test, expect, request } from "@playwright/test";
import { seedAnonymousUser, API_BASE } from "./helpers";

test("budget page shows 10 categories and submit disabled until 100", async ({
  page,
}) => {
  await seedAnonymousUser(page);
  await page.goto("/budget");

  await expect(page.getByTestId("budget")).toBeVisible();
  const categoryRows = page.locator('[data-testid^="category-"]');
  expect(await categoryRows.count()).toBe(10);

  await expect(page.getByTestId("submit-button")).toBeDisabled();
  await expect(page.getByTestId("budget-status")).toContainText("Short by 100");
});

test("allocating exactly 100 across categories enables submit and recomputes profile", async ({
  page,
}) => {
  const userId = await seedAnonymousUser(page);
  await page.goto("/budget");
  await expect(page.getByTestId("budget")).toBeVisible();

  // Allocate 30 healthcare + 25 education + 25 infrastructure + 20 climate = 100
  await page.getByTestId("points-healthcare").fill("30");
  await page.getByTestId("points-education").fill("25");
  await page.getByTestId("points-infrastructure").fill("25");
  await page.getByTestId("points-climate").fill("20");

  await expect(page.getByTestId("total-points")).toHaveText("100");
  await expect(page.getByTestId("budget-status")).toContainText("Exactly 100");
  await expect(page.getByTestId("submit-button")).toBeEnabled();

  await page.getByTestId("submit-button").click();
  await expect(page.getByTestId("budget-done")).toBeVisible();

  // Profile should now reflect the allocation
  const api = await request.newContext({
    extraHTTPHeaders: { "X-User-Id": userId },
  });
  const profile = await (await api.get(`${API_BASE}/profile/me`)).json();
  expect(profile.profileVersion).toBeGreaterThan(0);
  // Allocating heavily to healthcare/education/infrastructure/climate should push
  // govt-role positive and time-horizon positive
  const govtRole = profile.axes.find(
    (a: { axisKey: string }) => a.axisKey === "govt-role",
  );
  expect(govtRole.score).toBeGreaterThan(0);
  const timeHorizon = profile.axes.find(
    (a: { axisKey: string }) => a.axisKey === "time-horizon",
  );
  expect(timeHorizon.score).toBeGreaterThan(0);
  await api.dispose();
});

test("budget over 100 surfaces an over-budget message", async ({ page }) => {
  await seedAnonymousUser(page);
  await page.goto("/budget");

  await page.getByTestId("points-healthcare").fill("50");
  await page.getByTestId("points-defense").fill("70");

  await expect(page.getByTestId("total-points")).toHaveText("120");
  await expect(page.getByTestId("budget-status")).toContainText("Over by 20");
  await expect(page.getByTestId("submit-button")).toBeDisabled();
});
