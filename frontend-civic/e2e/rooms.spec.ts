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

/**
 * The surfaces added in F2, F4 and F5. Same rule as above: skip cleanly when the pilot is
 * not seeded, so the suite stays green on a clean database.
 */

async function findRoom(request: import("@playwright/test").APIRequestContext, kind: string) {
  const res = await request.get("http://localhost:5050/api/rooms");
  const rooms = (await res.json()) as Array<{ slug: string; kind: string }>;
  return rooms.find((r) => r.kind === kind);
}

test.describe("a story room", () => {
  test("renders the story page, not the theme page", async ({ page, request }) => {
    // The bug this guards: one URL serves two shapes, and a story rendered through the
    // theme component showed a status-sentence heading with no sentence and four empty
    // sections. Kind has to drive the choice of page.
    const story = await findRoom(request, "Story");
    test.skip(!story, "no published story room on this database");

    await page.goto(`/rooms/${story!.slug}`);

    await expect(page.getByTestId("story-room")).toBeVisible();
    await expect(page.getByTestId("room-detail")).toHaveCount(0);
    await expect(page.getByTestId("story-next-steps")).toBeVisible();
  });

  test("its facts carry evidence marks that link to the claim", async ({ page, request }) => {
    const story = await findRoom(request, "Story");
    test.skip(!story, "no published story room on this database");

    await page.goto(`/rooms/${story!.slug}`);
    await expect(page.getByTestId("story-room")).toBeVisible();

    const facts = page.getByTestId("story-fact");
    test.skip((await facts.count()) === 0, "story has no essential facts seeded");

    await facts.first().getByRole("link", { name: "Evidence" }).click();

    await expect(page.getByTestId("claim-detail")).toBeVisible();
    // Required on every claim, so it always renders.
    await expect(page.getByTestId("claim-what-would-settle-it")).toBeVisible();
  });
});

async function moneyItemCount(
  request: import("@playwright/test").APIRequestContext,
  slug: string,
) {
  const res = await request.get(`http://localhost:5050/api/rooms/${slug}/money`);
  const money = (await res.json()) as { items: unknown[] };
  return money.items.length;
}

test.describe("the money trail", () => {
  test("shows all five rungs including the empty ones", async ({ page, request }) => {
    // "Empty stages render as visible empty, never omitted." A requested figure and a spent
    // figure look identical without the blanks above them.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");

    // Decide whether to skip from the API, not from the DOM. The section fetches after
    // mount, so counting elements straight after goto() races the render and turns a real
    // regression into a silent skip.
    const items = await moneyItemCount(request, theme!.slug);
    test.skip(items === 0, "no money items seeded for this room");

    await page.goto(`/rooms/${theme!.slug}`);
    await expect(page.getByTestId("room-money")).toBeVisible();

    const firstItem = page.getByTestId("money-item").first();
    await expect(firstItem.getByTestId("money-ladder").locator("li")).toHaveCount(5);

    // Required field, rendered as a panel rather than hidden behind a tooltip.
    await expect(firstItem.getByTestId("money-does-not-mean")).toBeVisible();
  });

  test("never shows a total across the funding stages", async ({ page, request }) => {
    // The single most common error in budget coverage. The API refuses to compute it; the
    // page must not assemble one either.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");

    const items = await moneyItemCount(request, theme!.slug);
    test.skip(items === 0, "no money items seeded for this room");

    await page.goto(`/rooms/${theme!.slug}`);
    const money = page.getByTestId("room-money");
    await expect(money).toBeVisible();

    await expect(money).not.toContainText(/total across/i);
    await expect(money).toContainText(/nothing here is summed across stages/i);
  });
});

test.describe("the situation board", () => {
  test("is a different view of the same room, not just a wider one", async ({ page, request }) => {
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}?view=board`);

    await expect(page.getByTestId("room-board")).toBeVisible();
    // The reading view's article shell is gone; this is a destination, not a stylesheet.
    await expect(page.getByTestId("room-detail")).toHaveCount(0);
    await expect(page.getByTestId("room-claims")).toBeVisible();
  });

  test("the toggle returns to the reading view", async ({ page, request }) => {
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");

    await page.goto(`/rooms/${theme!.slug}?view=board`);
    await page.getByTestId("room-view-toggle").click();

    await expect(page.getByTestId("room-detail")).toBeVisible();
    await expect(page.getByTestId("room-board")).toHaveCount(0);
  });
});

test("the claims ledger puts the least settled claims first", async ({ page, request }) => {
  // Design 1n's deliberate sort: unsettled at the top "because that is where you are most
  // likely to be misled". The flattering order would be the other way round.
  const theme = await findRoom(request, "Theme");
  test.skip(!theme, "no published theme room on this database");

  const res = await request.get(
    `http://localhost:5050/api/rooms/${theme!.slug}/claims`,
  );
  const ledger = (await res.json()) as { unsettledCount: number; total: number };
  test.skip(ledger.total === 0, "no claims seeded for this room");
  test.skip(
    ledger.unsettledCount === 0 || ledger.unsettledCount === ledger.total,
    "ordering is only observable when the room holds both settled and unsettled claims",
  );

  await page.goto(`/rooms/${theme!.slug}`);
  const rows = page.getByTestId("ledger-claim");
  await expect(rows.first()).toBeVisible();

  const unsettled = ["Disputed", "PlausibleButUnresolved", "Unsupported", "Prediction"];
  await expect(rows.first()).toHaveAttribute(
    "data-status",
    new RegExp(`^(${unsettled.join("|")})$`),
  );
  await expect(rows.last()).not.toHaveAttribute(
    "data-status",
    new RegExp(`^(${unsettled.join("|")})$`),
  );
});

test("every control in a room is a 44px touch target at 390px", async ({ page, request }) => {
  // Designs 1aa / 1bb. Checked on the real page rather than by reading classnames, because
  // a utility class that does not survive the cascade reads fine in source and fails here.
  const theme = await findRoom(request, "Theme");
  test.skip(!theme, "no published theme room on this database");

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/rooms/${theme!.slug}`);
  await expect(page.getByTestId("room-detail")).toBeVisible();

  const controls = page.locator(
    '[data-testid="room-detail"] button:visible, [data-testid="room-detail"] select:visible',
  );
  const count = await controls.count();
  expect(count).toBeGreaterThan(0);

  for (let i = 0; i < count; i++) {
    const box = await controls.nth(i).boundingBox();
    if (!box) continue;
    expect(
      box.height,
      `control ${i} ("${(await controls.nth(i).innerText()).slice(0, 30)}") is ${box.height}px tall`,
    ).toBeGreaterThanOrEqual(44);
  }
});

test.describe("room interactions", () => {
  async function interactionCount(
    request: import("@playwright/test").APIRequestContext,
    slug: string,
  ) {
    const res = await request.get(
      `http://localhost:5050/api/rooms/${slug}/interactions`,
    );
    return ((await res.json()) as unknown[]).length;
  }

  test("a signed-out reader can play and gets the explanation", async ({ page, request }) => {
    // Playing needs no account. Civic gives the browser a pseudonymous id, so this answer
    // IS kept against that id — the "nothing was saved" path belongs to a client that sends
    // no id at all, and is covered separately below against the API.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");
    test.skip((await interactionCount(request, theme!.slug)) === 0, "no interactions seeded");

    await page.goto(`/rooms/${theme!.slug}`);
    const byk = page.locator('[data-testid="interaction"][data-kind="BeforeYouKnow"]');
    await expect(byk).toBeVisible();

    await byk.getByTestId("byk-option").first().click();
    await byk.getByTestId("byk-submit").click();

    const result = byk.getByTestId("interaction-result");
    await expect(result).toBeVisible();
    // Mandatory: an interaction that cannot explain itself is a publish blocker.
    await expect(result).not.toBeEmpty();
    // Mandatory whether the answer was right or wrong.
    await expect(result).toContainText(/Requested/i);
  });

  test("a client with no identity plays fully and stores nothing", async ({ request }) => {
    // CurrentUserService falls back to the literal "anonymous" only when there is no sub
    // claim AND no X-User-Id. Writing ledger rows for that id would pool every such visitor
    // into one XP bucket, so the play is scored and explained but not persisted.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");

    const res = await request.post(
      `http://localhost:5050/api/rooms/${theme!.slug}/interactions/appropriations-before-you-know/submit`,
      { data: { phase: "post", responseJson: JSON.stringify({ optionId: "none" }) } },
    );
    test.skip(res.status() === 404, "interaction not seeded on this database");

    const body = (await res.json()) as { persisted: boolean; explanation: string };
    expect(body.persisted).toBe(false);
    expect(body.explanation).not.toBe("");
  });

  test("the timeline builder is orderable without a mouse", async ({ page, request }) => {
    // Ordering is move-up / move-down, not drag. There is no second path to keep working,
    // which is the point — an accessibility fallback nobody exercises is one nobody notices
    // breaking.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");
    test.skip((await interactionCount(request, theme!.slug)) === 0, "no interactions seeded");

    await page.goto(`/rooms/${theme!.slug}`);
    const builder = page.locator('[data-testid="interaction"][data-kind="TimelineBuilder"]');
    await expect(builder).toBeVisible();

    const rows = builder.getByTestId("builder-event");
    const before = await rows.first().getAttribute("data-event-id");

    // Keyboard only: focus the second row's "move earlier" control and activate it.
    await rows.nth(1).getByTestId("builder-up").focus();
    await page.keyboard.press("Enter");

    await expect(rows.first()).not.toHaveAttribute("data-event-id", before!);

    await builder.getByTestId("builder-submit").click();
    await expect(builder.getByTestId("interaction-result")).toBeVisible();
  });

  test("an unscored interaction never marks an answer right or wrong", async ({
    page,
    request,
  }) => {
    // Vote Before Reading has no answer key and must never grow one — scoring an opinion
    // would be an ideological answer key.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");
    test.skip((await interactionCount(request, theme!.slug)) === 0, "no interactions seeded");

    await page.goto(`/rooms/${theme!.slug}`);
    const vote = page.locator('[data-testid="interaction"][data-kind="VoteBeforeReading"]');
    await expect(vote).toBeVisible();
    await expect(vote).toContainText(/no right answer/i);

    await vote.getByTestId("vote-option").first().click();
    await vote.getByTestId("vote-submit").click();

    const result = vote.getByTestId("interaction-result");
    await expect(result).toBeVisible();
    await expect(result).not.toContainText(/right|not quite|%/i);
  });

  test("the first vote is withheld until the second pass", async ({ page, request }) => {
    // Enforced server-side: the Pre response carries no answer at all, so there is nothing
    // in the client that could render it early even by mistake.
    const theme = await findRoom(request, "Theme");
    test.skip(!theme, "no published theme room on this database");

    const res = await request.post(
      `http://localhost:5050/api/rooms/${theme!.slug}/interactions/appropriations-vote-before-reading/submit`,
      { data: { phase: "pre", responseJson: JSON.stringify({ vote: "Yes" }) } },
    );
    test.skip(res.status() === 404, "vote interaction not seeded on this database");

    const body = await res.text();
    expect(body).not.toContain("Yes");
    expect(body).toContain("after you have read both sides");
  });
});
