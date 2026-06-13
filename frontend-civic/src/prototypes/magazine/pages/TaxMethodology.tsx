import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  ADDITIONAL_MEDICARE_RATE,
  ADDITIONAL_MEDICARE_THRESHOLD,
  BRACKETS,
  compute,
  findStateProfile,
  MEDICARE_RATE,
  SOCIAL_SECURITY_RATE,
  SOCIAL_SECURITY_WAGE_BASE,
  STANDARD_DEDUCTION,
  STATE_PROFILES,
  TAX_YEAR,
} from "@/taxModel/engine";
import { pct, usd } from "../components/taxApportionment/format";

const API_BASE =
  (import.meta.env.VITE_CIVIC_API_URL as string | undefined) ?? "http://localhost:5050/api";

function Code({ children }: { children: ReactNode }) {
  return (
    <pre className="mt-3 overflow-x-auto border border-[var(--border)] bg-[var(--bg-elev)] p-4 text-xs leading-relaxed text-[var(--fg)]">
      <code className="font-mono">{children}</code>
    </pre>
  );
}

function SectionHeading({ n, title }: { n: string; title: string }) {
  return (
    <header className="mb-4 mt-14">
      <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Step {n}
      </p>
      <h2 className="display mt-1 text-2xl md:text-3xl">{title}</h2>
    </header>
  );
}

export default function MagazineTaxMethodology() {
  // Worked example, computed live by the same engine the calculator uses.
  const tx = findStateProfile("TX") ?? STATE_PROFILES[0];
  const example = compute(100_000, "single", tx);

  return (
    <article className="mx-auto max-w-3xl" data-testid="tax-methodology">
      <Link
        to="/briefings/who-gets-your-tax-dollar"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to the calculator
      </Link>

      <header className="mt-6">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          The fine print · tax year {TAX_YEAR}
        </p>
        <h1 className="display mt-2 text-4xl md:text-5xl">How the math works</h1>
        <p className="mt-4 text-lg leading-relaxed text-[var(--fg-soft)]">
          Every number on the calculator is computed by a deterministic, closed-form engine — no AI,
          no rounding tricks, no hidden adjustments. This page lays out every formula and constant so
          you can reproduce any figure by hand or in a spreadsheet. The same engine runs in your
          browser and on the server; both are checked against a fixed table of golden values.
        </p>
      </header>

      {/* 1 · Federal */}
      <SectionHeading n="1" title="Federal tax" />
      <p className="text-base leading-relaxed text-[var(--fg-soft)]">
        Start from gross wage income. Subtract the standard deduction for your filing status to get
        taxable income, then walk the marginal brackets — each rate applies only to the income that
        falls inside its band. Payroll taxes (FICA) are added on top of the wage income, not the
        taxable amount.
      </p>
      <Code>{`taxableIncome  = max(0, income − standardDeduction[filing])
incomeTax      = progressive(taxableIncome, brackets[filing])
socialSecurity = min(income, ${usd(SOCIAL_SECURITY_WAGE_BASE)}) × ${SOCIAL_SECURITY_RATE}
medicare       = income × ${MEDICARE_RATE}
addlMedicare   = max(0, income − addlMedicareThreshold[filing]) × ${ADDITIONAL_MEDICARE_RATE}
fica           = socialSecurity + medicare + addlMedicare
federalTotal   = incomeTax + fica`}</Code>
      <p className="mt-3 text-sm text-[var(--muted)]">
        Only the employee share of FICA is shown. Employers match 6.2% + 1.45% = 7.65% invisibly;
        counting it would raise the federal share further.
      </p>

      <h3 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
        Standard deduction ({TAX_YEAR})
      </h3>
      <table className="mt-2 w-full max-w-sm border-collapse text-sm">
        <tbody>
          <tr className="border-b border-[var(--border)]">
            <td className="py-1.5">Single</td>
            <td className="py-1.5 text-right font-semibold tabular-nums">{usd(STANDARD_DEDUCTION.single)}</td>
          </tr>
          <tr className="border-b border-[var(--border)]">
            <td className="py-1.5">Married filing jointly</td>
            <td className="py-1.5 text-right font-semibold tabular-nums">{usd(STANDARD_DEDUCTION.mfj)}</td>
          </tr>
        </tbody>
      </table>

      <h3 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
        Income tax brackets ({TAX_YEAR}, marginal)
      </h3>
      <div className="mt-2 grid gap-6 sm:grid-cols-2">
        {(["single", "mfj"] as const).map((filing) => (
          <div key={filing}>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wider">
              {filing === "single" ? "Single" : "Married filing jointly"}
            </p>
            <table className="w-full border-collapse text-sm">
              <thead>
                <tr className="border-b border-[var(--fg)] text-left text-xs uppercase tracking-wider text-[var(--muted)]">
                  <th className="py-1 font-semibold">Rate</th>
                  <th className="py-1 text-right font-semibold">On income over</th>
                </tr>
              </thead>
              <tbody>
                {BRACKETS[filing].map((b) => (
                  <tr key={b.lower} className="border-b border-[var(--border)]">
                    <td className="py-1.5 font-semibold tabular-nums">{pct(b.rate)}</td>
                    <td className="py-1.5 text-right tabular-nums">{usd(b.lower)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>

      <h3 className="mt-8 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
        FICA / payroll ({TAX_YEAR})
      </h3>
      <table className="mt-2 w-full border-collapse text-sm">
        <tbody>
          <tr className="border-b border-[var(--border)]">
            <td className="py-1.5">Social Security (OASDI)</td>
            <td className="py-1.5 text-right tabular-nums">
              {pct(SOCIAL_SECURITY_RATE)} on wages up to {usd(SOCIAL_SECURITY_WAGE_BASE)}
            </td>
          </tr>
          <tr className="border-b border-[var(--border)]">
            <td className="py-1.5">Medicare (HI)</td>
            <td className="py-1.5 text-right tabular-nums">{pct(MEDICARE_RATE)} on all wages (no cap)</td>
          </tr>
          <tr className="border-b border-[var(--border)]">
            <td className="py-1.5">Additional Medicare</td>
            <td className="py-1.5 text-right tabular-nums">
              {pct(ADDITIONAL_MEDICARE_RATE)} over {usd(ADDITIONAL_MEDICARE_THRESHOLD.single)} (single) /{" "}
              {usd(ADDITIONAL_MEDICARE_THRESHOLD.mfj)} (MFJ)
            </td>
          </tr>
        </tbody>
      </table>

      {/* 2 · State */}
      <SectionHeading n="2" title="State & local tax" />
      <p className="text-base leading-relaxed text-[var(--fg-soft)]">
        Each state carries three rules — an income rule, a sales rule, and a property rule. Sales and
        property are modeled from income using two transparent assumptions (below).
      </p>
      <Code>{`stateIncomeTax = byRule(income, state.income)
                 // none   → 0
                 // flat   → max(0, income − stdDed) × rate
                 // graduated → progressive(income − stdDed, brackets)
salesTax       = income × consumptionShare(0.40) × state.salesRate
propertyTax    = income × state.homeMultiple × state.propRate
stateTotal     = stateIncomeTax + salesTax + propertyTax`}</Code>

      {/* 3 · Combined */}
      <SectionHeading n="3" title="The apportionment split" />
      <Code>{`combinedTotal = federalTotal + stateTotal
federalShare  = federalTotal / combinedTotal
stateShare    = stateTotal   / combinedTotal`}</Code>

      {/* 4 · Worked example */}
      <SectionHeading n="4" title="Worked example — $100,000, single, Texas" />
      <p className="text-base leading-relaxed text-[var(--fg-soft)]">
        Reproduce these exactly. Texas has no income tax, so the state side is sales + property only.
      </p>
      <table className="mt-3 w-full border-collapse text-sm">
        <tbody>
          {[
            ["Taxable income (100,000 − " + usd(STANDARD_DEDUCTION.single) + ")", usd(100_000 - STANDARD_DEDUCTION.single)],
            ["Federal income tax", usd(example.federal.incomeTax)],
            ["Social Security (6.2% × 100,000)", usd(example.federal.socialSecurity)],
            ["Medicare (1.45% × 100,000)", usd(example.federal.medicare)],
            ["FICA subtotal", usd(example.federal.fica)],
            ["Federal total", usd(example.federal.total)],
            ["State income tax (none in TX)", usd(example.stateLocal.incomeTax)],
            ["Sales tax (100,000 × 0.40 × " + pct(tx.salesRate) + ")", usd(example.stateLocal.salesTax)],
            ["Property tax (100,000 × " + tx.homeMultiple + " × " + pct(tx.propRate) + ")", usd(example.stateLocal.propertyTax)],
            ["State & local total", usd(example.stateLocal.total)],
            ["Combined total", usd(example.combined.total)],
            ["Federal share", pct(example.combined.federalShare)],
            ["State & local share", pct(example.combined.stateShare)],
          ].map(([label, value], i) => (
            <tr key={i} className="border-b border-[var(--border)]">
              <td className="py-1.5 text-[var(--fg-soft)]">{label}</td>
              <td className="py-1.5 text-right font-semibold tabular-nums">{value}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* 5 · State parameters */}
      <SectionHeading n="5" title="Per-state parameters" />
      <p className="text-base leading-relaxed text-[var(--fg-soft)]">
        The income/sales/property rules for all 50 states are served as machine-readable JSON, so you
        can pull the exact parameters used for any state and feed them back into the formulas above:
      </p>
      <Code>{`GET ${API_BASE}/tax-model/states
GET ${API_BASE}/tax-model/compute?income=100000&filing=single&state=TX
GET ${API_BASE}/tax-model/ladder?filing=single&state=CA`}</Code>
      <p className="mt-3 text-sm text-[var(--muted)]">
        The 8 spotlight states (CA, NY, TX, FL, WA, CO, PA, IL) carry fully verified spec figures. The
        other 42 are verified against Tax Foundation 2025 data (income brackets &amp; standard
        deductions; combined state+local sales rates, midyear 2025; effective owner-occupied property
        rates). Single-filer income schedules are simplified to top-line brackets.
      </p>

      {/* 6 · Assumptions */}
      <SectionHeading n="6" title="The two modeling assumptions" />
      <ul className="space-y-3 text-base leading-relaxed text-[var(--fg-soft)]">
        <li>
          <strong className="text-[var(--fg)]">Consumption share = 40%.</strong> Sales tax assumes a
          household spends a taxable 40% of income. Real spending varies with income, and
          groceries, services, and rent are often exempt.
        </li>
        <li>
          <strong className="text-[var(--fg)]">Home value = a multiple of income.</strong> Property
          tax imputes an owned home worth a state-specific multiple of income. Renters pay
          indirectly through rent, so this overstates the renter burden. The multiple is a modeled
          estimate for every state.
        </li>
        <li>
          <strong className="text-[var(--fg)]">Wages only.</strong> All income is treated as wages.
          Capital gains face different federal rates, escape FICA, and are taxed differently by
          states. Local (city) income taxes, credits, and exemptions are out of scope.
        </li>
      </ul>

      {/* 7 · Sources */}
      <SectionHeading n="7" title="Sources" />
      <p className="text-sm leading-relaxed text-[var(--muted)]">
        Federal brackets &amp; standard deduction: IRS Rev. Proc. 2024-40; OBBBA (2025). FICA: Social
        Security Administration ({TAX_YEAR} wage base {usd(SOCIAL_SECURITY_WAGE_BASE)}) and Medicare
        thresholds. Federal vs. state/local revenue: IRS / USAFacts FY2024 and U.S. Census Bureau
        (2024). State income, sales &amp; property rates: Tax Foundation 2025.
      </p>

      <div className="mt-12 border-t border-[var(--border)] pt-6">
        <Link
          to="/briefings/who-gets-your-tax-dollar"
          className="text-sm font-semibold text-[var(--accent)] hover:underline"
        >
          ← Back to the calculator
        </Link>
      </div>
    </article>
  );
}
