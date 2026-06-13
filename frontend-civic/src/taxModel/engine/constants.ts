import type { FilingStatus, TaxBracket } from "./types";

// Federal tax constants — source of truth (§3). All figures verified 2025-2026.
// The annual refresh is a contained edit here.

export const TAX_YEAR = 2025; // verified 2025-2026

// --- FICA / payroll (§3.3) ---
export const SOCIAL_SECURITY_RATE = 0.062;
/** 2025 SS wage base. 2026 rises to 184,500 — one-line update. */
export const SOCIAL_SECURITY_WAGE_BASE = 176_100;
export const MEDICARE_RATE = 0.0145;
export const ADDITIONAL_MEDICARE_RATE = 0.009;

export const ADDITIONAL_MEDICARE_THRESHOLD: Record<FilingStatus, number> = {
  single: 200_000,
  mfj: 250_000,
};

// --- Standard deduction (§3.2, 2025 OBBBA) ---
export const STANDARD_DEDUCTION: Record<FilingStatus, number> = {
  single: 15_750,
  mfj: 31_500,
};

// --- Ordinary income brackets (§3.1, IRS Rev. Proc. 2024-40) ---
// Marginal: each rate applies only to income within its band. Lower bound → rate.
export const BRACKETS: Record<FilingStatus, TaxBracket[]> = {
  single: [
    { lower: 0, rate: 0.1 },
    { lower: 11_925, rate: 0.12 },
    { lower: 48_475, rate: 0.22 },
    { lower: 103_350, rate: 0.24 },
    { lower: 197_300, rate: 0.32 },
    { lower: 250_525, rate: 0.35 },
    { lower: 626_350, rate: 0.37 },
  ],
  mfj: [
    { lower: 0, rate: 0.1 },
    { lower: 23_850, rate: 0.12 },
    { lower: 96_950, rate: 0.22 },
    { lower: 206_700, rate: 0.24 },
    { lower: 394_600, rate: 0.32 },
    { lower: 501_050, rate: 0.35 },
    { lower: 751_600, rate: 0.37 },
  ],
};

/** Preset incomes for the scaling table (§5 / §7.4). */
export const LADDER_INCOMES = [30_000, 60_000, 100_000, 175_000, 350_000, 750_000];

/**
 * Macro-proof figures (§7.2). Stored constants — federal collections (IRS/USAFacts
 * FY2024), state+local tax revenue (Census 2024), and the ratio between them.
 */
export const MACRO_PROOF = {
  federalCollectionsTrillions: 5.07,
  stateLocalRevenueTrillions: 2.1,
  ratio: 2.4,
};
