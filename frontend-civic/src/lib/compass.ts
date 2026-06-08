import type { Profile, AxisScore } from "@/api/profile";

/**
 * A coalition position derived from a person's Civic Compass instead of a partisan
 * left/center/right self-label. The `bucket` is the string sent to the coalition API
 * (which accepts any spectrum label); `label`/`detail` are how we describe it to the user.
 */
export type CompassPosition = {
  hasData: boolean;
  bucket: string;
  label: string;
  detail: string;
};

const UNDECIDED: CompassPosition = {
  hasData: false,
  bucket: "undecided",
  label: "Compass not built yet",
  detail: "Answer a few Civic Compass questions to join with a real, discovered position.",
};

function sideLabel(axis: AxisScore): string {
  return axis.score >= 0 ? axis.highLabel : axis.lowLabel;
}

/**
 * Turn a Civic Compass profile into the position a person speaks for in a coalition.
 * Prefers the strongest discovered archetype (e.g. "The Public Builder"); falls back to
 * the most strongly-held value axis when archetypes aren't available yet.
 */
export function deriveCompassPosition(profile: Profile | null): CompassPosition {
  if (!profile) return UNDECIDED;

  const answeredAxes = profile.axes.filter((a) => a.supportingAnswerCount > 0);
  const hasData = profile.answerCount > 0 && (answeredAxes.length > 0 || profile.archetypeBlend.length > 0);
  if (!hasData) return UNDECIDED;

  const top = [...profile.archetypeBlend].sort((a, b) => b.percent - a.percent)[0];
  if (top) {
    return {
      hasData: true,
      bucket: top.archetypeKey,
      label: top.name,
      detail: `${Math.round(top.percent)}% match · discovered from your Civic Compass`,
    };
  }

  // Fallback: lead with the axis the person holds most strongly.
  const strongest = [...answeredAxes].sort((a, b) => Math.abs(b.score) - Math.abs(a.score))[0];
  const side = sideLabel(strongest);
  return {
    hasData: true,
    bucket: `${strongest.axisKey}:${strongest.score >= 0 ? "high" : "low"}`,
    label: side,
    detail: `Your strongest lean · ${strongest.axisName}`,
  };
}

/** Prettify a raw spectrum bucket string (archetype key or seeded label) for display. */
export function prettyBucket(bucket: string): string {
  if (bucket.includes(":")) bucket = bucket.split(":")[0];
  return bucket
    .split(/[-_]/)
    .map((w) => (w ? w[0].toUpperCase() + w.slice(1) : w))
    .join(" ");
}
