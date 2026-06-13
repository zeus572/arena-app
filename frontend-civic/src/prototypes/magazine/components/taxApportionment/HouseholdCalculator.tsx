import type { FilingStatus, StateProfile, TaxComputeResult } from "@/taxModel/engine";
import { SplitBar } from "./SplitBar";
import { StatePicker } from "./StatePicker";
import { pct, usd } from "./format";

const INCOME_MIN = 20_000;
const INCOME_MAX = 800_000;
const INCOME_STEP = 5_000;

type LineItem = { label: string; amount: number; note: string; strong?: boolean };

function LineColumn({
  title,
  color,
  total,
  effectiveRate,
  items,
}: {
  title: string;
  color: string;
  total: number;
  effectiveRate: number;
  items: LineItem[];
}) {
  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5">
      <div className="flex items-baseline justify-between border-b border-[var(--border)] pb-3">
        <h3 className="display text-lg font-semibold" style={{ color }}>
          {title}
        </h3>
        <div className="text-right">
          <div className="text-xl font-semibold" style={{ color }}>
            {usd(total)}
          </div>
          <div className="text-xs uppercase tracking-wider text-[var(--muted)]">
            {pct(effectiveRate)} of income
          </div>
        </div>
      </div>
      <ul className="mt-3 space-y-3">
        {items.map((it) => (
          <li key={it.label} className={it.strong ? "border-t border-[var(--border)] pt-3" : ""}>
            <div className="flex items-baseline justify-between gap-3">
              <span className={`text-sm ${it.strong ? "font-semibold" : ""}`}>{it.label}</span>
              <span className={`tabular-nums text-sm ${it.strong ? "font-semibold" : ""}`}>
                {usd(it.amount)}
              </span>
            </div>
            <p className="mt-0.5 text-xs leading-snug text-[var(--muted)]">{it.note}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function HouseholdCalculator({
  income,
  filing,
  stateCode,
  profile,
  result,
  states,
  onIncomeChange,
  onFilingChange,
  onStateChange,
}: {
  income: number;
  filing: FilingStatus;
  stateCode: string;
  profile: StateProfile;
  result: TaxComputeResult;
  states: StateProfile[];
  onIncomeChange: (income: number) => void;
  onFilingChange: (filing: FilingStatus) => void;
  onStateChange: (code: string) => void;
}) {
  const { federal, stateLocal, combined } = result;

  const federalItems: LineItem[] = [
    {
      label: "Federal income tax",
      amount: federal.incomeTax,
      note: "Progressive brackets on income after the standard deduction.",
    },
    {
      label: "Social Security",
      amount: federal.socialSecurity,
      note: "6.2% on wages up to the $176,100 cap.",
    },
    { label: "Medicare", amount: federal.medicare, note: "1.45% on all wages, no cap." },
    ...(federal.addlMedicare > 0
      ? [
          {
            label: "Additional Medicare",
            amount: federal.addlMedicare,
            note: `0.9% on wages over the ${filing === "mfj" ? "$250,000" : "$200,000"} threshold.`,
          },
        ]
      : []),
    {
      label: "Federal total",
      amount: federal.total,
      note: "Income tax plus your half of FICA. Employer matches 7.65% invisibly.",
      strong: true,
    },
  ];

  const stateItems: LineItem[] = [
    {
      label: "State income tax",
      amount: stateLocal.incomeTax,
      note:
        profile.income.type === "none"
          ? "This state has no wage income tax."
          : "Applied to income under this state's schedule.",
    },
    {
      label: "Sales tax",
      amount: stateLocal.salesTax,
      note: "Estimated: a taxable 40% of income at the combined rate.",
    },
    {
      label: "Property tax",
      amount: stateLocal.propertyTax,
      note: "Imputed: a home worth a multiple of income at the effective rate.",
    },
    {
      label: "State & local total",
      amount: stateLocal.total,
      note: "Income, sales, and property combined.",
      strong: true,
    },
  ];

  return (
    <div data-testid="tax-calculator">
      {/* Controls */}
      <div className="grid gap-6 md:grid-cols-[1fr_auto] md:items-end">
        <div>
          <label
            htmlFor="tax-income"
            className="flex items-baseline justify-between text-xs font-semibold uppercase tracking-wider text-[var(--muted)]"
          >
            <span>Household income</span>
            <span className="display text-2xl text-[var(--fg)]">{usd(income)}</span>
          </label>
          <input
            id="tax-income"
            type="range"
            min={INCOME_MIN}
            max={INCOME_MAX}
            step={INCOME_STEP}
            value={income}
            onChange={(e) => onIncomeChange(Number(e.target.value))}
            className="mt-3 w-full accent-[var(--accent)]"
            data-testid="tax-income-slider"
            aria-valuetext={usd(income)}
          />
          <div className="mt-1 flex justify-between text-xs text-[var(--muted)]">
            <span>{usd(INCOME_MIN)}</span>
            <span>{usd(INCOME_MAX)}</span>
          </div>
        </div>

        <fieldset>
          <legend className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Filing
          </legend>
          <div className="mt-2 inline-flex border border-[var(--border)]" role="radiogroup" aria-label="Filing status">
            {(["single", "mfj"] as FilingStatus[]).map((f) => (
              <button
                key={f}
                type="button"
                role="radio"
                aria-checked={filing === f}
                onClick={() => onFilingChange(f)}
                data-testid={`tax-filing-${f}`}
                className={`px-4 py-2 text-sm font-semibold transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-[var(--accent)] ${
                  filing === f
                    ? "bg-[var(--fg)] text-[var(--bg)]"
                    : "text-[var(--muted)] hover:text-[var(--fg)]"
                }`}
              >
                {f === "single" ? "Single" : "Married / joint"}
              </button>
            ))}
          </div>
        </fieldset>
      </div>

      {/* State picker */}
      <div className="mt-6">
        <StatePicker states={states} selected={stateCode} onSelect={onStateChange} />
      </div>

      {/* Split bar */}
      <div className="mt-8">
        <SplitBar federalShare={combined.federalShare} stateShare={combined.stateShare} />
        <p className="mt-3 text-sm text-[var(--fg-soft)]">
          On {usd(income)} in {profile.name}, about{" "}
          <strong style={{ color: "var(--federal)" }}>{pct(combined.federalShare)}</strong> of your{" "}
          {usd(combined.total)} tax bill flows to the federal government and{" "}
          <strong style={{ color: "var(--state)" }}>{pct(combined.stateShare)}</strong> stays state
          and local — a combined effective rate of {pct(combined.effectiveRate)}.
        </p>
      </div>

      {/* Breakdown columns */}
      <div className="mt-8 grid gap-5 md:grid-cols-2">
        <LineColumn
          title="To the Federal Government"
          color="var(--federal)"
          total={federal.total}
          effectiveRate={federal.effectiveRate}
          items={federalItems}
        />
        <LineColumn
          title={`Stays in ${profile.name}`}
          color="var(--state)"
          total={stateLocal.total}
          effectiveRate={stateLocal.effectiveRate}
          items={stateItems}
        />
      </div>
    </div>
  );
}
