import {
  ADDITIONAL_MEDICARE_RATE,
  ADDITIONAL_MEDICARE_THRESHOLD,
  BRACKETS,
  MEDICARE_RATE,
  SOCIAL_SECURITY_RATE,
  SOCIAL_SECURITY_WAGE_BASE,
  STANDARD_DEDUCTION,
} from "./constants";
import type {
  CombinedResult,
  FederalResult,
  FilingStatus,
  IncomeRule,
  StateProfile,
  StateResult,
  TaxBracket,
  TaxComputeResult,
} from "./types";

/**
 * Walk a marginal bracket schedule. Each band's rate applies only to taxable income
 * within it. Brackets must be ordered ascending by lower bound.
 */
export function progressive(taxableIncome: number, brackets: TaxBracket[]): number {
  if (taxableIncome <= 0 || brackets.length === 0) return 0;

  let tax = 0;
  for (let i = 0; i < brackets.length; i++) {
    const lower = brackets[i].lower;
    if (taxableIncome <= lower) break;
    const upper = i + 1 < brackets.length ? brackets[i + 1].lower : Infinity;
    const bandTop = Math.min(taxableIncome, upper);
    tax += (bandTop - lower) * brackets[i].rate;
  }
  return tax;
}

/** Federal tax for one gross wage income (§3.4). */
export function computeFederal(income: number, filing: FilingStatus): FederalResult {
  const inc = Math.max(0, income);

  const taxableIncome = Math.max(0, inc - STANDARD_DEDUCTION[filing]);
  const incomeTax = progressive(taxableIncome, BRACKETS[filing]);

  const socialSecurity = Math.min(inc, SOCIAL_SECURITY_WAGE_BASE) * SOCIAL_SECURITY_RATE;
  const medicare = inc * MEDICARE_RATE;
  const addlMedicare =
    Math.max(0, inc - ADDITIONAL_MEDICARE_THRESHOLD[filing]) * ADDITIONAL_MEDICARE_RATE;
  const fica = socialSecurity + medicare + addlMedicare;

  const total = incomeTax + fica;
  return {
    incomeTax,
    socialSecurity,
    medicare,
    addlMedicare,
    fica,
    total,
    effectiveRate: inc > 0 ? total / inc : 0,
  };
}

/** State income tax under a state's income rule (§4.2). */
export function computeStateIncomeTax(income: number, rule: IncomeRule): number {
  const inc = Math.max(0, income);
  switch (rule.type) {
    case "none":
      return 0;
    case "flat":
      return Math.max(0, inc - rule.stdDed) * rule.rate;
    case "progressive":
      return progressive(Math.max(0, inc - rule.stdDed), rule.brackets);
  }
}

/** State + local tax for one income against a state profile (§4.2). */
export function computeState(income: number, profile: StateProfile): StateResult {
  const inc = Math.max(0, income);
  const incomeTax = computeStateIncomeTax(inc, profile.income);
  const salesTax = inc * profile.consumptionShare * profile.salesRate;
  const propertyTax = inc * profile.homeMultiple * profile.propRate;
  const total = incomeTax + salesTax + propertyTax;
  return {
    incomeTax,
    salesTax,
    propertyTax,
    total,
    effectiveRate: inc > 0 ? total / inc : 0,
  };
}

/** Combine federal and state/local into the apportionment split (§3.4 / §4.2). */
export function combine(
  income: number,
  federal: FederalResult,
  state: StateResult,
): CombinedResult {
  const total = federal.total + state.total;
  return {
    total,
    effectiveRate: income > 0 ? total / income : 0,
    federalShare: total > 0 ? federal.total / total : 0,
    stateShare: total > 0 ? state.total / total : 0,
  };
}

/** Full compute for one (income, filing, state) — mirrors GET /api/tax-model/compute. */
export function compute(
  income: number,
  filing: FilingStatus,
  profile: StateProfile,
): TaxComputeResult {
  const federal = computeFederal(income, filing);
  const stateLocal = computeState(income, profile);
  return {
    income,
    filing,
    state: profile.code,
    federal,
    stateLocal,
    combined: combine(income, federal, stateLocal),
  };
}
