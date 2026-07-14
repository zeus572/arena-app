import { useMemo, useState } from "react";

/**
 * The bill sits in the center; each value axis radiates outward as a spoke.
 * The bill's lean on an axis is a marker whose distance from center encodes how
 * strongly the bill pushes (|score|); the direction (low vs high label) is named
 * on the spoke. When the viewer has a compass, a second (hollow) marker shows
 * where they sit, and each spoke is tinted by alignment (aligned / mixed /
 * tension). No chart library — hand-rolled SVG, following the project's
 * Sparkline / conic-ring convention.
 */
export type RadialAxis = {
  axisKey: string;
  axisName: string;
  lowLabel: string;
  highLabel: string;
  billScore: number;
  billConfidence: number;
  rationale: string;
  userScore: number | null;
  alignment: "aligned" | "mixed" | "tension" | null;
};

const SIZE = 380;
const C = SIZE / 2;
const MAX_R = 132;

const ALIGN_COLOR: Record<string, string> = {
  aligned: "#059669", // emerald-600
  tension: "var(--state)",
  mixed: "var(--muted)",
};

function spokeColor(a: RadialAxis): string {
  if (a.alignment) return ALIGN_COLOR[a.alignment] ?? "var(--accent)";
  return "var(--accent)";
}

function pointAt(angleDeg: number, radius: number): { x: number; y: number } {
  const rad = (angleDeg * Math.PI) / 180;
  return { x: C + radius * Math.cos(rad), y: C + radius * Math.sin(rad) };
}

export default function CompassRadial({
  axes,
  showUser,
  reducedMotion = false,
}: {
  axes: RadialAxis[];
  showUser: boolean;
  reducedMotion?: boolean;
}) {
  const [active, setActive] = useState<string | null>(null);

  const layout = useMemo(() => {
    const n = Math.max(axes.length, 1);
    return axes.map((a, i) => {
      const angle = -90 + (360 * i) / n;
      const rim = pointAt(angle, MAX_R);
      const label = pointAt(angle, MAX_R + 18);
      const billPt = pointAt(angle, Math.abs(a.billScore) * MAX_R);
      const userPt = a.userScore == null ? null : pointAt(angle, Math.abs(a.userScore) * MAX_R);
      // The direction the bill leans, named for the reader.
      const leanLabel = a.billScore >= 0 ? a.highLabel : a.lowLabel;
      const anchor: "start" | "middle" | "end" =
        Math.abs(Math.cos((angle * Math.PI) / 180)) < 0.35
          ? "middle"
          : Math.cos((angle * Math.PI) / 180) > 0
            ? "start"
            : "end";
      return { a, angle, rim, label, billPt, userPt, leanLabel, anchor };
    });
  }, [axes]);

  const billPolygon = layout.map((l) => `${l.billPt.x},${l.billPt.y}`).join(" ");
  const userPolygon =
    showUser && layout.every((l) => l.userPt)
      ? layout.map((l) => `${l.userPt!.x},${l.userPt!.y}`).join(" ")
      : null;

  const activeAxis = active ? axes.find((a) => a.axisKey === active) ?? null : null;

  if (axes.length === 0) {
    return (
      <p className="py-10 text-center text-sm text-[var(--muted)]" data-testid="radial-empty">
        This bill hasn't been positioned on the value compass yet.
      </p>
    );
  }

  return (
    <div className="flex flex-col items-center" data-testid="compass-radial">
      <svg
        viewBox={`0 0 ${SIZE} ${SIZE}`}
        className="w-full max-w-[420px]"
        role="img"
        aria-label="The bill's position across value axes"
      >
        {/* Reference rings */}
        {[0.5, 1].map((f) => (
          <circle
            key={f}
            cx={C}
            cy={C}
            r={MAX_R * f}
            fill="none"
            stroke="var(--border)"
            strokeWidth={1}
            strokeDasharray={f === 1 ? undefined : "3 4"}
          />
        ))}

        {/* Spokes + labels */}
        {layout.map((l) => (
          <g key={l.a.axisKey}>
            <line
              x1={C}
              y1={C}
              x2={l.rim.x}
              y2={l.rim.y}
              stroke="var(--border)"
              strokeWidth={1}
            />
            <text
              x={l.label.x}
              y={l.label.y}
              textAnchor={l.anchor}
              dominantBaseline="middle"
              className="fill-[var(--fg-soft)]"
              style={{ fontSize: 10, fontWeight: 600 }}
            >
              {l.a.axisName}
            </text>
          </g>
        ))}

        {/* User shape (underlay) */}
        {userPolygon && (
          <polygon
            points={userPolygon}
            fill="var(--fg)"
            fillOpacity={0.06}
            stroke="var(--fg-soft)"
            strokeWidth={1.5}
            strokeDasharray="4 3"
          />
        )}

        {/* Bill shape */}
        <polygon
          points={billPolygon}
          fill="var(--accent)"
          fillOpacity={0.1}
          stroke="var(--accent)"
          strokeWidth={1.5}
        />

        {/* Per-axis colored lean segments + markers */}
        {layout.map((l) => {
          const color = spokeColor(l.a);
          const isActive = active === l.a.axisKey;
          return (
            <g
              key={`m-${l.a.axisKey}`}
              onMouseEnter={() => setActive(l.a.axisKey)}
              onMouseLeave={() => setActive((k) => (k === l.a.axisKey ? null : k))}
              style={{ cursor: "pointer" }}
            >
              <line
                x1={C}
                y1={C}
                x2={l.billPt.x}
                y2={l.billPt.y}
                stroke={color}
                strokeWidth={isActive ? 4 : 2.5}
                strokeOpacity={0.35 + 0.55 * Math.max(0, Math.min(1, l.a.billConfidence))}
                strokeLinecap="round"
              />
              {l.userPt && (
                <circle
                  cx={l.userPt.x}
                  cy={l.userPt.y}
                  r={4}
                  fill="var(--bg)"
                  stroke="var(--fg)"
                  strokeWidth={2}
                />
              )}
              <circle
                cx={l.billPt.x}
                cy={l.billPt.y}
                r={isActive ? 6 : 4.5}
                fill={color}
                stroke="var(--bg)"
                strokeWidth={1.5}
                className={reducedMotion ? undefined : "transition-all"}
              >
                <title>{`${l.a.axisName}: bill leans ${l.leanLabel}`}</title>
              </circle>
            </g>
          );
        })}

        {/* Center hub */}
        <circle cx={C} cy={C} r={5} fill="var(--fg)" />
      </svg>

      {/* Legend */}
      <div className="mt-3 flex flex-wrap items-center justify-center gap-x-4 gap-y-1 text-[11px] text-[var(--muted)]">
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-full bg-[var(--accent)]" /> Bill
        </span>
        {showUser && (
          <>
            <span className="flex items-center gap-1.5">
              <span className="inline-block h-2.5 w-2.5 rounded-full border-2 border-[var(--fg)] bg-[var(--bg)]" /> You
            </span>
            <span className="flex items-center gap-1.5">
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: ALIGN_COLOR.aligned }} /> Aligned
            </span>
            <span className="flex items-center gap-1.5">
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: "var(--state)" }} /> Tension
            </span>
          </>
        )}
      </div>

      {/* Active-axis detail */}
      <div className="mt-3 min-h-[3.5rem] w-full max-w-[420px] text-center" aria-live="polite">
        {activeAxis ? (
          <div data-testid="radial-active">
            <p className="text-sm font-semibold text-[var(--fg)]">
              {activeAxis.axisName}
              <span className="ml-2 text-xs font-normal text-[var(--muted)]">
                bill leans {activeAxis.billScore >= 0 ? activeAxis.highLabel : activeAxis.lowLabel}
              </span>
            </p>
            <p className="mt-1 text-xs leading-relaxed text-[var(--fg-soft)]">{activeAxis.rationale}</p>
          </div>
        ) : (
          <p className="text-xs text-[var(--muted)]">Hover a spoke to see why the bill lands there.</p>
        )}
      </div>
    </div>
  );
}
