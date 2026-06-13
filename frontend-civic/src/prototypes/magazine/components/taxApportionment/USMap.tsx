import type { FilingStatus, StateProfile } from "@/taxModel/engine";
import { combine, computeFederal, computeState } from "@/taxModel/engine";
import { pct0 } from "./format";

// Tile cartogram: each state sits in an approximate geographic grid cell (8 rows ×
// 11 cols). A recognizable, accessible alternative to fragile SVG path data — every
// state is a real button. [row, col], both 1-indexed.
const GRID: Record<string, [number, number]> = {
  AK: [1, 1], ME: [1, 11],
  VT: [2, 10], NH: [2, 11],
  WA: [3, 1], ID: [3, 2], MT: [3, 3], ND: [3, 4], MN: [3, 5], IL: [3, 6], WI: [3, 7], MI: [3, 8], NY: [3, 9], RI: [3, 10], MA: [3, 11],
  OR: [4, 1], NV: [4, 2], WY: [4, 3], SD: [4, 4], IA: [4, 5], IN: [4, 6], OH: [4, 7], PA: [4, 8], NJ: [4, 9], CT: [4, 10],
  CA: [5, 1], UT: [5, 2], CO: [5, 3], NE: [5, 4], MO: [5, 5], KY: [5, 6], WV: [5, 7], VA: [5, 8], MD: [5, 9], DE: [5, 10],
  AZ: [6, 1], NM: [6, 2], KS: [6, 3], AR: [6, 4], TN: [6, 5], NC: [6, 6], SC: [6, 7],
  OK: [7, 3], LA: [7, 4], MS: [7, 5], AL: [7, 6], GA: [7, 7],
  HI: [8, 1], TX: [8, 3], FL: [8, 7],
};

/**
 * Clickable US tile map (§7.3 state picker). Each tile is tinted between terracotta
 * (state/local-heavy) and federal blue by the federal share at the current income, so
 * the map itself previews the apportionment gradient. The selected state is ringed.
 */
export function USMap({
  states,
  income,
  filing,
  selected,
  onSelect,
}: {
  states: StateProfile[];
  income: number;
  filing: FilingStatus;
  selected: string;
  onSelect: (code: string) => void;
}) {
  const byCode = new Map(states.map((s) => [s.code, s]));

  return (
    <div>
      <div
        role="radiogroup"
        aria-label="Select a state"
        className="grid gap-1"
        style={{ gridTemplateColumns: "repeat(11, minmax(0, 1fr))" }}
        data-testid="tax-state-map"
      >
        {Object.entries(GRID).map(([code, [row, col]]) => {
          const profile = byCode.get(code);
          if (!profile) return null;

          const c = combine(
            income,
            computeFederal(income, filing),
            computeState(income, profile),
          );
          const fedPct = Math.round(c.federalShare * 100);
          const isSelected = selected === code;

          return (
            <button
              key={code}
              type="button"
              role="radio"
              aria-checked={isSelected}
              aria-label={`${profile.name}: federal ${pct0(c.federalShare)}, state and local ${pct0(c.stateShare)}`}
              title={`${profile.name} — ${pct0(c.federalShare)} federal`}
              onClick={() => onSelect(code)}
              data-testid={`tax-map-${code}`}
              style={{
                gridRow: row,
                gridColumn: col,
                aspectRatio: "1 / 1",
                backgroundColor: `color-mix(in srgb, var(--federal) ${fedPct}%, var(--state))`,
                outline: isSelected ? "3px solid var(--fg)" : undefined,
                outlineOffset: isSelected ? "1px" : undefined,
              }}
              className="flex items-center justify-center text-[10px] font-bold text-white/95 transition hover:brightness-110 focus-visible:outline focus-visible:outline-2 focus-visible:outline-[var(--accent)] md:text-xs"
            >
              {code}
            </button>
          );
        })}
      </div>

      {/* Legend */}
      <div className="mt-3 flex items-center justify-center gap-2 text-xs text-[var(--muted)]">
        <span style={{ color: "var(--state)" }}>More state &amp; local</span>
        <span
          aria-hidden
          className="h-2 w-24 rounded-full"
          style={{ background: "linear-gradient(to right, var(--state), var(--federal))" }}
        />
        <span style={{ color: "var(--federal)" }}>More federal</span>
      </div>
    </div>
  );
}
