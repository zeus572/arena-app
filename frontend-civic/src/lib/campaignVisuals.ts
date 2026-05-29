import type { CampaignTone } from "@/api/campaign";

// Per-tone accent color used on post cards and tone chips. The Campaign Feed
// uses its own palette (indigo-leaning) to stay visually distinct from the
// real-news Briefings surface — the two must never be confused.
export const TONE_META: Record<CampaignTone, { label: string; color: string }> = {
  Stern: { label: "Stern", color: "#475569" },
  Angry: { label: "Defiant", color: "#dc2626" },
  Casual: { label: "Casual", color: "#0891b2" },
  Hopeful: { label: "Hopeful", color: "#16a34a" },
  Sarcastic: { label: "Dry", color: "#9333ea" },
  Presidential: { label: "Statesmanlike", color: "#1d4ed8" },
  Folksy: { label: "Folksy", color: "#d97706" },
  Wonkish: { label: "Wonkish", color: "#0d9488" },
};

export const INTENSITY_LABEL: Record<number, string> = {
  1: "Measured",
  2: "Concerned",
  3: "Engaged",
  4: "Heated",
  5: "Fired up",
};

export function toneColor(tone: CampaignTone): string {
  return TONE_META[tone]?.color ?? "#6366f1";
}

/** Border thickness in px scaling with intensity (1..5). */
export function intensityBorderWidth(intensity: number): number {
  return Math.min(5, Math.max(1, intensity));
}

/** Background tint for a fragment in the heat map, keyed by net sentiment [-1,1]. */
export function netSentimentColor(net: number): string {
  if (net > 0.15) {
    const a = Math.min(0.35, 0.12 + net * 0.25);
    return `rgba(22, 163, 74, ${a.toFixed(2)})`; // green
  }
  if (net < -0.15) {
    const a = Math.min(0.35, 0.12 + Math.abs(net) * 0.25);
    return `rgba(220, 38, 38, ${a.toFixed(2)})`; // red
  }
  return "rgba(100, 116, 139, 0.10)"; // neutral gray
}
