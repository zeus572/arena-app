import { test, expect } from "@playwright/test";
import { seedAnonymousUser } from "./helpers";

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
});

test("home surfaces the four learn-more entry points", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByTestId("concept-of-the-day")).toBeVisible();
  await expect(page.getByTestId("cta-quiz")).toBeVisible();
  await expect(page.getByTestId("cta-timeline")).toBeVisible();
  await expect(page.getByTestId("cta-teachers")).toBeVisible();
});

test("quiz: grades a correct answer and finishes with results", async ({
  page,
}) => {
  await page.goto("/quizzes");

  await expect(page.getByTestId("magazine-quiz")).toBeVisible();
  // The seeded quiz has 4 questions; the correct indices match the seed
  // ordering: rulemaking→Executive (1), committee→No (1), prediction (2),
  // focus/independence (0).
  await expect(page.getByTestId("quiz-progress")).toHaveText("Question 1 of 4");

  const correctIndices = [1, 1, 2, 0];
  for (let i = 0; i < correctIndices.length; i++) {
    await page.getByTestId(`quiz-option-${correctIndices[i]}`).click();
    await expect(page.getByTestId("quiz-explanation")).toBeVisible();
    await page.getByTestId("quiz-next").click();
  }

  await expect(page.getByTestId("quiz-results")).toBeVisible();
  await expect(page.getByTestId("quiz-results-score")).toContainText(
    "4 of 4 right",
  );
});

test("quiz: a wrong answer surfaces the explanation", async ({ page }) => {
  await page.goto("/quizzes");

  // q-001 correct is index 1; pick index 0 (Legislative) to be wrong.
  await page.getByTestId("quiz-option-0").click();
  await expect(page.getByTestId("quiz-explanation")).toBeVisible();
  await expect(page.getByTestId("quiz-explanation")).toContainText(
    /Not quite/i,
  );
});

test("concept of the day routes to the concept detail page", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByTestId("concept-of-the-day-link").click();

  await expect(page).toHaveURL(/\/concepts\//);
  await expect(page.getByTestId("magazine-concept")).toBeVisible();
  await expect(page.getByTestId("concept-title")).toBeVisible();
  await expect(page.getByTestId("concept-try-it")).toBeVisible();
});

test("unknown concept slug shows graceful not-found state", async ({
  page,
}) => {
  await page.goto("/concepts/no-such-concept");
  await expect(page.getByTestId("concept-not-found")).toBeVisible();
});

test("bill timeline shows steps with a single current step + what-happens-next", async ({
  page,
}) => {
  await page.goto("/timelines/bill");

  await expect(page.getByTestId("magazine-bill-timeline")).toBeVisible();

  const stepLocators = page.locator('[data-testid^="timeline-step-"]');
  expect(await stepLocators.count()).toBeGreaterThanOrEqual(5);

  const currentSteps = page.locator('[data-status="Current"]');
  await expect(currentSteps).toHaveCount(1);

  await expect(page.getByTestId("timeline-whats-next")).toBeVisible();
});

test("teachers page renders prompts and trust signals", async ({ page }) => {
  await page.goto("/teachers");

  await expect(page.getByTestId("magazine-teachers")).toBeVisible();
  await expect(page.getByTestId("teachers-trust-signals")).toBeVisible();
  await expect(page.getByTestId("teachers-classroom-prompts")).toBeVisible();
  await expect(page.getByTestId("teachers-parent-starters")).toBeVisible();
});
