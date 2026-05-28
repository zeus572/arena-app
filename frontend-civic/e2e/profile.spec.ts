import { test, expect, request } from "@playwright/test";
import { seedAnonymousUser, API_BASE } from "./helpers";

test("empty profile shows axis chrome and a start-onboarding CTA", async ({
  page,
}) => {
  await seedAnonymousUser(page);
  await page.goto("/profile");

  await expect(page.getByTestId("profile")).toBeVisible();
  await expect(page.getByTestId("start-onboarding-link")).toBeVisible();

  // All 10 axes render even with no data
  await expect(page.getByTestId("axis-govt-role")).toBeVisible();
  await expect(page.getByTestId("axis-time-horizon")).toBeVisible();

  // No axis-marker dots when there's no data
  await expect(page.getByTestId("axis-marker-govt-role")).toHaveCount(0);

  // No top archetype card yet
  await expect(page.getByTestId("top-archetype")).toHaveCount(0);
});

test("after onboarding, profile shows axis markers and a top archetype", async ({
  page,
}) => {
  const userId = await seedAnonymousUser(page);

  // Complete onboarding answering B + VerySure + High — should push toward public-builder
  await page.goto("/onboarding");
  for (let i = 0; i < 10; i++) {
    await page.getByTestId("choice-B").click();
    await page.getByTestId("confidence-VerySure").click();
    await page.getByTestId("intensity-High").click();
    await page.getByTestId("next-button").click();
  }
  await expect(page.getByTestId("onboarding-done")).toBeVisible();

  // Navigate to profile via the "See your profile" button
  await page.getByTestId("see-profile").click();
  await expect(page).toHaveURL(/\/profile$/);

  await expect(page.getByTestId("top-archetype")).toBeVisible();
  await expect(page.getByTestId("top-archetype-name")).not.toBeEmpty();
  await expect(page.getByTestId("top-archetype-percent")).toContainText("%");

  // At least one axis has a marker rendered
  const markers = page.locator('[data-testid^="axis-marker-"]');
  expect(await markers.count()).toBeGreaterThan(0);

  // Other tendencies list renders
  await expect(page.getByTestId("archetype-blend-list")).toBeVisible();

  // Confirm scoring details via the API. Answering B on every question pushes
  // govt-role, economic-fairness, and community positive — so a public-systems
  // / fairness-flavored archetype should lead. Accept either of the two
  // archetypes whose vectors match that signal.
  const api = await request.newContext({
    extraHTTPHeaders: { "X-User-Id": userId },
  });
  const profile = await (await api.get(`${API_BASE}/profile/me`)).json();
  expect(profile.archetypeBlend[0].archetypeKey).toMatch(
    /^(public-builder|fairness-advocate)$/,
  );
  const govtRoleAxis = profile.axes.find(
    (a: { axisKey: string }) => a.axisKey === "govt-role",
  );
  expect(govtRoleAxis.score).toBeGreaterThan(0);
  const economicAxis = profile.axes.find(
    (a: { axisKey: string }) => a.axisKey === "economic-fairness",
  );
  expect(economicAxis.score).toBeGreaterThan(0);
  await api.dispose();
});
