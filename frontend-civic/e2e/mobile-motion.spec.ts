import { test, expect } from "@playwright/test";
import { seedAnonymousUser } from "./helpers";

// Mobile motion embellishments. iPhone-class viewport so the bottom tab bar and
// other mobile-only affordances render (matches mobile.spec.ts).
test.use({
  viewport: { width: 390, height: 844 },
  hasTouch: true,
  isMobile: true,
});

test("boot splash paints on load then is removed once the app hydrates", async ({
  page,
}) => {
  // Commit-only wait so we can observe the splash before the app fully settles.
  await page.goto("/", { waitUntil: "commit" });

  // The branded loading screen is baked into index.html, so it's present from
  // the first frame — no bundle round-trip needed.
  const splash = page.locator("#boot-splash");
  await expect(splash).toBeAttached();
  await expect(splash.locator(".boot-splash__word")).toHaveText("Civersify");

  // BootSplash fades + removes it once auth resolves (a fast localStorage read
  // on web), so it must detach on its own.
  await expect(splash).toBeHidden();
  await expect(splash).toHaveCount(0, { timeout: 6000 });
});

test("bottom tabs carry the tactile press-feedback class", async ({ page }) => {
  await seedAnonymousUser(page);
  await page.goto("/");

  // motion-press gives the touch scale-down; it's gated to reduced-motion in CSS.
  await expect(page.getByTestId("tab-home")).toHaveClass(/motion-press/);
  await expect(page.getByTestId("tab-shorts")).toHaveClass(/motion-press/);
});
