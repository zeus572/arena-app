import { civicApi } from "./client";

export type AxisScore = {
  axisKey: string;
  axisName: string;
  lowLabel: string;
  highLabel: string;
  order: number;
  score: number;
  confidence: number;
  intensity: number;
  supportingAnswerCount: number;
};

export type ArchetypeBlendItem = {
  archetypeKey: string;
  name: string;
  description: string;
  percent: number;
};

export type Profile = {
  userId: string;
  profileVersion: number;
  updatedAt: string;
  answerCount: number;
  /** Chosen local-news region (state code), or null for national-only. */
  localityState: string | null;
  axes: AxisScore[];
  archetypeBlend: ArchetypeBlendItem[];
};

/**
 * Supported local-news regions. The empty value ("") means national-only and
 * maps to a null locality on the server. Keep in sync with the backend
 * `Localities` allowlist and the `News:LocalSources` config keys.
 */
export const LOCALITIES: { value: string; label: string }[] = [
  { value: "", label: "National only" },
  { value: "WA", label: "Washington" },
  { value: "MD", label: "Maryland" },
  { value: "CA", label: "California" },
];

export function localityLabel(code: string | null | undefined): string {
  if (!code) return "National only";
  return LOCALITIES.find((l) => l.value === code)?.label ?? code;
}

export async function getMyProfile(): Promise<Profile> {
  const { data } = await civicApi.get<Profile>("/profile/me");
  return data;
}

/** Set the reader's local-news region. Pass "" / null for national-only. */
export async function setMyLocality(localityState: string | null): Promise<Profile> {
  const { data } = await civicApi.put<Profile>("/profile/me/locality", {
    localityState: localityState || null,
  });
  return data;
}
