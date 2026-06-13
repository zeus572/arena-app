import type { StateProfile } from "@/taxModel/engine";

/**
 * Compact state picker (§7.3): a single-row native dropdown listing all 50 states,
 * so the calculator and scaling table stay above the fold. Native <select> gives
 * type-ahead, keyboard nav, and a mobile-friendly wheel for free. The selected
 * state's income-tax summary is shown inline.
 */
export function StatePicker({
  states,
  selected,
  onSelect,
}: {
  states: StateProfile[];
  selected: string;
  onSelect: (code: string) => void;
}) {
  const sorted = [...states].sort((a, b) => a.name.localeCompare(b.name));
  const current = states.find((s) => s.code === selected);

  return (
    <div>
      <label
        htmlFor="tax-state-select"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]"
      >
        State
      </label>
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <div className="relative">
          <select
            id="tax-state-select"
            value={selected}
            onChange={(e) => onSelect(e.target.value)}
            data-testid="tax-state-select"
            className="appearance-none border border-[var(--border)] bg-[var(--bg-elev)] py-2 pl-3 pr-9 text-base font-semibold text-[var(--fg)] transition hover:border-[var(--fg)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
          >
            {sorted.map((s) => (
              <option key={s.code} value={s.code}>
                {s.name}
              </option>
            ))}
          </select>
          {/* chevron */}
          <span
            aria-hidden
            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-[var(--muted)]"
          >
            ▾
          </span>
        </div>
        {current && (
          <span className="text-sm text-[var(--fg-soft)]" data-testid="tax-state-summary">
            <span aria-hidden>{current.glyph} </span>
            Income tax: <span className="font-semibold">{current.incomeSummary}</span>
          </span>
        )}
      </div>
    </div>
  );
}
