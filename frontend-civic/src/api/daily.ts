import { civicApi } from "./client";

/**
 * The casual daily games. See docs/civic_daily_games/.
 *
 * The server strips every answer-key field before serving a puzzle, so the payload
 * types here are deliberately the REDACTED shapes — the truth only ever arrives in a
 * result's `reveal`. Don't add solution fields to the payload types; if one appears in
 * a response, that's a backend redaction bug, not a typing gap.
 */
export type DailyGameKind =
  | "Fork"
  | "CrowdCall"
  | "PricedIn"
  | "PlaceIt"
  | "TimeMachine"
  | "WhoseValue"
  | "WhichIsTrue";

/** URL segment for a kind — the API accepts kebab-case. */
export const kindSlug: Record<DailyGameKind, string> = {
  Fork: "fork",
  CrowdCall: "crowd-call",
  PricedIn: "priced-in",
  PlaceIt: "place-it",
  TimeMachine: "time-machine",
  WhoseValue: "whose-value",
  WhichIsTrue: "which-is-true",
};

export const slugToKind: Record<string, DailyGameKind> = Object.fromEntries(
  Object.entries(kindSlug).map(([k, v]) => [v, k as DailyGameKind]),
) as Record<string, DailyGameKind>;

export const gameTitle: Record<DailyGameKind, string> = {
  Fork: "Fork",
  CrowdCall: "Crowd Call",
  PricedIn: "Priced In",
  PlaceIt: "Place It",
  TimeMachine: "Time Machine",
  WhoseValue: "Whose Value",
  WhichIsTrue: "Which Is True",
};

export const gameTagline: Record<DailyGameKind, string> = {
  Fork: "Two costly options. One tap.",
  CrowdCall: "Guess what the country actually knows.",
  PricedIn: "How big is that number, really?",
  PlaceIt: "Where does this bill sit on your compass?",
  TimeMachine: "Real headlines, wrong order.",
  WhoseValue: "Name the value behind the argument.",
  WhichIsTrue: "Two real numbers. Only one answers the question.",
};

// ---------------------------------------------------------- payload shapes

export type ForkPayload = {
  question: string;
  tradeoff: string;
  optionA: { label: string; cost: string };
  optionB: { label: string; cost: string };
  axisKey: string;
  subQuestionKey: string;
  provisionSlug: string | null;
};

/** `trueRate` and `sampleSize` are stripped server-side. */
export type CrowdCallPayload = {
  rounds: {
    prompt: string;
    answer: string;
    explanation: string;
    crowdSource: "civic-users" | "national-poll";
    attribution: string;
    sourceUrl: string | null;
    fieldedOn: string | null;
  }[];
};

/** `trueValue` and `anchor` are stripped server-side. */
export type PricedInPayload = {
  prompt: string;
  unit: string;
  minBound: number;
  maxBound: number;
  maxGuesses: number;
  source: string;
  sourceUrl: string | null;
  asOf: string | null;
};

/** `trueBucket`, `rationale` and `evidence` are stripped server-side. */
export type PlaceItPayload = {
  billId: string;
  billTitle: string;
  billSummary: string;
  billStatus: string;
  axes: { axisKey: string; name: string; lowLabel: string; highLabel: string }[];
  maxRounds: number;
};

/** `trueOrder`, `currentItemId` and `dates` are stripped server-side. */
export type TimeMachinePayload = {
  mode: "sort" | "oddOneOut";
  items: { id: string; headline: string; publisher: string }[];
  urls: Record<string, string>;
  revealLine: string;
};

/** `correctAxisKey`, `billTitle` and `billId` are stripped server-side. */
export type WhoseValuePayload = {
  rounds: {
    argument: string;
    choices: { axisKey: string; name: string; lowLabel: string; highLabel: string }[];
  }[];
};

/**
 * Every answer-adjacent field is stripped server-side: `correct`, `explanation`,
 * `decoyTruth`, and — unlike the other games — the provenance too. With only two options
 * on the card, a citation is an answer key. It all comes back in the reveal.
 */
export type WhichIsTruePayload = {
  rounds: {
    /** "Federal budget" | "State & local tax" | "Congress" */
    topic: string;
    prompt: string;
    optionA: string;
    optionB: string;
  }[];
};

export type DailyPayload =
  | ForkPayload
  | CrowdCallPayload
  | PricedInPayload
  | PlaceItPayload
  | TimeMachinePayload
  | WhoseValuePayload
  | WhichIsTruePayload;

// ------------------------------------------------------------------- DTOs

export type DailyPlayState = {
  completed: boolean;
  score: number;
  attemptsUsed: number;
  response: unknown;
};

export type DailyPuzzle = {
  id: string;
  kind: DailyGameKind;
  puzzleDate: string;
  edition: number;
  payloadVersion: number;
  locality: string | null;
  payload: DailyPayload;
  play: DailyPlayState | null;
};

export type DailyCadence = { last7Days: boolean[]; activeDays: number };

export type DailySlate = {
  date: string;
  puzzles: DailyPuzzle[];
  cadence: DailyCadence;
  /** True when the caller has no stable id, so nothing is recorded. */
  anonymous: boolean;
};

export type DailyRoundResult = { score: number; band: "hit" | "near" | "miss" };

export type DailyResult = {
  puzzleId: string;
  kind: DailyGameKind;
  edition: number;
  completed: boolean;
  score: number;
  attemptsUsed: number;
  rounds: DailyRoundResult[];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  reveal: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  crowd: any;
  shareGrid: string;
  pointsAwarded: number;
};

export type PlaceItRoundResult = {
  completed: boolean;
  roundsUsed: number;
  roundsRemaining: number;
  hints: ("exact" | "higher" | "lower")[];
  result: DailyResult | null;
};

export type PricedInGuessResult = {
  completed: boolean;
  guessesUsed: number;
  guessesRemaining: number;
  direction: "higher" | "lower" | "exact";
  result: DailyResult | null;
};

export type DailyArchiveRow = {
  puzzleId: string;
  edition: number;
  puzzleDate: string;
  played: boolean;
  score: number;
};

// ------------------------------------------------------------------ calls

/** Today's slate — every live game plus this player's state. One round-trip for the hub. */
export async function getDailySlate(date?: string): Promise<DailySlate> {
  const { data } = await civicApi.get<DailySlate>("/daily", {
    params: date ? { date } : undefined,
  });
  return data;
}

export async function getDailyPuzzle(
  kind: DailyGameKind,
  date?: string,
): Promise<DailyPuzzle> {
  const { data } = await civicApi.get<DailyPuzzle>(`/daily/${kindSlug[kind]}`, {
    params: date ? { date } : undefined,
  });
  return data;
}

/** Single-shot submission: Fork, Crowd Call, Time Machine, Whose Value. */
export async function submitDailyPlay(
  kind: DailyGameKind,
  body: unknown,
  date?: string,
): Promise<DailyResult> {
  const { data } = await civicApi.post<DailyResult>(
    `/daily/${kindSlug[kind]}/plays`,
    body,
    { params: date ? { date } : undefined },
  );
  return data;
}

export async function submitPlaceItRound(
  guesses: number[],
  date?: string,
): Promise<PlaceItRoundResult> {
  const { data } = await civicApi.post<PlaceItRoundResult>(
    "/daily/place-it/rounds",
    { guesses },
    { params: date ? { date } : undefined },
  );
  return data;
}

export async function submitPricedInGuess(
  guess: number,
  final: boolean,
  date?: string,
): Promise<PricedInGuessResult> {
  const { data } = await civicApi.post<PricedInGuessResult>(
    "/daily/priced-in/guesses",
    { guess, final },
    { params: date ? { date } : undefined },
  );
  return data;
}

export async function getDailyArchive(
  kind: DailyGameKind,
  take = 14,
): Promise<DailyArchiveRow[]> {
  const { data } = await civicApi.get<DailyArchiveRow[]>(
    `/daily/${kindSlug[kind]}/archive`,
    { params: { take } },
  );
  return data;
}
