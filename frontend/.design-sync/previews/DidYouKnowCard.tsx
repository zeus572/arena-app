import { DidYouKnowCard } from "frontend";

// A real federal-budget tension: both perspectives are sourced and true, but
// they pull in opposite directions — the "BUT" seam sits on the divider.
const deficitVsInvestment = {
  id: "fact_1",
  factDate: "2026-02-14",
  category: "Federal Budget",
  tensionLabel: "Deficit vs. Investment",
  perspectiveA:
    "The federal deficit topped $1.8 trillion last year. Every dollar of new spending adds to a debt whose interest now costs more to service than the entire defense budget.",
  sourceA: "CBO Budget Outlook 2026",
  sourceUrlA: "https://www.cbo.gov/",
  perspectiveB:
    "Underfunding roads, grids, and research is its own debt. Deferred maintenance and lost competitiveness compound silently — and never show up on the deficit line.",
  sourceB: "GAO Infrastructure Report",
  sourceUrlB: "https://www.gao.gov/",
  explanation:
    "Both framings use real numbers; they disagree on which kind of debt — financial or structural — is the one worth worrying about.",
};

export const BudgetTension = () => <DidYouKnowCard fact={deficitVsInvestment} />;
