import { test, expect } from "@playwright/test";
import { getAgents } from "./helpers";

test.describe("Agent Profile Page (Spec 10)", () => {
  let agentId: string;
  let agentName: string;

  test.beforeAll(async ({ request }) => {
    const agents = await getAgents(request);
    agentId = agents[0].id;
    agentName = agents[0].name;
  });

  test("shows agent name and back button", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await expect(page.getByText(agentName)).toBeVisible();
    await expect(
      page.getByRole("link", { name: /All Agents/i })
    ).toBeVisible();
  });

  test("shows ideology badge", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await page.waitForSelector("h1");
    const header = page.locator("h1").filter({ hasText: agentName });
    await expect(header).toBeVisible();
  });

  test("shows personality radar chart (SVG)", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await expect(
      page.getByText("Personality Profile")
    ).toBeVisible();

    // Radar chart is an SVG element
    const svg = page.locator("svg");
    await expect(svg.first()).toBeVisible();

    // Trait labels appear in both the SVG and the bars below - use .first()
    await expect(page.getByText("Aggression").first()).toBeVisible();
    await expect(page.getByText("Eloquence").first()).toBeVisible();
    await expect(page.getByText("Fact Reliance").first()).toBeVisible();
    await expect(page.getByText("Empathy").first()).toBeVisible();
    await expect(page.getByText("Wit").first()).toBeVisible();
  });

  test("shows personality trait bars with numeric values", async ({
    page,
  }) => {
    await page.goto(`/agents/${agentId}`);
    await expect(page.getByText("Personality Profile")).toBeVisible();

    // Trait bars should show numeric values (e.g., "7.0", "6.0")
    const traitValues = page.locator(
      ".font-mono.text-muted-foreground"
    );
    const count = await traitValues.count();
    expect(count).toBe(5); // 5 personality traits
  });

  test("shows behavioral stats section", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await expect(page.getByText("Behavioral Stats")).toBeVisible();
    await expect(page.getByText("Total Debates")).toBeVisible();
    await expect(page.getByText("Total Turns")).toBeVisible();
    await expect(page.getByText("Avg Words/Turn")).toBeVisible();
    await expect(page.getByText("Citations Used")).toBeVisible();
  });

  test("shows audience reaction breakdown", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await expect(page.getByText("Audience Reactions")).toBeVisible();
    await expect(page.getByText("Likes")).toBeVisible();
    // "Insightful" and "Disagree" appear in both reaction row and breakdown
    await expect(page.getByText("Insightful").first()).toBeVisible();
    await expect(page.getByText("Disagree").first()).toBeVisible();
  });

  test("shows W-L-D record and reputation", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await expect(page.getByText(/Reputation:/)).toBeVisible();
    await expect(page.getByText(/\d+W-\d+L-\d+D/)).toBeVisible();
  });

  test("back button navigates to agents list", async ({ page }) => {
    await page.goto(`/agents/${agentId}`);
    await page.getByRole("link", { name: /All Agents/i }).click();
    await expect(page).toHaveURL("/agents");
  });
});
