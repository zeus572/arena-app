import { StateCard } from "frontend-civic";

// StateCard shows a selected state's glyph + name, three headline stats
// (income-tax summary, combined sales rate, effective property rate) drawn from the
// StateProfile, and the stored `notes` prose. Profiles below are the real v1
// per-state parameters from the tax engine's stateProfiles.ts.

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
  notes:
    "Top 13.3% rate includes a 1% Mental Health Services surcharge over $1M. " +
    "No tax on Social Security benefits. Prop. 13 caps assessed-value growth, " +
    "so effective rates on long-held homes run lower.",
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
    "No personal income tax. The state leans on some of the nation's highest property " +
    "taxes and a high combined sales tax to fund itself.",
};

const pennsylvania = {
  code: "PA",
  name: "Pennsylvania",
  glyph: "🔔",
  incomeSummary: "Flat 3.07%, no std. deduction",
  income: { type: "flat", rate: 0.0307, stdDed: 0 },
  salesRate: 0.0634,
  consumptionShare: 0.4,
  propRate: 0.0149,
  homeMultiple: 2.9,
  notes:
    "Flat 3.07% with no standard deduction — the first dollar is taxed. " +
    "Many municipalities add a local earned-income tax (~1%), not modeled here.",
};

// Progressive state — the most structurally complete card.
export const Progressive = () => (
  <div style={{ maxWidth: 680 }}>
    <StateCard profile={california as any} />
  </div>
);

// No-income-tax state — income summary reads "None".
export const NoIncomeTax = () => (
  <div style={{ maxWidth: 680 }}>
    <StateCard profile={texas as any} />
  </div>
);

// Flat-rate state.
export const FlatRate = () => (
  <div style={{ maxWidth: 680 }}>
    <StateCard profile={pennsylvania as any} />
  </div>
);
