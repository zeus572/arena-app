import type { BillSummary } from "@/api/bills";
import type { Profile } from "@/api/profile";

/**
 * The six canonical compass axes the Explore page plots. All six exist in the
 * backend axis catalog (which actually holds 15); these are the subset the
 * redesign visualises. Order is FIXED — the hexagon vertices depend on it, so
 * axis `i` always maps to the same vertex across every view.
 *
 * A bill is scored only on the axes it implicates, so many bills have fewer
 * than six positions; a missing axis is treated as centre (score 0).
 */
export const AXES = [
  { key: "govt-role", name: "Government role", low: "Minimal state", high: "Active public builder" },
  { key: "change-speed", name: "Change speed", low: "Gradualist", high: "Transformational" },
  { key: "economic-fairness", name: "Economic fairness", low: "Market outcome", high: "Redistributive correction" },
  { key: "authority", name: "Authority", low: "Decentralized", high: "Centralized" },
  { key: "risk", name: "Risk", low: "Precautionary", high: "Innovation-tolerant" },
  { key: "time-horizon", name: "Time horizon", low: "Present relief", high: "Future resilience" },
] as const;

export const AXIS_COUNT = AXES.length;

/**
 * Stage pipeline. The design's five stages assumed a "Floor Vote" stage that has
 * no matching status in the data; this maps the real `BillStatus` enum instead.
 * `Failed`/`Unknown` are handled off the pipeline (see {@link stageIndex}).
 */
export const STAGES = [
  { label: "Introduced", color: "var(--muted)" },
  { label: "In Committee", color: "var(--federal)" },
  { label: "Passed Chamber", color: "var(--accent)" },
  { label: "Passed Congress", color: "var(--state)" },
  { label: "Enacted", color: "var(--fg)" },
] as const;

export const STAGE_COUNT = STAGES.length;

/** Map a raw `BillStatus` string to a pipeline index, or -1 for off-track (Failed). */
export function stageIndex(status: string): number {
  switch (status) {
    case "Enacted":
      return 4;
    case "PassedBothChambers":
      return 3;
    case "PassedOneChamber":
      return 2;
    case "InCommittee":
      return 1;
    case "Failed":
      return -1;
    default:
      return 0; // Introduced / Unknown
  }
}

export function stageColor(idx: number): string {
  return idx < 0 ? "var(--fg-soft)" : STAGES[idx].color;
}

export function stageLabel(idx: number): string {
  return idx < 0 ? "Failed" : STAGES[idx].label;
}

/* --------------------------------------------------------------------------
 * Alignment — client mirror of the backend `BillAlignment` so the whole list
 * (which the API serves without alignment) can be scored against one compass.
 * ------------------------------------------------------------------------ */

const NEUTRAL_BAND = 0.15;

export function classify(userScore: number, billScore: number): "aligned" | "mixed" | "tension" {
  if (Math.abs(userScore) < NEUTRAL_BAND || Math.abs(billScore) < NEUTRAL_BAND) return "mixed";
  return Math.sign(userScore) === Math.sign(billScore) ? "aligned" : "tension";
}

/** Confidence-weighted closeness across shared axes → 0..100, or null if none. */
export function overallPercent(pairs: { u: number; b: number; conf: number }[]): number | null {
  let weight = 0;
  let sum = 0;
  for (const { u, b, conf } of pairs) {
    const w = Math.max(0.05, Math.min(1, conf));
    sum += (1 - Math.abs(u - b) / 2) * w;
    weight += w;
  }
  return weight <= 0 ? null : Math.round((100 * sum) / weight);
}

/* --------------------------------------------------------------------------
 * Geometry
 * ------------------------------------------------------------------------ */

/** Map a signed score (-1..+1) to the 0..1 radar position (0.5 = neutral centre-out). */
function toUnit(score: number): number {
  return (score + 1) / 2;
}

/**
 * SVG polygon `points` for a 6-axis radar, on a 0..100 viewBox. Vertex i sits at
 * angle `-90° + i·60°`; radius scales the unit position by `r` (default 46).
 */
export function radarPoints(scores: number[], r = 46, cx = 50, cy = 50): string {
  return scores
    .map((s, i) => {
      const a = -Math.PI / 2 + (i * Math.PI) / 3;
      const rad = toUnit(s) * r;
      return `${(cx + Math.cos(a) * rad).toFixed(1)},${(cy + Math.sin(a) * rad).toFixed(1)}`;
    })
    .join(" ");
}

/* --------------------------------------------------------------------------
 * User compass
 * ------------------------------------------------------------------------ */

export type UserCompass = {
  /** Six canonical-axis scores (-1..+1), zero where the user has no reading. */
  vec: number[];
  /** The pole the user leans toward per axis: index into AXES[i].low/high. */
};

/** Build the 6-axis user vector from a profile, or null when there's no compass. */
export function userVecFromProfile(profile: Profile | null): number[] | null {
  if (!profile) return null;
  const hasCompass = profile.profileVersion > 0 && profile.answerCount > 0;
  if (!hasCompass) return null;
  const byKey = new Map(profile.axes.map((a) => [a.axisKey, a.score]));
  return AXES.map((a) => byKey.get(a.key) ?? 0);
}

/* --------------------------------------------------------------------------
 * Per-bill view model
 * ------------------------------------------------------------------------ */

export type BillVM = {
  bill: BillSummary;
  /** Six canonical-axis scores (-1..+1), 0 where the bill has no position. */
  axisScores: number[];
  /** Which of the six axes the bill actually implicates. */
  hasAxis: boolean[];
  /** Number of the six canonical axes positioned (0..6). */
  positioned: number;
  stage: number;
  /** Overall alignment 0..100, or null (no compass / no shared axes). */
  alignPct: number | null;
  /** Per-axis alignment when a compass exists, else all null. */
  axisAlignment: ("aligned" | "mixed" | "tension" | null)[];
  /** Precomputed radar polygon points for the bill hexagon. */
  radar: string;
  /** Euclidean distance from the user across shared axes (unit space), or null. */
  distance: number | null;
};

export function buildBillVM(bill: BillSummary, userVec: number[] | null): BillVM {
  const byKey = new Map(bill.axes.map((a) => [a.axisKey, a]));
  const axisScores: number[] = [];
  const hasAxis: boolean[] = [];
  const axisAlignment: ("aligned" | "mixed" | "tension" | null)[] = [];
  const pairs: { u: number; b: number; conf: number }[] = [];
  let distSq = 0;
  let shared = 0;

  AXES.forEach((a, i) => {
    const pos = byKey.get(a.key);
    const score = pos?.score ?? 0;
    axisScores.push(score);
    hasAxis.push(pos != null);

    if (userVec && pos) {
      const u = userVec[i];
      axisAlignment.push(classify(u, pos.score));
      pairs.push({ u, b: pos.score, conf: pos.confidence });
      distSq += (toUnit(u) - toUnit(pos.score)) ** 2;
      shared += 1;
    } else {
      axisAlignment.push(null);
    }
  });

  return {
    bill,
    axisScores,
    hasAxis,
    positioned: hasAxis.filter(Boolean).length,
    stage: stageIndex(bill.status),
    alignPct: userVec ? overallPercent(pairs) : null,
    axisAlignment,
    radar: radarPoints(axisScores),
    distance: userVec && shared > 0 ? Math.sqrt(distSq / shared) : null,
  };
}

/** Alignment → dot colour used across the Floor board and Field plot. */
export function alignmentColor(pct: number | null): string {
  if (pct == null) return "var(--muted)";
  if (pct >= 75) return "var(--accent)";
  if (pct >= 55) return "var(--federal)";
  return "var(--muted)";
}

/** The pole label the given signed score leans toward on axis `i`. */
export function leanPole(i: number, score: number): string {
  return score >= 0 ? AXES[i].high : AXES[i].low;
}

const DATE_FMT: Intl.DateTimeFormatOptions = { month: "short", day: "numeric", year: "numeric" };

export function fmtDate(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : d.toLocaleDateString(undefined, DATE_FMT);
}

export function fmtDateShort(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : d.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

/** Sort keys offered by the Explore filter bar. */
export type SortKey = "recent" | "alignment" | "distance" | "coverage";

export const SORT_OPTIONS: { key: SortKey; label: string; needsCompass?: boolean }[] = [
  { key: "recent", label: "Recent action" },
  { key: "alignment", label: "Best aligned", needsCompass: true },
  { key: "distance", label: "Furthest from you", needsCompass: true },
  { key: "coverage", label: "Most values mapped" },
];

export function sortVms(vms: BillVM[], sort: SortKey): BillVM[] {
  const out = [...vms];
  switch (sort) {
    case "alignment":
      out.sort((a, b) => (b.alignPct ?? -1) - (a.alignPct ?? -1));
      break;
    case "distance":
      out.sort((a, b) => (b.distance ?? -1) - (a.distance ?? -1));
      break;
    case "coverage":
      out.sort((a, b) => b.positioned - a.positioned);
      break;
    case "recent":
    default:
      out.sort((a, b) => actionTime(b.bill) - actionTime(a.bill));
      break;
  }
  return out;
}

function actionTime(bill: BillSummary): number {
  const iso = bill.latestActionDate ?? bill.introducedDate;
  const t = new Date(iso).getTime();
  return Number.isNaN(t) ? 0 : t;
}
