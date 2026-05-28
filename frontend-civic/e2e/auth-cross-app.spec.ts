import { test, expect, request as pwRequest } from "@playwright/test";

const ARENA_BASE = "http://localhost:5000";
const INVITE_CODE = "ARENA7X";

// The debate backend is the identity source; if it's not running, skip rather
// than fail the whole suite. Other civic E2E tests don't depend on it.
test.beforeAll(async () => {
  const api = await pwRequest.newContext();
  let alive = false;
  try {
    const resp = await api.get(`${ARENA_BASE}/health`, { timeout: 2000 });
    alive = resp.ok();
  } catch {
    alive = false;
  }
  await api.dispose();
  test.skip(
    !alive,
    `Debate backend not reachable at ${ARENA_BASE}/health. Start it with: dotnet run --project backend --urls http://localhost:5000`,
  );
});

test("magazine register flow signs the user in across both arenas", async ({
  page,
}) => {
  // Distinct email AND display name per run — DebateArena enforces a unique
  // Username derived from displayName, so reusing "Civic E2E" across runs fails
  // on the second pass.
  const uniq = crypto.randomUUID().slice(0, 8);
  const email = `civic-e2e-${uniq}@example.com`;
  const password = "civicE2E-PASS-1234";
  const displayName = `Civic E2E ${uniq}`;

  await page.goto("/");

  // Anonymous: header shows "Sign up" and "Sign in", home shows the public CTA.
  await expect(page.getByTestId("auth-strip-anon")).toBeVisible();
  await expect(page.getByTestId("signup-cta-register-link")).toBeVisible();

  // Click sign-up from the header.
  await page.getByTestId("auth-strip-signup-link").click();
  await expect(page).toHaveURL(/\/register/);

  await page.getByTestId("register-displayname").fill(displayName);
  await page.getByTestId("register-email").fill(email);
  await page.getByTestId("register-password").fill(password);
  await page.getByTestId("register-invite").fill(INVITE_CODE);

  await page.getByTestId("register-submit").click();

  // Lands back on the magazine home, now authed.
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("auth-strip-authed")).toBeVisible();
  await expect(page.getByTestId("auth-strip-email")).toHaveText(displayName);

  // The home CTA flips to the welcome-back state pointing at the civic profile.
  await expect(page.getByTestId("welcome-back-cta")).toBeVisible();
  await expect(page.getByTestId("signup-cta-debate-link")).toBeVisible();

  // The token persists across navigations.
  await page.goto("/profile");
  await expect(page.getByTestId("auth-strip-authed")).toBeVisible();

  // And logout returns us to anonymous.
  await page.getByTestId("auth-strip-logout").click();
  await expect(page.getByTestId("auth-strip-anon")).toBeVisible();
});

test("magazine login form rejects bad credentials with a visible error", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByTestId("login-email").fill("nobody@example.com");
  await page.getByTestId("login-password").fill("definitely-wrong");
  await page.getByTestId("login-submit").click();

  await expect(page.getByTestId("login-error")).toBeVisible();
  await expect(page.getByTestId("auth-strip-anon")).toBeVisible();
});
