import { BudgetFactCard } from "frontend-civic";

// Editorial "Did You Know?" treatment for a both-true budget contradiction:
// two perspective columns split by a "But" hinge, with sourced links and an
// italic explanatory footer.

const taxationFact = {
  id: "bf-taxation-2026-06-18",
  factDate: "2026-06-18",
  category: "Taxation",
  tensionLabel: "Who really carries the income tax?",
  perspectiveA:
    "The top 10% of earners pay roughly 75% of all federal individual income taxes — a far larger share than their share of national income.",
  sourceA: "Tax Foundation, 2024 summary",
  sourceUrlA: "https://taxfoundation.org/data/all/federal/latest-federal-income-tax-data",
  perspectiveB:
    "Yet the top 1%'s effective income tax rate sits near 24%, well below the 37% top statutory bracket, because capital gains and deductions lower the bill.",
  sourceB: "Congressional Budget Office",
  sourceUrlB: "https://www.cbo.gov/topics/taxes",
  explanation:
    "High earners can pay the largest total share of taxes and still face a lower effective rate than the headline bracket suggests — both follow from the same progressive code.",
};

const defenseFact = {
  id: "bf-defense-2026-06-18",
  factDate: "2026-06-18",
  category: "Defense",
  tensionLabel: "The biggest budget that keeps shrinking",
  perspectiveA:
    "At about $850 billion, the U.S. defense budget is the largest in the world — more than the next nine countries combined.",
  sourceA: "SIPRI Military Expenditure Database",
  sourceUrlA: "https://www.sipri.org/databases/milex",
  perspectiveB:
    "As a share of the economy, defense spending has fallen from roughly 9% of GDP in the 1960s to under 3.5% today.",
  sourceB: "Office of Management and Budget",
  sourceUrlB: "https://www.whitehouse.gov/omb/budget/historical-tables/",
  explanation:
    "Both the record dollar total and the shrinking GDP share are true at once — the economy has simply grown faster than the defense budget.",
};

// A fact whose sources have no URLs (plain-text attribution path).
const debtFact = {
  id: "bf-debt-2026-06-18",
  factDate: "2026-06-18",
  category: "Debt",
  tensionLabel: "Record debt, near-record-low cost",
  perspectiveA:
    "Federal debt held by the public has topped $28 trillion, the highest dollar figure in the nation's history.",
  sourceA: "U.S. Treasury, Fiscal Data",
  sourceUrlA: "",
  perspectiveB:
    "Net interest as a share of GDP spent much of the past decade below its 1990s peak, kept down by historically low rates.",
  sourceB: "Government Accountability Office",
  sourceUrlB: "",
  explanation:
    "A larger debt can cost less to carry when interest rates are low — the size of the balance and the price of servicing it move on different clocks.",
};

export const Taxation = () => (
  <div style={{ maxWidth: 820 }}>
    <BudgetFactCard fact={taxationFact as any} />
  </div>
);

export const Defense = () => (
  <div style={{ maxWidth: 820 }}>
    <BudgetFactCard fact={defenseFact as any} />
  </div>
);

export const PlainTextSources = () => (
  <div style={{ maxWidth: 820 }}>
    <BudgetFactCard fact={debtFact as any} />
  </div>
);
