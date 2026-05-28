import type { Page } from "@playwright/test";

export const API_BASE = "http://localhost:5050/api";

/**
 * Set a fresh anonymous user id in localStorage before the app runs. The civic
 * backend's CurrentUserService falls back to the X-User-Id header, so this lets
 * each test act as an independent anonymous user.
 */
export async function seedAnonymousUser(page: Page): Promise<string> {
  const id = crypto.randomUUID();
  await page.addInitScript((userId) => {
    window.localStorage.setItem("civic-user-id", userId);
  }, id);
  return id;
}
