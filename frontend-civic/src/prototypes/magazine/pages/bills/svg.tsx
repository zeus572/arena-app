import { radarPoints } from "./model";

const HEX_OUTLINE = "50,4 90,27 90,73 50,96 10,73 10,27";
const HEX_INNER = "50,27 70,38.5 70,61.5 50,73 30,61.5 30,38.5";

/**
 * A compact 6-axis compass hexagon: the bill polygon (accent), an optional user
 * polygon overlay (muted), and reference frame. Purely presentational SVG with
 * no labels — safe to render at any size, unlike the full `CompassRadial`.
 */
export function MiniCompass({
  radar,
  userRadar = null,
  size = 34,
  billStroke = 3,
  showFrame = true,
}: {
  radar: string;
  userRadar?: string | null;
  size?: number;
  billStroke?: number;
  showFrame?: boolean;
}) {
  return (
    <svg viewBox="0 0 100 100" width={size} height={size} style={{ flex: "none" }} aria-hidden>
      {showFrame && <polygon points={HEX_OUTLINE} fill="none" stroke="var(--border)" />}
      {showFrame && <polygon points={HEX_INNER} fill="none" stroke="var(--border)" strokeDasharray="2 3" />}
      {userRadar && <polygon points={userRadar} fill="none" stroke="var(--muted)" strokeWidth={1.5} />}
      <polygon
        points={radar}
        fill="color-mix(in oklab, var(--accent) 18%, transparent)"
        stroke="var(--accent)"
        strokeWidth={billStroke}
      />
    </svg>
  );
}

/** Build the six-axis polygon points for a raw score vector. */
export function radarFor(scores: number[]): string {
  return radarPoints(scores);
}

const RING_R = 44;
const RING_CIRC = 2 * Math.PI * RING_R;

/**
 * Circular alignment gauge. When `pct` is null (no compass) the value ring is
 * hidden and the centre shows an em-dash, per the design's cold-start rule.
 */
export function AlignmentRing({
  pct,
  size = 104,
  label = "Aligned",
}: {
  pct: number | null;
  size?: number;
  label?: string;
}) {
  const dash = pct == null ? 0 : (pct / 100) * RING_CIRC;
  return (
    <div style={{ position: "relative", width: size, height: size, flex: "none" }}>
      <svg viewBox="0 0 100 100" width={size} height={size} style={{ transform: "rotate(-90deg)" }} aria-hidden>
        <circle cx="50" cy="50" r={RING_R} fill="none" stroke="var(--border)" strokeWidth="7" />
        {pct != null && (
          <circle
            cx="50"
            cy="50"
            r={RING_R}
            fill="none"
            stroke="var(--accent)"
            strokeWidth="7"
            strokeDasharray={`${dash.toFixed(1)} ${RING_CIRC.toFixed(1)}`}
          />
        )}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="display text-[27px] leading-none text-[var(--fg)]">
          {pct == null ? "—" : `${pct}%`}
        </span>
        <span className="mt-0.5 text-[8px] font-bold uppercase tracking-[0.12em] text-[var(--muted)]">{label}</span>
      </div>
    </div>
  );
}
