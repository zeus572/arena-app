import { test, expect, type Page } from "@playwright/test";
import { API_BASE, acceptCookieConsent, seedAnonymousUser } from "./helpers";

// Regression coverage for the Bills page on phones. The bug: the six stage-filter
// segments are ~910px of nowrap uppercase text, and their container was `flex-none` —
// which sizes to content and refuses to shrink. Its own `overflow-x-auto` therefore
// never engaged, and instead of the segments swiping inside their box, the whole
// PAGE grew to 928px and scrolled sideways, dragging the header, the bill list and
// the bottom nav out of alignment with the viewport.
//
// The invariant these tests pin: the document never scrolls horizontally, and the
// filter row absorbs its own overflow.

test.use({
  viewport: { width: 390, height: 844 },
  hasTouch: true,
  isMobile: true,
});

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
  "Access-Control-Allow-Headers": "*",
};

// Long titles and sponsors on purpose — the page has to stay inside the viewport with
// realistic content, not just with short fixtures.
const BILLS = [
  {
    id: "bill-1",
    externalId: "hr-3935-118",
    title: "FAA Reauthorization Act of 2024",
    shortTitle: "FAA Reauthorization Act of 2024",
    identifier: "HR 3935 · 118th Congress",
    sponsor: "Rep. Samuel Bartholomew Graves Jr.",
    party: "R",
    status: "InCommittee",
    jurisdiction: "Federal",
    jurisdictionRegion: null,
    introducedDate: "2023-06-09T00:00:00Z",
    latestActionDate: "2024-05-16T00:00:00Z",
    teaser:
      "Reauthorizes the Federal Aviation Administration for five years, funding air-traffic " +
      "modernization and hiring more controllers…",
    axisCount: 3,
    axes: [
      { axisKey: "time-horizon", score: 0.62, confidence: 0.8 },
      { axisKey: "change-speed", score: 0.55, confidence: 0.7 },
      { axisKey: "risk", score: 0.31, confidence: 0.6 },
    ],
  },
  {
    id: "bill-2",
    externalId: "s-2043-118",
    title: "Right to Contraception Act",
    shortTitle: "Right to Contraception Act",
    identifier: "S 2043 · 118th Congress",
    sponsor: "Sen. Edward Markey",
    party: "D",
    status: "PassedOneChamber",
    jurisdiction: "Federal",
    jurisdictionRegion: null,
    introducedDate: "2023-06-20T00:00:00Z",
    latestActionDate: "2024-06-05T00:00:00Z",
    teaser: "Establishes a statutory right to obtain and provide contraception…",
    axisCount: 2,
    axes: [
      { axisKey: "authority", score: 0.89, confidence: 0.9 },
      { axisKey: "govt-role", score: 0.44, confidence: 0.7 },
    ],
  },
];

function fulfillJson(page: Page, glob: string, json: unknown): Promise<void> {
  return page.route(glob, (route) =>
    route.request().method() === "OPTIONS"
      ? route.fulfill({ status: 204, headers: CORS })
      : route.fulfill({ json, headers: CORS }),
  );
}

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
  await acceptCookieConsent(page);
  await fulfillJson(page, `${API_BASE}/bills`, BILLS);
  await page.goto("/bills");
  await expect(page.getByTestId("bills-stage-filter")).toBeVisible();
});

/**
 * Anything sticking out past the viewport that no ancestor clips or scrolls. An element
 * inside an `overflow-x-auto` box is fine — that's a swipeable row, not a broken layout.
 */
async function unclippedOverflow(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const de = document.documentElement;
    const out: string[] = [];
    document.querySelectorAll<HTMLElement>("*").forEach((el) => {
      const r = el.getBoundingClientRect();
      if (r.right <= de.clientWidth + 1) return;
      let a = el.parentElement;
      while (a) {
        const ox = getComputedStyle(a).overflowX;
        if (ox === "auto" || ox === "scroll" || ox === "hidden") return;
        a = a.parentElement;
      }
      out.push(`${el.tagName.toLowerCase()}.${el.className?.toString?.().slice(0, 60)}`);
    });
    return out;
  });
}

for (const view of ["front", "floor", "field"] as const) {
  test(`bills: the ${view} view fits a phone viewport`, async ({ page }) => {
    await page.goto(`/bills?view=${view}`);
    await expect(page.getByTestId("bills-stage-filter")).toBeVisible();

    const { client, scroll } = await page.evaluate(() => ({
      client: document.documentElement.clientWidth,
      scroll: document.documentElement.scrollWidth,
    }));

    expect(await unclippedOverflow(page)).toEqual([]);
    expect(scroll, `the page scrolls ${scroll - client}px sideways`).toBeLessThanOrEqual(client);
  });
}

test("bills: the stage filter absorbs its own overflow instead of widening the page", async ({
  page,
}) => {
  // The point isn't that the segments fit — they can't, and shouldn't have to. It's
  // that the row is bounded by the viewport and swipes internally.
  const box = await page.getByTestId("bills-stage-filter").evaluate((el) => ({
    width: el.getBoundingClientRect().width,
    content: el.scrollWidth,
    viewport: document.documentElement.clientWidth,
  }));

  expect(box.width).toBeLessThanOrEqual(box.viewport);
  expect(box.content, "the segments should still overflow their box").toBeGreaterThan(box.width);
});

test("bills: a later stage segment is reachable by swiping the filter row", async ({
  page,
}) => {
  // A bounded row that can't actually be scrolled would be worse than the original bug:
  // the filters past "Introduced" would be unreachable on a phone.
  const row = page.getByTestId("bills-stage-filter");
  await row.evaluate((el) => el.scrollTo({ left: el.scrollWidth }));

  const enacted = page.getByTestId("bills-stage-enacted");
  await expect(enacted).toBeInViewport();
  await enacted.click();

  await expect(enacted).toHaveAttribute("aria-pressed", "true");
});
