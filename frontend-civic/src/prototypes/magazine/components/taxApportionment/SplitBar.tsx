import { pct0 } from "./format";

/**
 * The federal-vs-state/local apportionment bar (§7.3/§7.4). Federal = blue,
 * state/local = terracotta, applied via design tokens. Sized variant lets the
 * scaling table reuse it as a slim per-row bar.
 */
export function SplitBar({
  federalShare,
  stateShare,
  size = "lg",
  showLabels = true,
}: {
  federalShare: number;
  stateShare: number;
  size?: "sm" | "lg";
  showLabels?: boolean;
}) {
  const fed = Math.max(0, Math.min(1, federalShare));
  const state = Math.max(0, Math.min(1, stateShare));
  const height = size === "lg" ? "h-10" : "h-3";

  return (
    <div>
      <div
        className={`flex w-full overflow-hidden border border-[var(--border)] ${height}`}
        role="img"
        aria-label={`Federal ${pct0(fed)}, state and local ${pct0(state)}`}
      >
        <div
          className="tax-animate flex items-center justify-start pl-2 transition-[width] duration-300 ease-out"
          style={{ width: `${fed * 100}%`, backgroundColor: "var(--federal)" }}
        >
          {showLabels && fed > 0.12 && (
            <span className="text-xs font-semibold text-white">{pct0(fed)}</span>
          )}
        </div>
        <div
          className="tax-animate flex items-center justify-end pr-2 transition-[width] duration-300 ease-out"
          style={{ width: `${state * 100}%`, backgroundColor: "var(--state)" }}
        >
          {showLabels && state > 0.12 && (
            <span className="text-xs font-semibold text-white">{pct0(state)}</span>
          )}
        </div>
      </div>
      {showLabels && size === "lg" && (
        <div className="mt-2 flex justify-between text-xs font-semibold uppercase tracking-wider">
          <span style={{ color: "var(--federal)" }}>Federal</span>
          <span style={{ color: "var(--state)" }}>State &amp; local</span>
        </div>
      )}
    </div>
  );
}
