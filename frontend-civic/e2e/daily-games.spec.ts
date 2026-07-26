import { test, expect, type Page } from "@playwright/test";
import { acceptCookieConsent, seedAnonymousUser } from "./helpers";

// End-to-end coverage of the daily games (docs/civic_daily_games): the hub, a full
// play-through of each interaction shape (single-tap, multi-round slider, guess ladder,
// round ladder), the share grid, and the degraded states.
//
// The API is mocked so content is fixed — otherwise every assertion would depend on
// whatever the generator happened to produce today. Globs are scoped to the API origin
// (:5050) on purpose: a loose "**/api/daily*" would also swallow Vite's own
// /src/api/daily.ts module request in dev and break the bundle.

const API = "http://localhost:5050/api";

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

// ------------------------------------------------------------------ fixtures

const FORK_PUZZLE = {
  id: "fork-1",
  kind: "Fork",
  puzzleDate: "2026-07-25",
  edition: 12,
  payloadVersion: 1,
  locality: null,
  play: null,
  payload: {
    question: "Who should pay for the grid upgrades a new data center needs?",
    tradeoff: "Charging the facility slows buildout; spreading it raises everyone's bill.",
    optionA: { label: "The facility pays", cost: "Fewer data centers get built here." },
    optionB: { label: "All ratepayers share", cost: "Your utility bill goes up." },
    axisKey: "economic-fairness",
    subQuestionKey: "cost-allocation",
    provisionSlug: "data-center-grid-fee",
  },
};

const CROWDCALL_PUZZLE = {
  id: "cc-1",
  kind: "CrowdCall",
  puzzleDate: "2026-07-25",
  edition: 88,
  payloadVersion: 1,
  locality: null,
  play: null,
  payload: {
    rounds: [
      {
        prompt: "Which branch can declare a law unconstitutional?",
        answer: "The judicial branch",
        explanation: "Judicial review was established in Marbury v. Madison (1803).",
        crowdSource: "civic-users",
        attribution: "Civersify players, last 60 days",
        sourceUrl: null,
        fieldedOn: null,
      },
      {
        prompt: "How many senators does each state send?",
        answer: "Two, regardless of population",
        explanation: "The Connecticut Compromise.",
        crowdSource: "national-poll",
        attribution: "Example National Poll",
        sourceUrl: "https://example.org/poll",
        fieldedOn: "2026-01-15",
      },
    ],
  },
};

const PLACEIT_PUZZLE = {
  id: "pi-1",
  kind: "PlaceIt",
  puzzleDate: "2026-07-25",
  edition: 31,
  payloadVersion: 1,
  locality: null,
  play: null,
  payload: {
    billId: "bill-1",
    billTitle: "Federal Permitting Modernization Act",
    billSummary: "Moves permitting authority to a federal office with a two-year cap.",
    billStatus: "InCommittee",
    axes: [
      { axisKey: "authority", name: "Authority", lowLabel: "Decentralized", highLabel: "Centralized" },
      { axisKey: "risk", name: "Risk", lowLabel: "Precautionary", highLabel: "Innovation-tolerant" },
      { axisKey: "change-speed", name: "Change speed", lowLabel: "Gradualist", highLabel: "Transformational" },
    ],
    maxRounds: 3,
  },
};

const PRICEDIN_PUZZLE = {
  id: "pr-1",
  kind: "PricedIn",
  puzzleDate: "2026-07-25",
  edition: 5,
  payloadVersion: 1,
  locality: null,
  play: null,
  payload: {
    prompt: "What was the federal standard deduction for a single filer in 2025?",
    unit: "usd",
    minBound: 1000,
    maxBound: 200000,
    maxGuesses: 3,
    source: "IRS Rev. Proc. 2024-40",
    sourceUrl: "https://example.org/irs",
    asOf: "2025-01-01",
  },
};

const SLATE = {
  date: "2026-07-25",
  anonymous: true,
  cadence: { last7Days: [false, true, false, true, false, false, true], activeDays: 3 },
  puzzles: [FORK_PUZZLE, CROWDCALL_PUZZLE, PLACEIT_PUZZLE, PRICEDIN_PUZZLE],
};

test.beforeEach(async ({ page }) => {
  await seedAnonymousUser(page);
  await acceptCookieConsent(page);
});

// ---------------------------------------------------------------- the hub

test("daily hub lists every live game with its edition and tagline", async ({ page }) => {
  await fulfillJson(page, `${API}/daily`, SLATE);
  await page.goto("/daily");

  await expect(page.getByTestId("daily-hub")).toBeVisible();
  await expect(page.getByTestId("daily-card-Fork")).toBeVisible();
  await expect(page.getByTestId("daily-card-CrowdCall")).toBeVisible();
  await expect(page.getByTestId("daily-card-PlaceIt")).toBeVisible();
  await expect(page.getByTestId("daily-card-PricedIn")).toBeVisible();

  await expect(page.getByTestId("daily-card-Fork")).toContainText("#12");
  await expect(page.getByTestId("daily-card-CrowdCall")).toContainText("Crowd Call");
});

test("hub shows the weekly ring, not a breakable streak counter", async ({ page }) => {
  await fulfillJson(page, `${API}/daily`, SLATE);
  await page.goto("/daily");

  // The docs rule out hard streaks: a soft "N of 7" ring is what we render.
  await expect(page.getByTestId("daily-cadence-count")).toHaveText("3 of 7 days this week");
});

test("anonymous players are told nothing is being saved", async ({ page }) => {
  await fulfillJson(page, `${API}/daily`, SLATE);
  await page.goto("/daily");

  await expect(page.getByTestId("daily-anon-note")).toContainText("aren't saved");
});

test("a slate with no live games degrades gracefully instead of erroring", async ({ page }) => {
  await fulfillJson(page, `${API}/daily`, { ...SLATE, puzzles: [] });
  await page.goto("/daily");

  await expect(page.getByTestId("daily-empty")).toBeVisible();
  await expect(page.getByTestId("daily-error")).toHaveCount(0);
});

test("a failed slate load says so rather than hanging on a spinner", async ({ page }) => {
  await page.route(`${API}/daily`, (route) =>
    route.request().method() === "OPTIONS"
      ? route.fulfill({ status: 204, headers: CORS })
      : route.fulfill({ status: 500, headers: CORS, json: {} }),
  );
  await page.goto("/daily");

  await expect(page.getByTestId("daily-error")).toBeVisible();
});

test("the daily hub is reachable from the primary nav", async ({ page }) => {
  await fulfillJson(page, `${API}/daily`, SLATE);
  await page.goto("/daily");

  // It's the funnel entrance, so it must not be buried in a dropdown.
  await expect(page.getByRole("link", { name: "Daily", exact: true }).first()).toBeVisible();
});

// ---------------------------------------------------------------- Fork

test("fork: one tap reveals the split, the share grid, and an explicit compass upsell", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/fork`, FORK_PUZZLE);
  await fulfillJson(page, `${API}/daily/fork/plays`, {
    puzzleId: "fork-1",
    kind: "Fork",
    edition: 12,
    completed: true,
    score: 0,
    attemptsUsed: 1,
    rounds: [],
    reveal: { axisKey: "economic-fairness", tradeoff: "…", provisionSlug: "data-center-grid-fee" },
    crowd: {
      national: { label: null, total: 412, suppressed: false, aPercent: 39, bPercent: 61 },
      locality: { label: "OH", total: 55, suppressed: false, aPercent: 44, bPercent: 56 },
      ageBand: null,
    },
    shareGrid: "Fork #12\n◧ I went A — 61% of the country went B.\nciversify.com/daily",
    pointsAwarded: 3,
  });

  await page.goto("/daily/fork");
  await expect(page.getByTestId("fork-game")).toBeVisible();

  // Both options must state what they cost — that's the neutrality guard, visible in the UI.
  await expect(page.getByTestId("fork-option-A")).toContainText("Costs you:");
  await expect(page.getByTestId("fork-option-B")).toContainText("Costs you:");

  await page.getByTestId("fork-option-A").click();

  await expect(page.getByTestId("fork-reveal")).toBeVisible();
  await expect(page.getByTestId("daily-crowd-bar").first()).toContainText("39%");
  await expect(page.getByTestId("daily-share-grid")).toContainText("Fork #12");
  await expect(page.getByTestId("daily-xp")).toContainText("+3");

  // A single tap never silently edits the compass — the upgrade is offered, not assumed.
  await expect(page.getByTestId("fork-reveal")).toContainText("Add it to your compass");
});

test("fork: a thin sample is suppressed rather than shown as a bogus percentage", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/fork`, FORK_PUZZLE);
  await fulfillJson(page, `${API}/daily/fork/plays`, {
    puzzleId: "fork-1",
    kind: "Fork",
    edition: 12,
    completed: true,
    score: 0,
    attemptsUsed: 1,
    rounds: [],
    reveal: {},
    crowd: {
      national: { label: null, total: 3, suppressed: true, aPercent: 0, bPercent: 0 },
      locality: null,
      ageBand: null,
    },
    shareGrid: "Fork #12\nciversify.com/daily",
    pointsAwarded: 3,
  });

  await page.goto("/daily/fork");
  await page.getByTestId("fork-option-B").click();

  await expect(page.getByTestId("daily-crowd-suppressed")).toContainText("only 3 plays");
});

test("fork: a failed submit surfaces an error instead of silently doing nothing", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/fork`, FORK_PUZZLE);
  await page.route(`${API}/daily/fork/plays`, (route) =>
    route.request().method() === "OPTIONS"
      ? route.fulfill({ status: 204, headers: CORS })
      : route.fulfill({ status: 500, headers: CORS, json: {} }),
  );

  await page.goto("/daily/fork");
  await page.getByTestId("fork-option-A").click();

  await expect(page.getByTestId("fork-error")).toBeVisible();
  // Still replayable — the tap didn't take.
  await expect(page.getByTestId("fork-option-A")).toBeEnabled();
});

// ----------------------------------------------------------- Crowd Call

test("crowd call: slide through both rounds, then see the true rates and attribution", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/crowd-call`, CROWDCALL_PUZZLE);
  await fulfillJson(page, `${API}/daily/crowd-call/plays`, {
    puzzleId: "cc-1",
    kind: "CrowdCall",
    edition: 88,
    completed: true,
    score: 82,
    attemptsUsed: 1,
    rounds: [
      { score: 90, band: "hit" },
      { score: 74, band: "near" },
    ],
    reveal: {
      rounds: [
        { trueRate: 0.68, sampleSize: 412 },
        { trueRate: 0.74, sampleSize: 1000 },
      ],
      overestimatedDivision: 1,
    },
    crowd: { plays: 300, averageScore: 61 },
    shareGrid: "Crowd Call #88 — 82/100\n🟩🟨\nI overestimated division on 1 of 2.\nciversify.com/daily",
    pointsAwarded: 3,
  });

  await page.goto("/daily/crowd-call");
  await expect(page.getByTestId("crowdcall-game")).toBeVisible();

  await page.getByTestId("crowdcall-slider").fill("60");
  await expect(page.getByTestId("crowdcall-guess")).toHaveText("60%");
  await page.getByTestId("crowdcall-next").click();

  await page.getByTestId("crowdcall-slider").fill("50");
  await page.getByTestId("crowdcall-next").click();

  await expect(page.getByTestId("crowdcall-result")).toContainText("82");
  await expect(page.getByTestId("crowdcall-result")).toContainText(
    "overestimated how divided people are on 1 of 2",
  );
  await expect(page.getByTestId("crowdcall-reveal-0")).toContainText("68%");

  // The crowd source is always named — attributing a published poll to our own users
  // (or the reverse) would be a credibility problem.
  await expect(page.getByTestId("crowdcall-reveal-0")).toContainText("Civersify players");
  await expect(page.getByTestId("crowdcall-reveal-1")).toContainText("Example National Poll");
  await expect(page.getByTestId("crowdcall-reveal-1")).toContainText("fielded 2026-01-15");
});

// -------------------------------------------------------------- Place It

test("place it: hints narrow the guess, then the reveal explains rather than scolds", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/place-it`, PLACEIT_PUZZLE);

  let round = 0;
  await page.route(`${API}/daily/place-it/rounds`, (route) => {
    if (route.request().method() === "OPTIONS")
      return route.fulfill({ status: 204, headers: CORS });
    round += 1;
    if (round === 1) {
      return route.fulfill({
        headers: CORS,
        json: {
          completed: false,
          roundsUsed: 1,
          roundsRemaining: 2,
          hints: ["higher", "lower", "exact"],
          result: null,
        },
      });
    }
    return route.fulfill({
      headers: CORS,
      json: {
        completed: true,
        roundsUsed: 2,
        roundsRemaining: 1,
        hints: ["exact", "exact", "exact"],
        result: {
          puzzleId: "pi-1",
          kind: "PlaceIt",
          edition: 31,
          completed: true,
          score: 85,
          attemptsUsed: 2,
          rounds: [
            { score: 100, band: "hit" },
            { score: 100, band: "hit" },
            { score: 100, band: "hit" },
          ],
          reveal: {
            billId: "bill-1",
            axes: [
              {
                axisKey: "authority",
                name: "Authority",
                trueBucket: 4,
                rationale: "The bill moves permitting authority from state agencies to a federal office.",
                evidence: null,
              },
              { axisKey: "risk", name: "Risk", trueBucket: 1, rationale: "It adds review steps.", evidence: null },
              { axisKey: "change-speed", name: "Change speed", trueBucket: 2, rationale: "Phased in.", evidence: null },
            ],
          },
          crowd: { plays: 120, averageScore: 58 },
          shareGrid: "Place It #31\n🟨🟩⬜\n🟩🟩🟩\nciversify.com/daily",
          pointsAwarded: 3,
        },
      },
    });
  });

  await page.goto("/daily/place-it");
  await expect(page.getByTestId("placeit-game")).toBeVisible();

  await page.getByTestId("placeit-submit").click();
  await expect(page.getByTestId("placeit-axis-authority")).toContainText("Further right");
  await expect(page.getByTestId("placeit-axis-risk")).toContainText("Further left");

  await page.getByTestId("placeit-authority-4").click();
  await page.getByTestId("placeit-submit").click();

  await expect(page.getByTestId("placeit-result")).toContainText("85");
  await expect(page.getByTestId("placeit-rationale-authority")).toContainText(
    "Our synthesis put this here",
  );
  // The truth here is our LLM synthesis, not ground truth — the copy must never say "wrong".
  await expect(page.getByTestId("placeit-game")).not.toContainText("wrong");
  await expect(page.getByTestId("placeit-game")).not.toContainText("incorrect");
  await expect(page.getByTestId("placeit-bill-link")).toBeVisible();
});

// ------------------------------------------------------------- Priced In

test("priced in: the ladder gives direction only, then reveals the figure and its source", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/priced-in`, PRICEDIN_PUZZLE);

  let guess = 0;
  await page.route(`${API}/daily/priced-in/guesses`, (route) => {
    if (route.request().method() === "OPTIONS")
      return route.fulfill({ status: 204, headers: CORS });
    guess += 1;
    if (guess === 1) {
      return route.fulfill({
        headers: CORS,
        json: {
          completed: false,
          guessesUsed: 1,
          guessesRemaining: 2,
          direction: "higher",
          result: null,
        },
      });
    }
    return route.fulfill({
      headers: CORS,
      json: {
        completed: true,
        guessesUsed: 2,
        guessesRemaining: 1,
        direction: "exact",
        result: {
          puzzleId: "pr-1",
          kind: "PricedIn",
          edition: 5,
          completed: true,
          score: 90,
          attemptsUsed: 2,
          rounds: [],
          reveal: {
            trueValue: 15750,
            anchor: "A single filer earning less than this owes no federal income tax at all.",
            source: "IRS Rev. Proc. 2024-40",
            sourceUrl: "https://example.org/irs",
            asOf: "2025-01-01",
            closeness: 1.02,
          },
          crowd: { plays: 88, averageScore: 44 },
          shareGrid: "Priced In #5\n🎯 Got it in 2 — within 1.25x\nciversify.com/daily",
          pointsAwarded: 3,
        },
      },
    });
  });

  await page.goto("/daily/priced-in");
  await expect(page.getByTestId("pricedin-game")).toBeVisible();

  await page.getByTestId("pricedin-guess-btn").click();
  await expect(page.getByTestId("pricedin-history")).toContainText("Higher");

  await page.getByTestId("pricedin-final").click();

  await expect(page.getByTestId("pricedin-truth")).toContainText("$15,750");
  await expect(page.getByTestId("pricedin-result")).toContainText("IRS Rev. Proc. 2024-40");
  await expect(page.getByTestId("pricedin-result")).toContainText("as of 2025-01-01");
});

// ----------------------------------------------------------- share + state

test.describe("share grid", () => {
  test.use({ permissions: ["clipboard-read", "clipboard-write"] });

  test("copying the result confirms it copied", async ({ page }) => {
    await fulfillJson(page, `${API}/daily/fork`, FORK_PUZZLE);
    await fulfillJson(page, `${API}/daily/fork/plays`, {
      puzzleId: "fork-1",
      kind: "Fork",
      edition: 12,
      completed: true,
      score: 0,
      attemptsUsed: 1,
      rounds: [],
      reveal: {},
      crowd: { national: { total: 400, suppressed: false, aPercent: 39, bPercent: 61 } },
      shareGrid: "Fork #12\n◧ I went A — 61% of the country went B.\nciversify.com/daily",
      pointsAwarded: 3,
    });

    await page.goto("/daily/fork");
    await page.getByTestId("fork-option-A").click();
    await page.getByTestId("daily-share-copy").click();

    // A copy button with no feedback reads as broken — this is the same complaint we
    // already had about the MFA backup-code copy.
    await expect(page.getByTestId("daily-share-copied")).toBeVisible();

    const clipboard = await page.evaluate(() => navigator.clipboard.readText());
    expect(clipboard).toContain("Fork #12");
    // The grid teases the split but never the question or the option text.
    expect(clipboard).not.toContain("data center");
  });
});

test("a game already played today says so instead of inviting a rejected replay", async ({
  page,
}) => {
  await fulfillJson(page, `${API}/daily/crowd-call`, {
    ...CROWDCALL_PUZZLE,
    play: { completed: true, score: 82, attemptsUsed: 1, response: null },
  });

  await page.goto("/daily/crowd-call");

  await expect(page.getByTestId("daily-already-played")).toContainText("82/100");
});

test("a game that isn't live today is a normal state, not an error", async ({ page }) => {
  await page.route(`${API}/daily/time-machine`, (route) =>
    route.request().method() === "OPTIONS"
      ? route.fulfill({ status: 204, headers: CORS })
      : route.fulfill({ status: 404, headers: CORS, json: {} }),
  );

  await page.goto("/daily/time-machine");

  await expect(page.getByTestId("daily-game-empty")).toBeVisible();
});

test("an unknown game slug points back at the hub", async ({ page }) => {
  await page.goto("/daily/not-a-real-game");

  await expect(page.getByTestId("daily-unknown")).toBeVisible();
});
