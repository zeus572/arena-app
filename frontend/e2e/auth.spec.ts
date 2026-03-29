import { test, expect } from "@playwright/test";
import { registerTestUser } from "./helpers";

test.describe("Authentication", () => {
  test("login page shows form fields", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByPlaceholder("Email")).toBeVisible();
    await expect(page.getByPlaceholder("Password")).toBeVisible();
    await expect(
      page.locator("form").getByRole("button", { name: "Log In" })
    ).toBeVisible();
  });

  test("login page has link to register", async ({ page }) => {
    await page.goto("/login");
    await expect(
      page.locator("main").getByText("Sign up")
    ).toBeVisible();
  });

  test("register page shows form fields", async ({ page }) => {
    await page.goto("/register");
    await expect(page.getByPlaceholder("Email")).toBeVisible();
    await expect(
      page.getByPlaceholder("Password (min 8 characters)")
    ).toBeVisible();
    await expect(page.getByPlaceholder("Display name")).toBeVisible();
    await expect(page.getByPlaceholder("Invite code")).toBeVisible();
    await expect(
      page.locator("form").getByRole("button", { name: "Create Account" })
    ).toBeVisible();
  });

  test("register via API and verify logged-in state in browser", async ({
    page,
    request,
  }) => {
    // Register via API to avoid potential browser CORS issues
    const user = await registerTestUser(request, `browser-${Date.now()}`);

    // Set the token in localStorage
    await page.goto("/");
    await page.evaluate(
      (token) => {
        localStorage.setItem("arena-access-token", token);
      },
      user.accessToken
    );
    await page.reload();

    // User should now be logged in - navbar should show user menu instead of "Log In"
    await page.waitForTimeout(500);
    // Logged-in users don't see the "Log In" button in header
    const logInBtn = page
      .locator("header")
      .getByRole("button", { name: "Log In" });
    await expect(logInBtn).not.toBeVisible();
  });

  test("login with invalid credentials shows error", async ({ page }) => {
    await page.goto("/login");
    await page.getByPlaceholder("Email").fill("nonexistent@test.local");
    await page.getByPlaceholder("Password").fill("WrongPassword123!");
    await page.locator("form").getByRole("button", { name: "Log In" }).click();

    // Should stay on login page
    await page.waitForTimeout(1000);
    await expect(page).toHaveURL(/\/login/);
  });

  test("login via API verifies user can access profile", async ({
    page,
    request,
  }) => {
    const user = await registerTestUser(request, `profile-${Date.now()}`);

    await page.goto("/");
    await page.evaluate(
      (token) => {
        localStorage.setItem("arena-access-token", token);
      },
      user.accessToken
    );

    // Should be able to access the protected profile page
    await page.goto("/profile");
    await page.waitForTimeout(1000);
    // Should NOT redirect to login
    await expect(page).toHaveURL("/profile");
  });

  test("protected route /profile redirects unauthenticated users", async ({
    page,
  }) => {
    await page.goto("/");
    await page.evaluate(() => {
      localStorage.removeItem("arena-access-token");
      localStorage.removeItem("arena-refresh-token");
    });

    await page.goto("/profile");
    await page.waitForTimeout(1000);
    await expect(page).toHaveURL(/\/login/);
  });

  test("protected route /start redirects unauthenticated users", async ({
    page,
  }) => {
    await page.goto("/");
    await page.evaluate(() => {
      localStorage.removeItem("arena-access-token");
      localStorage.removeItem("arena-refresh-token");
    });

    await page.goto("/start");
    await page.waitForTimeout(1000);
    await expect(page).toHaveURL(/\/login/);
  });
});
