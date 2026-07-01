import { test, expect, type Page } from "@playwright/test";
import { seedAnonymousUser } from "./helpers";

// Regression coverage for the Shorts feed layout on small phones. The bug: every
// card used a fixed `pb-8 pt-20` wrapper with no safe-area insets and no room to
// scroll, so on shorter viewports (iPhone Pro vs the roomier Plus) the bottom
// react bar + CTA were pushed below the fold / under the iOS home indicator and
// became unreachable. This spec pins that down deterministically by mocking the
// three feed sources so content lengths are fixed, then asserting every card's
// primary CTA stays inside its own snap slide on a short viewport.

// iPhone Pro-class short viewport. We avoid devices[] because that pulls in WebKit
// (not installed here); an explicit small viewport reproduces the vertical squeeze.
test.use({
  viewport: { width: 390, height: 600 },
  hasTouch: true,
  isMobile: true,
});

// Long-but-realistic content — the lengths that actually crowd a short viewport.
const PROVISION = {
  id: "prov-1",
  slug: "zoning",
  title: "Statewide zoning reform to legalize missing-middle housing near transit corridors",
  neutralText:
    "Requires cities over 25,000 people to permit duplexes, triplexes, and fourplexes on any lot currently zoned exclusively for single-family homes, and to allow accessory dwelling units by right.",
  state: "WA",
  distance: 0,
  coveredBuckets: 3,
  totalBuckets: 8,
  deadline: "2026-08-15T00:00:00Z",
  gapWidth: 5,
  difficulty: "Moderate",
  governance: true,
  locality: "Washington",
};

const BUDGET_FACT = {
  id: "fact-1",
  factDate: "2026-06-01",
  category: "Defense",
  tensionLabel: "National security vs. the deficit",
  perspectiveA:
    "Defense spending sustains an industrial base spread across more than 300 congressional districts, funding shipyards, aircraft plants, and hundreds of thousands of skilled manufacturing jobs.",
  sourceA: "Congressional Budget Office",
  sourceUrlA: "https://cbo.gov",
  perspectiveB:
    "At roughly half of all discretionary spending, the defense budget crowds out investment in infrastructure, research, and education, and outpaces inflation.",
  sourceB: "Office of Management and Budget",
  sourceUrlB: "https://omb.gov",
  explanation:
    "Both statements are supported by the same appropriations data — the disagreement is about priorities, not facts.",
};

// One news briefing (has sourcePublisher) and one think-deeper briefing (has a
// question, no publisher) — buildFeed partitions them into the two card kinds.
const BRIEFINGS = {
  items: [
    {
      id: "news-1",
      slug: "news-1",
      headline:
        "Senate advances a sweeping bipartisan permitting overhaul after months of stalled negotiations",
      institution: "Senate",
      branch: "Legislative",
      status: "active",
      audienceLevel: "General",
      keyConcept: "Permitting",
      tags: [],
      summary30:
        "The compromise would compress federal environmental-review timelines for transmission lines and clean-energy projects to a hard two-year cap while preserving the public-comment periods community groups fought to keep.",
      createdAt: "2026-06-20T00:00:00Z",
      thinkDeeperQuestion: "",
      locality: null,
      sourcePublisher: "NPR",
    },
    {
      id: "think-1",
      slug: "think-1",
      headline:
        "A plain-language explainer on how federal permitting reform changes project timelines",
      institution: "Senate",
      branch: "Legislative",
      status: "active",
      audienceLevel: "General",
      keyConcept: "Tradeoffs",
      tags: [],
      summary30: "",
      createdAt: "2026-06-19T00:00:00Z",
      thinkDeeperQuestion:
        "If faster permits mean more energy on the grid sooner but less time for environmental review, where should the line between speed and scrutiny actually sit — and who should get to decide?",
      locality: null,
      sourcePublisher: null,
    },
  ],
  total: 2,
  page: 1,
  pageSize: 20,
};

// The feed calls the Civic API cross-origin (page :5175 → api :5050) with a custom
// X-User-Id header, so mocked responses need CORS headers and a preflight reply or
// the browser blocks them and the feed silently falls back to empty pools.
const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
  "Access-Control-Allow-Headers": "*",
};

function fulfillJson(page: Page, glob: string, json: unknown): Promise<void> {
  return page.route(glob, (route) =>
    route.request().method() === "OPTIONS"
      ? route.fulfill({ status: 204, headers: CORS })
      : route.fulfill({ json, headers: CORS }),
  );
}

// Mock the three feed endpoints so the four card kinds render deterministically,
// independent of whatever the backend happens to be seeded with. The globs are
// scoped to the API origin (:5050) on purpose — a loose "**/api/briefings*" would
// also swallow Vite's own /src/api/briefings.ts module request in dev and break
// the app bundle.
const API = "http://localhost:5050/api";
async function mockFeed(page: Page): Promise<void> {
  await fulfillJson(page, `${API}/coalition/provisions`, [PROVISION]);
  await fulfillJson(page, `${API}/budget-facts`, [BUDGET_FACT]);
  await fulfillJson(page, `${API}/briefings*`, BRIEFINGS);
}

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
  await mockFeed(page);
  await page.goto("/shorts");
  await expect(page.getByTestId("shorts-scroll")).toBeVisible();
});

test("shorts: every card's CTA stays inside its slide on a short viewport", async ({
  page,
}) => {
  // All four card kinds should be present given the mocked pools.
  const kinds = await page.evaluate(() =>
    Array.from(
      document.querySelectorAll<HTMLElement>('[data-testid^="short-"]'),
    )
      .map((el) => el.getAttribute("data-testid"))
      .filter((t): t is string => !!t && t.endsWith("-open"))
      .sort(),
  );
  expect(kinds).toEqual(
    [
      "short-budget-open",
      "short-coalition-open",
      "short-news-open",
      "short-thinkdeeper-open",
    ].sort(),
  );

  // For each card: snap its slide to the top, exhaust any in-card scroll (the
  // safety valve), then require the CTA to sit fully within the slide's box.
  const results = await page.evaluate(() => {
    const scroll = document.querySelector('[data-testid="shorts-scroll"]')!;
    const slides = Array.from(scroll.children).filter((s) =>
      s.querySelector('[data-testid^="short-"]'),
    ) as HTMLElement[];
    return slides.map((slide) => {
      const kind = slide
        .querySelector('[data-testid^="short-"]')!
        .getAttribute("data-testid");
      const cta = slide.querySelector<HTMLElement>('a[data-testid$="-open"]')!;
      slide.scrollIntoView({ block: "start" });
      // Exhaust any scrollable ancestor inside the slide (the shell safety valve).
      const shell = slide.firstElementChild as HTMLElement;
      shell.scrollTop = shell.scrollHeight;
      const sr = slide.getBoundingClientRect();
      const cr = cta.getBoundingClientRect();
      return {
        kind,
        withinSlide: cr.bottom <= sr.bottom + 1 && cr.top >= sr.top - 1,
        belowFoldPx: Math.round(cr.bottom - sr.bottom),
      };
    });
  });

  for (const r of results) {
    expect(
      r.withinSlide,
      `${r.kind} CTA is ${r.belowFoldPx}px below the slide fold`,
    ).toBe(true);
  }
});

test("shorts: cards and header reserve iOS safe-area insets", async ({
  page,
}) => {
  // Every card renders through the shared shell, and the shell folds the bottom
  // + top safe-area insets into its padding (root cause of the home-indicator
  // overlap). Checked via the inline style so it's deterministic even where the
  // env() insets resolve to 0 (headless Chromium).
  const shells = page.getByTestId("short-card-shell");
  await expect(shells.first()).toBeVisible();
  const styles = await shells.evaluateAll((els) =>
    els.map((e) => e.getAttribute("style") ?? ""),
  );
  expect(styles.length).toBeGreaterThanOrEqual(4);
  for (const style of styles) {
    expect(style).toContain("safe-area-inset-bottom");
    expect(style).toContain("safe-area-inset-top");
  }

  // The overlay header (close + title) must clear the notch / status bar.
  const headerStyle = await page
    .getByTestId("shorts-header")
    .evaluate((e) => e.getAttribute("style") ?? "");
  expect(headerStyle).toContain("safe-area-inset-top");
});
