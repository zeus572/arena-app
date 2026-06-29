import { test, expect } from "@playwright/test";
import { API_BASE, seedAnonymousUser } from "./helpers";

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
});

test("magazine home shows the next national election countdown", async ({
  page,
  request,
}) => {
  const apiResp = await request.get(`${API_BASE}/elections/next?scope=national`);
  expect(apiResp.ok()).toBeTruthy();
  const expected = await apiResp.json();
  expect(expected.scope).toBe("National");

  await page.goto("/");

  // The feature rotator now opens on a random card, so select the countdown
  // explicitly before asserting its contents.
  await page.getByRole("button", { name: "Show countdown feature" }).click();

  const card = page.getByTestId("countdown-national");
  await expect(card).toBeVisible();

  await expect(page.getByTestId("countdown-national-name")).toHaveText(
    expected.name,
  );

  const days = await page.getByTestId("cd-days").innerText();
  expect(Number(days)).toBeGreaterThan(0);

  // Clock cells render with two-digit padding for hrs/min/sec
  await expect(page.getByTestId("cd-hours")).toHaveText(/^\d{2}$/);
  await expect(page.getByTestId("cd-minutes")).toHaveText(/^\d{2}$/);
  await expect(page.getByTestId("cd-seconds")).toHaveText(/^\d{2}$/);
});

test("countdown seconds tick down once per second", async ({ page }) => {
  await page.goto("/");

  // The rotator opens on a random card; select the countdown to read its clock.
  await page.getByRole("button", { name: "Show countdown feature" }).click();

  const seconds = page.getByTestId("cd-seconds");
  await expect(seconds).toBeVisible();

  const first = await seconds.innerText();
  // Wait long enough that even rolling-over from :00 → :59 still gives a different value.
  await page.waitForTimeout(1500);
  const second = await seconds.innerText();
  expect(second).not.toBe(first);
});
