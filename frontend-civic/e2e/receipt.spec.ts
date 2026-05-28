import { test, expect, request } from "@playwright/test";
import { seedAnonymousUser, API_BASE } from "./helpers";

async function answerQuestion(
  api: import("@playwright/test").APIRequestContext,
  externalId: string,
  choiceKey: string,
) {
  const all = await (await api.get(`${API_BASE}/questions?take=50`)).json();
  const q = all.find((x: { externalId: string }) => x.externalId === externalId);
  if (!q) throw new Error(`question ${externalId} not seeded`);
  const resp = await api.post(`${API_BASE}/answers`, {
    data: {
      questionId: q.id,
      selectedChoiceKey: choiceKey,
      confidence: "VerySure",
      intensity: "High",
    },
  });
  if (!resp.ok()) throw new Error(`POST /answers failed: ${resp.status()}`);
}

test("generate receipt produces a values receipt with insights", async ({
  page,
}) => {
  const userId = await seedAnonymousUser(page);

  // Seed answers via the API so we don't need to walk onboarding
  const api = await request.newContext({
    extraHTTPHeaders: { "X-User-Id": userId },
  });
  const questions = await (await api.get(`${API_BASE}/questions?take=20`)).json();
  for (const q of questions.slice(0, 10)) {
    await api.post(`${API_BASE}/answers`, {
      data: {
        questionId: q.id,
        selectedChoiceKey: "B",
        confidence: "VerySure",
        intensity: "High",
      },
    });
  }
  await api.dispose();

  await page.goto("/profile");
  await expect(page.getByTestId("profile")).toBeVisible();
  await expect(page.getByTestId("top-archetype")).toBeVisible();

  await page.getByTestId("generate-receipt").click();
  await page.waitForURL(/\/receipt\/[0-9a-f-]+$/);

  await expect(page.getByTestId("receipt")).toBeVisible();
  await expect(page.getByTestId("learned-list")).toBeVisible();
  const insightCount = await page
    .locator('[data-testid^="insight-"]')
    .count();
  expect(insightCount).toBeGreaterThan(0);
});

test("opposing-direction answers surface a tension on the receipt", async ({
  page,
}) => {
  const userId = await seedAnonymousUser(page);
  const api = await request.newContext({
    extraHTTPHeaders: { "X-User-Id": userId },
  });

  // Pick two questions that both touch the "speech" axis but in opposite ways.
  // q-pairing-08-speech-harm: A pushes speech negative, B pushes positive
  // q-pressure-01-campus-speaker: A pushes speech negative, B pushes positive
  await answerQuestion(api, "q-pairing-08-speech-harm", "A");
  await answerQuestion(api, "q-pressure-01-campus-speaker", "B");
  await api.dispose();

  await page.goto("/profile");
  await page.getByTestId("generate-receipt").click();
  await page.waitForURL(/\/receipt\/[0-9a-f-]+$/);

  await expect(page.getByTestId("tensions-section")).toBeVisible();
  await expect(page.getByTestId("tension-speech")).toBeVisible();
  await expect(page.getByTestId("tension-speech")).toContainText("Speech");
});

test("unknown receipt id shows not-found state", async ({ page }) => {
  await seedAnonymousUser(page);
  await page.goto("/receipt/00000000-0000-0000-0000-000000000000");
  await expect(page.getByTestId("receipt-not-found")).toBeVisible();
});
