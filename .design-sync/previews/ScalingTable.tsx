import { ScalingTable } from "frontend-civic";

// ScalingTable computes federal vs. state/local vs. federal-share across the six
// preset ladder incomes ($30k–$750k) for the given state + filing, each row carrying
// a slim SplitBar. It calls the tax engine internally, so it only needs filing,
// profile, and currentIncome (used to highlight the nearest "you are here" row).

const california = {
  code: "CA",
  name: "California",
  glyph: "🌅",
  incomeSummary: "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)",
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
  consumptionShare: 0.4,
  propRate: 0.0068,
  homeMultiple: 4.5,
  notes: "",
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
  notes: "",
};

// Progressive state, with the $100k row highlighted as the current household.
export const Progressive = () => (
  <div style={{ maxWidth: 680 }}>
    <ScalingTable filing="single" profile={california as any} currentIncome={100_000} />
  </div>
);

// No-income-tax state, married/joint — the federal share climbs harder with income
// because no progressive state tax offsets it. $175k row highlighted.
export const NoIncomeTaxState = () => (
  <div style={{ maxWidth: 680 }}>
    <ScalingTable filing="mfj" profile={texas as any} currentIncome={175_000} />
  </div>
);
