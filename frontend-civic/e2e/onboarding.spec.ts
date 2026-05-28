import { test, expect, request } from "@playwright/test";
import { seedAnonymousUser, API_BASE } from "./helpers";

test("user can complete the onboarding flow and answers persist", async ({
  page,
}) => {
  const userId = await seedAnonymousUser(page);

  await page.goto("/");
  await page.getByTestId("onboarding-cta").getByRole("link").click();
  await expect(page).toHaveURL(/\/onboarding$/);

  // Walk all 10 questions. Alternate A/B for some variety.
  for (let i = 1; i <= 10; i++) {
    await expect(page.getByTestId("progress")).toContainText(
      `Question ${i} of 10`,
    );

    const choice = i % 2 === 0 ? "B" : "A";
    await page.getByTestId(`choice-${choice}`).click();
    await page.getByTestId("confidence-VerySure").click();
    await page.getByTestId("intensity-High").click();
    await page.getByTestId("next-button").click();
  }

  await expect(page.getByTestId("onboarding-done")).toBeVisible();
  await expect(page.getByText("Thank you.", { exact: false })).toBeVisible();

  // Verify persistence via the API using the same X-User-Id.
  const api = await request.newContext({
    extraHTTPHeaders: { "X-User-Id": userId },
  });
  const mine = await api.get(`${API_BASE}/answers/me`);
  expect(mine.ok()).toBeTruthy();
  const body = await mine.json();
  expect(body).toHaveLength(10);
  // Server stored the canonical PascalCase enum values
  for (const a of body) {
    expect(a.confidence).toBe("VerySure");
    expect(a.intensity).toBe("High");
  }
  await api.dispose();
});

test("re-answering the same question replaces the prior answer", async ({
  page,
}) => {
  const userId = await seedAnonymousUser(page);

  // First pass — answer every A with VerySure/High
  await page.goto("/onboarding");
  await expect(page.getByTestId("progress")).toBeVisible();
  for (let i = 0; i < 10; i++) {
    await page.getByTestId("choice-A").click();
    await page.getByTestId("confidence-VerySure").click();
    await page.getByTestId("intensity-High").click();
    await page.getByTestId("next-button").click();
  }
  await expect(page.getByTestId("onboarding-done")).toBeVisible();

  // Second pass — answer every B with NotSure/Low
  await page.goto("/onboarding");
  await expect(page.getByTestId("progress")).toBeVisible();
  for (let i = 0; i < 10; i++) {
    await page.getByTestId("choice-B").click();
    await page.getByTestId("confidence-NotSure").click();
    await page.getByTestId("intensity-Low").click();
    await page.getByTestId("next-button").click();
  }
  await expect(page.getByTestId("onboarding-done")).toBeVisible();

  // Only 10 answers should exist — all B/NotSure/Low — proving upsert
  const api = await request.newContext({
    extraHTTPHeaders: { "X-User-Id": userId },
  });
  const body = await (await api.get(`${API_BASE}/answers/me`)).json();
  expect(body).toHaveLength(10);
  for (const a of body) {
    expect(a.selectedChoiceKey).toBe("B");
    expect(a.confidence).toBe("NotSure");
    expect(a.intensity).toBe("Low");
  }
  await api.dispose();
});
