import { StatePicker } from "frontend-civic";

// StatePicker is a compact native <select> over the available state profiles, with
// the selected state's glyph + income-tax summary shown inline beside it. We feed
// the real v1 state list and select one of them so the inline summary renders.

const STATES = [
  { code: "CA", name: "California", glyph: "🌅", incomeSummary: "Progressive, 1%–13.3% (incl. 1% MHS surcharge >$1M)", income: { type: "progressive" }, salesRate: 0.0882, consumptionShare: 0.4, propRate: 0.0068, homeMultiple: 4.5, notes: "" },
  { code: "NY", name: "New York", glyph: "🗽", incomeSummary: "Progressive, 4%–10.9%", income: { type: "progressive" }, salesRate: 0.0853, consumptionShare: 0.4, propRate: 0.014, homeMultiple: 3.5, notes: "" },
  { code: "TX", name: "Texas", glyph: "🤠", incomeSummary: "None", income: { type: "none" }, salesRate: 0.082, consumptionShare: 0.4, propRate: 0.0158, homeMultiple: 3.2, notes: "" },
  { code: "FL", name: "Florida", glyph: "🌴", incomeSummary: "None", income: { type: "none" }, salesRate: 0.07, consumptionShare: 0.4, propRate: 0.0091, homeMultiple: 3.6, notes: "" },
  { code: "WA", name: "Washington", glyph: "🌲", incomeSummary: "None (7% cap-gains tax >$262k only)", income: { type: "none" }, salesRate: 0.0938, consumptionShare: 0.4, propRate: 0.0087, homeMultiple: 4.2, notes: "" },
  { code: "CO", name: "Colorado", glyph: "🏔️", incomeSummary: "Flat 4.40%", income: { type: "flat" }, salesRate: 0.0781, consumptionShare: 0.4, propRate: 0.0049, homeMultiple: 4.8, notes: "" },
  { code: "PA", name: "Pennsylvania", glyph: "🔔", incomeSummary: "Flat 3.07%, no std. deduction", income: { type: "flat" }, salesRate: 0.0634, consumptionShare: 0.4, propRate: 0.0149, homeMultiple: 2.9, notes: "" },
  { code: "IL", name: "Illinois", glyph: "🌽", incomeSummary: "Flat 4.95%, no std. deduction", income: { type: "flat" }, salesRate: 0.0886, consumptionShare: 0.4, propRate: 0.0188, homeMultiple: 3.0, notes: "" },
];

// Default selection — a progressive state, summary reads its full bracket range.
export const Default = () => (
  <div style={{ maxWidth: 680 }}>
    <StatePicker states={STATES as any} selected="CA" onSelect={() => {}} />
  </div>
);

// A no-income-tax state selected — inline summary collapses to "None".
export const NoIncomeTaxSelected = () => (
  <div style={{ maxWidth: 680 }}>
    <StatePicker states={STATES as any} selected="TX" onSelect={() => {}} />
  </div>
);
