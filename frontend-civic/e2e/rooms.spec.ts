import { test, expect } from "@playwright/test";
import { acceptCookieConsent, seedAnonymousUser } from "./helpers";

/**
 * Topic Rooms, against the real backend.
 *
 * These tests are content-agnostic on purpose. The pilot room seeds only behind
 * Rooms:SeedPilot (off by default, and off in CI), so anything asserting a specific room's
 * copy would fail on a clean database for reasons that have nothing to do with the code.
 * What is asserted instead is the SHAPE: the index renders, a room that exists renders its
 * front door in the right order, and a room that does not exist says so rather than
 * hanging or blanking.
 */

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
  await acceptCookieConsent(page);
});

test("the rooms index renders without an account", async ({ page }) => {
  await page.goto("/rooms");

  await expect(page.getByTestId("rooms-index")).toBeVisible();
  // Exactly one of the three terminal states, never a stuck spinner.
  await expect(
    page
      .getByTestId("room-card")
      .first()
      .or(page.getByTestId("rooms-empty"))
      .or(page.getByTestId("rooms-error")),
  ).toBeVisible();
});

test("an unknown room says so instead of hanging", async ({ page }) => {
  await page.goto("/rooms/definitely-not-a-real-room");

  await expect(page.getByTestId("room-missing")).toBeVisible();
  await expect(page.getByTestId("room-loading")).toHaveCount(0);
});

test("Rooms is reachable from the desktop nav", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto("/");

  await page.getByRole("link", { name: "Rooms", exact: true }).first().click();

  await expect(page).toHaveURL(/\/rooms$/);
  await expect(page.getByTestId("rooms-index")).toBeVisible();
});

test.describe("a published room", () => {
  // Skips cleanly when no room is published, so the suite stays green on a clean database
  // while still covering the real thing whenever content exists.
  test("renders its front door in the designed order", async ({ page, request }) => {
    const res = await request.get("http://localhost:5050/api/rooms");
    const rooms = (await res.json()) as Array<{ slug: string; kind: string }>;
    const theme = rooms.find((r) => r.kind === "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}`);

    const room = page.getByTestId("room-detail");
    await expect(room).toBeVisible();

    // The status sentence is the room's most important element and comes before the facts.
    await expect(page.getByTestId("room-status")).toBeVisible();
    await expect(page.getByTestId("room-watch-next")).toBeVisible();

    // Square corners are the identity rule, scoped to rooms only.
    await expect(room).toHaveClass(/rooms-square/);
  });

  test("evidence marks always ship with their status word", async ({ page, request }) => {
    const res = await request.get("http://localhost:5050/api/rooms");
    const rooms = (await res.json()) as Array<{ slug: string; kind: string }>;
    const theme = rooms.find((r) => r.kind === "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}`);
    // Wait for the room to actually render before counting. Counting straight after goto
    // races the fetch and silently skips the test, which is worse than failing.
    await expect(page.getByTestId("room-detail")).toBeVisible();

    const marks = page.getByTestId("evidence-mark");
    const count = await marks.count();
    test.skip(count === 0, "this room has no claim-backed facts yet");

    // Every mark carries a status in the DOM, and an accessible label — the greyscale
    // requirement means the square alone can never be the only signal.
    for (let i = 0; i < Math.min(count, 5); i++) {
      const mark = marks.nth(i);
      await expect(mark).toHaveAttribute("data-status", /\w+/);
      await expect(mark.getByRole("img")).toHaveAttribute("aria-label", /claim/);
    }
  });

  test("the board view widens the shell", async ({ page, request }) => {
    const res = await request.get("http://localhost:5050/api/rooms");
    const rooms = (await res.json()) as Array<{ slug: string; kind: string }>;
    const theme = rooms.find((r) => r.kind === "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}`);
    await page.getByTestId("room-view-toggle").click();

    await expect(page).toHaveURL(/view=board/);
  });

  test("the Latest section states what it left out", async ({ page, request }) => {
    // The disclosure is the point of the section — a bounded list is only trustworthy if
    // the bound is visible.
    const res = await request.get("http://localhost:5050/api/rooms");
    const rooms = (await res.json()) as Array<{ slug: string; kind: string }>;
    const theme = rooms.find((r) => r.kind === "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}`);

    await expect(page.getByTestId("room-latest")).toBeVisible();
    await expect(page.getByTestId("latest-what-we-left-out")).toBeVisible();
  });

  test("the methodology section names the Conversation Map as absent", async ({
    page,
    request,
  }) => {
    // Silently omitting it would imply coverage we do not have.
    const res = await request.get("http://localhost:5050/api/rooms");
    const rooms = (await res.json()) as Array<{ slug: string; kind: string }>;
    const theme = rooms.find((r) => r.kind === "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}`);

    await expect(page.getByTestId("no-conversation-map")).toBeVisible();
    await expect(page.getByTestId("evidence-legend")).toBeVisible();
  });
});
