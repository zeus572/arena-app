// Tax Apportionment engine — deterministic, LLM-free. See docs/TaxApportionment_Spec.md.
// This TypeScript engine and the C# engine (backend-civic/Services/TaxModel) are kept
// in lockstep: both reproduce the same golden-value table (§6). A literal shared module
// across the TS/C# boundary isn't possible, so the contract is enforced by the identical
// golden tests in each project (engine.test.ts here, TaxEngineTests.cs there).

export type FilingStatus = "single" | "mfj";

/** A marginal bracket: `lower` is the taxable-income lower bound where `rate` begins. */
export type TaxBracket = { lower: number; rate: number };

/** A state's income-tax structure (§4.1). */
export type IncomeRule =
  | { type: "none" }
  | { type: "flat"; rate: number; stdDed: number }
  | { type: "progressive"; stdDed: number; brackets: TaxBracket[] };

/** Real per-state parameters (§4). `notes` is stored prose, never generated at compute time. */
export interface StateProfile {
  code: string;
  name: string;
  glyph: string;
  incomeSummary: string;
  income: IncomeRule;
  salesRate: number;
  consumptionShare: number;
  propRate: number;
  homeMultiple: number;
  notes: string;
}

export interface FederalResult {
  incomeTax: number;
  socialSecurity: number;
  medicare: number;
  addlMedicare: number;
  fica: number;
  total: number;
  effectiveRate: number;
}

export interface StateResult {
  incomeTax: number;
  salesTax: number;
  propertyTax: number;
  total: number;
  effectiveRate: number;
}

export interface CombinedResult {
  total: number;
  effectiveRate: number;
  federalShare: number;
  stateShare: number;
}

export interface TaxComputeResult {
  income: number;
  filing: FilingStatus;
  state: string;
  federal: FederalResult;
  stateLocal: StateResult;
  combined: CombinedResult;
}
