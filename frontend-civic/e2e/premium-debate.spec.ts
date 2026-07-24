import { test, expect, request as pwRequest } from "@playwright/test";
import { execSync } from "node:child_process";
import { acceptCookieConsent } from "./helpers";

const ARENA_BASE = "http://localhost:5000";
const INVITE_CODE = "ARENA7X";
const SEEDED_BRIEFING_SLUG = "congress-student-data-privacy-bill";

// Promote the registered debate user to Premium by direct DB update. This
// matches how the rest of the dev environment grants premium today — there's
// no API endpoint yet. If docker / psql isn't available we skip the test.
function promoteToPremium(email: string): boolean {
  try {
    execSync(
      `docker exec arena-postgres psql -U postgres -d arena -c ` +
        `"UPDATE \\"Users\\" SET \\"Plan\\" = 1, \\"EmailVerified\\" = true WHERE \\"Email\\" = '${email}';"`,
      { stdio: "pipe" },
    );
    return true;
  } catch {
    return false;
  }
}

test.beforeAll(async () => {
  const api = await pwRequest.newContext();
  let alive = false;
  try {
    const resp = await api.get(`${ARENA_BASE}/health`, { timeout: 2000 });
    alive = resp.ok();
  } catch {
    /* not running */
  }
  await api.dispose();
  test.skip(
    !alive,
    `Debate backend not reachable at ${ARENA_BASE}/health. Start it with: dotnet run --project backend --urls http://localhost:5000`,
  );
});

test("free user does not see the 'Debate this' CTA on a briefing", async ({
  page,
}) => {
  await page.goto(`/briefings/${SEEDED_BRIEFING_SLUG}`);
  await expect(page.locator("article h1").first()).toBeVisible();
  await expect(page.getByTestId("debate-this-cta")).toHaveCount(0);
});

test("premium user sees and can click 'Debate this' to open a new debate", async ({
  page,
  context,
}) => {
  // Register a brand new user (anonymous → premium).
  const uniq = crypto.randomUUID().slice(0, 8);
  const email = `premium-civic-${uniq}@example.com`;
  const password = "premium-PASS-1234";
  const displayName = `Premium Civic ${uniq}`;

  await acceptCookieConsent(page);
  await page.goto("/register");
  await page.getByTestId("register-displayname").fill(displayName);
  await page.getByTestId("register-email").fill(email);
  await page.getByTestId("register-password").fill(password);
  await page.getByTestId("register-confirm-password").fill(password);
  await page.getByTestId("register-invite").fill(INVITE_CODE);
  await page.getByTestId("register-zip").fill("94105");
  await page.getByTestId("register-dob").fill("1990-01-01");
  await page.getByTestId("register-terms").check();
  await page.getByTestId("register-submit").click();
  await expect(page).toHaveURL(/\/$/);

  // Promote them to Premium directly in the debate DB.
  const promoted = promoteToPremium(email);
  test.skip(!promoted, "Could not reach docker / postgres to promote user to Premium.");

  // Force the AuthContext to pick up the new plan claim. Easiest path:
  // log out and log back in so the next access token carries plan=Premium.
  await page.getByTestId("auth-strip-logout").click();
  await expect(page.getByTestId("auth-strip-anon")).toBeVisible();

  await page.goto("/login");
  await page.getByTestId("login-email").fill(email);
  await page.getByTestId("login-password").fill(password);
  await page.getByTestId("login-submit").click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("auth-strip-authed")).toBeVisible();

  // Visit a seeded briefing — the CTA should now render.
  await page.goto(`/briefings/${SEEDED_BRIEFING_SLUG}`);
  const cta = page.getByTestId("debate-this-cta");
  await expect(cta).toBeVisible();
  const button = page.getByTestId("debate-this-button");
  await expect(button).toBeVisible();

  // Click and capture the new tab. window.open returns a popup page in PW.
  const popupPromise = context.waitForEvent("page");
  await button.click();
  const popup = await popupPromise;
  await popup.waitForLoadState("domcontentloaded").catch(() => {
    // Debate frontend may not be running — we only care about the URL pattern.
  });
  expect(popup.url()).toMatch(/\/debates\/[0-9a-f-]{36}/);
});
