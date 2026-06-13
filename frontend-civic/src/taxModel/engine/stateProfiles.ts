import type { StateProfile } from "./types";

// Real per-state parameters (§4.3), verified 2025-2026. v1 ships 8 representative
// states spanning the full structural range (progressive / flat / none); the engine
// already supports all 50 — the remaining 42 are data work, deferred (§8).
//
// Single-filer income schedules are simplified to top-line brackets; sales rates are
// combined state+local averages; property rates are state effective averages;
// consumptionShare = 0.40 across all states in v1 (flagged as an assumption in the UI).
// Notes prose is stored verbatim, never generated at compute time.

export const CONSUMPTION_SHARE = 0.4;

export const STATE_PROFILES: StateProfile[] = [
  {
    code: "CA",
    name: "California",
    glyph: "🌅",
    incomeSummary: "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)",
    // State std. deduction ≈ $5,540. Top 13.3% = 12.3% + 1% Mental Health Services
    // surcharge on taxable income over $1M.
    income: {
      type: "progressive",
      stdDed: 5_540,
      brackets: [
        { lower: 0, rate: 0.01 },
        { lower: 10_756, rate: 0.02 },
        { lower: 25_499, rate: 0.04 },
        { lower: 40_245, rate: 0.06 },
        { lower: 55_866, rate: 0.08 },
        { lower: 70_606, rate: 0.093 },
        { lower: 360_659, rate: 0.103 },
        { lower: 432_787, rate: 0.113 },
        { lower: 721_314, rate: 0.123 },
        { lower: 1_000_000, rate: 0.133 },
      ],
    },
    salesRate: 0.0882,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0068,
    homeMultiple: 4.5,
    notes:
      "Top 13.3% rate includes a 1% Mental Health Services surcharge over $1M. " +
      "No tax on Social Security benefits. Prop. 13 caps assessed-value growth, " +
      "so effective rates on long-held homes run lower.",
  },
  {
    code: "NY",
    name: "New York",
    glyph: "🗽",
    incomeSummary: "Progressive, 4%–10.9%",
    income: {
      type: "progressive",
      stdDed: 8_000,
      brackets: [
        { lower: 0, rate: 0.04 },
        { lower: 8_500, rate: 0.045 },
        { lower: 11_700, rate: 0.0525 },
        { lower: 80_650, rate: 0.055 },
        { lower: 215_400, rate: 0.06 },
        { lower: 1_077_550, rate: 0.0685 },
        { lower: 5_000_000, rate: 0.0965 },
        { lower: 25_000_000, rate: 0.109 },
      ],
    },
    salesRate: 0.0853,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.014,
    homeMultiple: 3.5,
    notes:
      "NYC and Yonkers stack a local income tax on top of the state rate — not modeled " +
      "here, so a city resident's burden is higher. High property tax, especially outside NYC.",
  },
  {
    code: "TX",
    name: "Texas",
    glyph: "🤠",
    incomeSummary: "None",
    income: { type: "none" },
    salesRate: 0.082,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0158,
    homeMultiple: 3.2,
    notes:
      "No personal income tax. The state leans on some of the nation's highest property " +
      "taxes and a high combined sales tax to fund itself.",
  },
  {
    code: "FL",
    name: "Florida",
    glyph: "🌴",
    incomeSummary: "None",
    income: { type: "none" },
    salesRate: 0.07,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0091,
    homeMultiple: 3.6,
    notes:
      "No personal income tax. Homestead exemption and a 'Save Our Homes' assessment cap " +
      "soften property tax for primary residents. Tourism shifts some sales-tax burden onto visitors.",
  },
  {
    code: "WA",
    name: "Washington",
    glyph: "🌲",
    incomeSummary: "None (7% cap-gains tax >$262k only)",
    income: { type: "none" },
    salesRate: 0.0938,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0087,
    homeMultiple: 4.2,
    notes:
      "No wage income tax, but a 7% capital-gains tax applies above ~$262k of gains " +
      "(not modeled — this calculator assumes wage income). Among the highest combined " +
      "sales-tax rates in the country.",
  },
  {
    code: "CO",
    name: "Colorado",
    glyph: "🏔️",
    incomeSummary: "Flat 4.40%",
    // Flat 4.4% on federal taxable income — std. deduction mirrors federal $15,750.
    income: { type: "flat", rate: 0.044, stdDed: 15_750 },
    salesRate: 0.0781,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0049,
    homeMultiple: 4.8,
    notes:
      "Flat 4.4% on federal taxable income. Low effective property tax rate, " +
      "but high home values raise the dollar amount.",
  },
  {
    code: "PA",
    name: "Pennsylvania",
    glyph: "🔔",
    incomeSummary: "Flat 3.07%, no std. deduction",
    income: { type: "flat", rate: 0.0307, stdDed: 0 },
    salesRate: 0.0634,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0149,
    homeMultiple: 2.9,
    notes:
      "Flat 3.07% with no standard deduction — the first dollar is taxed. " +
      "Many municipalities add a local earned-income tax (~1%), not modeled here.",
  },
  {
    code: "IL",
    name: "Illinois",
    glyph: "🌽",
    incomeSummary: "Flat 4.95%, no std. deduction",
    income: { type: "flat", rate: 0.0495, stdDed: 0 },
    salesRate: 0.0886,
    consumptionShare: CONSUMPTION_SHARE,
    propRate: 0.0188,
    homeMultiple: 3.0,
    notes:
      "Flat 4.95%, no standard deduction (a small personal exemption applies instead, " +
      "omitted here). Among the highest effective property tax rates in the nation. " +
      "Retirement income is exempt.",
  },
];

const BY_CODE = new Map(STATE_PROFILES.map((s) => [s.code, s]));

/** Look up a state profile by 2-letter code. Undefined if unknown. */
export function findStateProfile(code: string): StateProfile | undefined {
  return BY_CODE.get(code.toUpperCase());
}
