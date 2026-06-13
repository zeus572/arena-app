import { describe, it, expect } from "vitest";
import {
  combine,
  compute,
  computeFederal,
  computeState,
  computeStateIncomeTax,
  findStateProfile,
  LADDER_INCOMES,
  STATE_PROFILES,
} from "./index";

/**
 * Phase 1 gate — golden-value tests (§6). This table mirrors the C# engine's
 * TaxEngineTests.cs exactly; the two engines are kept in lockstep.
 *
 * NOTE: the spec's §6 federal income-tax column is internally inconsistent with the
 * §3 brackets for $60k/$100k/$250k (a correct marginal walk over the genuine 2025
 * single brackets yields different figures). The spec says §3 is the source of truth
 * and to "compute exactly", so we assert the CORRECT computed values. Every FICA value
 * and state spot-check below matches §6 verbatim. See TaxEngineTests.cs for the full
 * reconciliation table.
 */
describe("federal engine — golden rows (single)", () => {
  const rows = [
    // income, incomeTax, ss, medicare, addlMed, fica, total
    { income: 30_000, incomeTax: 1_471.5, ss: 1_860, medicare: 435, addlMed: 0, fica: 2_295, total: 3_766.5 },
    { income: 60_000, incomeTax: 5_071.5, ss: 3_720, medicare: 870, addlMed: 0, fica: 4_590, total: 9_661.5 },
    { income: 100_000, incomeTax: 13_449, ss: 6_200, medicare: 1_450, addlMed: 0, fica: 7_650, total: 21_099 },
    { income: 250_000, incomeTax: 52_023, ss: 10_918.2, medicare: 3_625, addlMed: 450, fica: 14_993.2, total: 67_016.2 },
  ];

  it.each(rows)("income $%s reproduces the golden federal row", (r) => {
    const f = computeFederal(r.income, "single");
    expect(f.incomeTax).toBeCloseTo(r.incomeTax, 2);
    expect(f.socialSecurity).toBeCloseTo(r.ss, 2);
    expect(f.medicare).toBeCloseTo(r.medicare, 2);
    expect(f.addlMedicare).toBeCloseTo(r.addlMed, 2);
    expect(f.fica).toBeCloseTo(r.fica, 2);
    expect(f.total).toBeCloseTo(r.total, 2);
  });

  it("caps Social Security at the wage base", () => {
    expect(computeFederal(250_000, "single").socialSecurity).toBeCloseTo(176_100 * 0.062, 2);
    expect(computeFederal(2_000_000, "single").socialSecurity).toBeCloseTo(176_100 * 0.062, 2);
  });

  it("applies additional Medicare over $200k (single) / $250k (mfj)", () => {
    expect(computeFederal(250_000, "single").addlMedicare).toBeCloseTo(450, 2);
    expect(computeFederal(200_000, "single").addlMedicare).toBe(0);
    expect(computeFederal(300_000, "mfj").addlMedicare).toBeCloseTo((300_000 - 250_000) * 0.009, 2);
  });

  it("produces no income tax below the standard deduction but still charges FICA", () => {
    const low = computeFederal(10_000, "single");
    expect(low.incomeTax).toBe(0);
    expect(low.fica).toBeCloseTo(10_000 * (0.062 + 0.0145), 2);
    expect(computeFederal(0, "single").total).toBe(0);
  });
});

describe("state engine — golden spot-checks (income $100k, single)", () => {
  it("TX: no income tax, sales + property only", () => {
    const r = computeState(100_000, findStateProfile("TX")!);
    expect(r.incomeTax).toBe(0);
    expect(r.salesTax).toBeCloseTo(3_280, 2);
    expect(r.propertyTax).toBeCloseTo(5_056, 2);
    expect(r.total).toBeCloseTo(8_336, 2);
  });

  it("PA: flat 3.07% with no standard deduction", () => {
    const r = computeState(100_000, findStateProfile("PA")!);
    expect(r.incomeTax).toBeCloseTo(3_070, 2);
  });

  it("CA: matches the spec formula", () => {
    const r = computeState(100_000, findStateProfile("CA")!);
    expect(r.salesTax).toBeCloseTo(3_528, 2);
    expect(r.propertyTax).toBeCloseTo(3_060, 2);
    // progressive over CA brackets on (100,000 − 5,540) = 94,460.
    expect(r.incomeTax).toBeCloseTo(5_327.142, 2);
  });
});

describe("combined apportionment", () => {
  it("federal + state shares sum to 1 for every shipped state", () => {
    for (const profile of STATE_PROFILES) {
      const federal = computeFederal(100_000, "single");
      const state = computeState(100_000, profile);
      const c = combine(100_000, federal, state);
      expect(c.total).toBeCloseTo(federal.total + state.total, 2);
      expect(c.federalShare + c.stateShare).toBeCloseTo(1, 9);
      expect(c.federalShare).toBeGreaterThan(0);
      expect(c.federalShare).toBeLessThan(1);
    }
  });

  it("federal share rises with income (steeply progressive federal income tax)", () => {
    const tx = findStateProfile("TX")!;
    const shareAt = (income: number) =>
      combine(income, computeFederal(income, "single"), computeState(income, tx)).federalShare;
    expect(shareAt(750_000)).toBeGreaterThan(shareAt(30_000));
  });
});

describe("income rules and catalog", () => {
  it("none / flat behave as specified", () => {
    expect(computeStateIncomeTax(100_000, { type: "none" })).toBe(0);
    expect(computeStateIncomeTax(100_000, { type: "flat", rate: 0.05, stdDed: 10_000 })).toBeCloseTo(4_500, 2);
    expect(computeStateIncomeTax(5_000, { type: "flat", rate: 0.05, stdDed: 10_000 })).toBe(0);
  });

  it("ships 8 unique verified states and 6 ladder presets", () => {
    expect(STATE_PROFILES).toHaveLength(8);
    expect(new Set(STATE_PROFILES.map((s) => s.code)).size).toBe(8);
    expect(STATE_PROFILES.every((s) => s.consumptionShare === 0.4)).toBe(true);
    expect(LADDER_INCOMES).toEqual([30_000, 60_000, 100_000, 175_000, 350_000, 750_000]);
  });

  it("compute() returns the full wire shape", () => {
    const r = compute(100_000, "single", findStateProfile("TX")!);
    expect(r.state).toBe("TX");
    expect(r.filing).toBe("single");
    expect(r.federal.total).toBeGreaterThan(0);
    expect(r.combined.federalShare + r.combined.stateShare).toBeCloseTo(1, 9);
  });
});
