import type { FilingStatus, StateProfile } from "@/taxModel/engine";
import { combine, computeFederal, computeState, LADDER_INCOMES } from "@/taxModel/engine";
import { SplitBar } from "./SplitBar";
import { pct, usd } from "./format";

/**
 * Scaling table (§7.4): federal vs. state vs. federal-share across the six preset
 * incomes for the selected state/filing, each with a mini split bar. The row nearest
 * the calculator's current income is highlighted.
 */
export function ScalingTable({
  filing,
  profile,
  currentIncome,
}: {
  filing: FilingStatus;
  profile: StateProfile;
  currentIncome: number;
}) {
  const highlighted = LADDER_INCOMES.reduce((best, income) =>
    Math.abs(income - currentIncome) < Math.abs(best - currentIncome) ? income : best,
  );

  const rows = LADDER_INCOMES.map((income) => {
    const federal = computeFederal(income, filing);
    const stateLocal = computeState(income, profile);
    const combined = combine(income, federal, stateLocal);
    return { income, federal, stateLocal, combined };
  });

  return (
    <div data-testid="tax-scaling-table">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b-2 border-[var(--fg)] text-left text-xs uppercase tracking-wider text-[var(--muted)]">
              <th scope="col" className="py-2 pr-3 font-semibold">Income</th>
              <th scope="col" className="py-2 pr-3 text-right font-semibold" style={{ color: "var(--federal)" }}>Federal</th>
              <th scope="col" className="py-2 pr-3 text-right font-semibold" style={{ color: "var(--state)" }}>State &amp; local</th>
              <th scope="col" className="py-2 pr-3 text-right font-semibold">Fed. share</th>
              <th scope="col" className="hidden py-2 font-semibold sm:table-cell">Split</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => {
              const isCurrent = r.income === highlighted;
              return (
                <tr
                  key={r.income}
                  data-testid={`tax-ladder-row-${r.income}`}
                  aria-current={isCurrent ? "true" : undefined}
                  className={`border-b border-[var(--border)] ${
                    isCurrent ? "bg-[var(--federal-soft)]" : ""
                  }`}
                >
                  <th scope="row" className="py-2.5 pr-3 text-left font-semibold tabular-nums">
                    {usd(r.income)}
                    {isCurrent && (
                      <span className="ml-2 text-[10px] font-semibold uppercase tracking-wider text-[var(--accent)]">
                        you
                      </span>
                    )}
                  </th>
                  <td className="py-2.5 pr-3 text-right tabular-nums">{usd(r.federal.total)}</td>
                  <td className="py-2.5 pr-3 text-right tabular-nums">{usd(r.stateLocal.total)}</td>
                  <td className="py-2.5 pr-3 text-right font-semibold tabular-nums">
                    {pct(r.combined.federalShare)}
                  </td>
                  <td className="hidden w-40 py-2.5 sm:table-cell">
                    <SplitBar
                      federalShare={r.combined.federalShare}
                      stateShare={r.combined.stateShare}
                      size="sm"
                      showLabels={false}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <p className="mt-4 text-sm leading-relaxed text-[var(--fg-soft)]">
        The federal share rises with income because the federal income tax is steeply
        progressive, while sales and property taxes are flat-to-regressive. At low incomes,
        state and local can be the larger share of the bill.
      </p>
    </div>
  );
}
