import { HouseholdCalculator } from "frontend-civic";

// HouseholdCalculator is the centerpiece: an income slider + filing toggle + state
// picker on top, a headline SplitBar with a prose summary, and two breakdown columns
// (federal line items vs. state & local line items). It is fully controlled, so we
// author one rendered state by passing matching `income` / `filing` / `profile` and a
// precomputed `result` (the exact tax-engine output for that household). Handlers are
// no-ops in the static preview.

const STATES = [
  { code: "CA", name: "California", glyph: "🌅", incomeSummary: "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)", income: { type: "progressive" }, salesRate: 0.0882, consumptionShare: 0.4, propRate: 0.0068, homeMultiple: 4.5, notes: "" },
  { code: "NY", name: "New York", glyph: "🗽", incomeSummary: "Progressive, 4%–10.9%", income: { type: "progressive" }, salesRate: 0.0853, consumptionShare: 0.4, propRate: 0.014, homeMultiple: 3.5, notes: "" },
  { code: "TX", name: "Texas", glyph: "🤠", incomeSummary: "None", income: { type: "none" }, salesRate: 0.082, consumptionShare: 0.4, propRate: 0.0158, homeMultiple: 3.2, notes: "" },
  { code: "CO", name: "Colorado", glyph: "🏔️", incomeSummary: "Flat 4.40%", income: { type: "flat" }, salesRate: 0.0781, consumptionShare: 0.4, propRate: 0.0049, homeMultiple: 4.8, notes: "" },
];

const california = {
  code: "CA",
  name: "California",
  glyph: "🌅",
  incomeSummary: "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)",
  income: { type: "progressive", stdDed: 5_540, brackets: [] },
  salesRate: 0.0882,
  consumptionShare: 0.4,
  propRate: 0.0068,
  homeMultiple: 4.5,
  notes:
    "Top 13.3% rate includes a 1% Mental Health Services surcharge over $1M. " +
    "No tax on Social Security benefits.",
};

// Real engine output for $85,000 / single / California.
const caResult = {
  income: 85_000,
  filing: "single",
  state: "CA",
  federal: {
    incomeTax: 10_149,
    socialSecurity: 5_270,
    medicare: 1_233,
    addlMedicare: 0,
    fica: 6_503,
    total: 16_652,
    effectiveRate: 0.196,
  },
  stateLocal: {
    incomeTax: 3_932,
    salesTax: 2_999,
    propertyTax: 2_601,
    total: 9_532,
    effectiveRate: 0.112,
  },
  combined: {
    total: 26_183,
    effectiveRate: 0.308,
    federalShare: 0.636,
    stateShare: 0.364,
  },
};

const texas = {
  code: "TX",
  name: "Texas",
  glyph: "🤠",
  incomeSummary: "None",
  income: { type: "none" },
  salesRate: 0.082,
  consumptionShare: 0.4,
  propRate: 0.0158,
  homeMultiple: 3.2,
  notes:
    "No personal income tax. The state leans on high property taxes and a high " +
    "combined sales tax to fund itself.",
};

// Real engine output for $60,000 / single / Texas (no state income tax).
const txResult = {
  income: 60_000,
  filing: "single",
  state: "TX",
  federal: {
    incomeTax: 5_072,
    socialSecurity: 3_720,
    medicare: 870,
    addlMedicare: 0,
    fica: 4_590,
    total: 9_662,
    effectiveRate: 0.161,
  },
  stateLocal: {
    incomeTax: 0,
    salesTax: 1_968,
    propertyTax: 3_034,
    total: 5_002,
    effectiveRate: 0.083,
  },
  combined: {
    total: 14_663,
    effectiveRate: 0.244,
    federalShare: 0.659,
    stateShare: 0.341,
  },
};

const noop = () => {};

// Canonical: $85,000 single household in California.
export const Canonical = () => (
  <div style={{ maxWidth: 920 }}>
    <HouseholdCalculator
      income={85_000}
      filing={"single" as any}
      stateCode="CA"
      profile={california as any}
      result={caResult as any}
      states={STATES as any}
      onIncomeChange={noop}
      onFilingChange={noop}
      onStateChange={noop}
    />
  </div>
);

// No-income-tax state: $60,000 single in Texas — the state income line reads $0 and
// the prose/SplitBar shift the share toward federal.
export const NoIncomeTaxState = () => (
  <div style={{ maxWidth: 920 }}>
    <HouseholdCalculator
      income={60_000}
      filing={"single" as any}
      stateCode="TX"
      profile={texas as any}
      result={txResult as any}
      states={STATES as any}
      onIncomeChange={noop}
      onFilingChange={noop}
      onStateChange={noop}
    />
  </div>
);
