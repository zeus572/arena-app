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

/**
 * Pre-accept the cookie/analytics consent so the first-visit CookieConsent
 * banner — fixed to the bottom of the viewport with a high z-index — never
 * renders. Otherwise it can overlay elements near the foot of a page (e.g. the
 * register form's submit button) and intercept clicks. Must run before the
 * first navigation. Keep the key in sync with lib/consent.ts.
 */
export async function acceptCookieConsent(page: Page): Promise<void> {
  await page.addInitScript(() => {
    window.localStorage.setItem("civersify-cookie-consent", "accepted");
  });
}
