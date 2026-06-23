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
  /** 5-digit ZIP code collected at sign-up, or null. */
  zipCode: string | null;
  /** Age-bracket key (e.g. "25_34") collected at sign-up, or null. */
  ageRange: string | null;
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

/**
 * Supported age brackets for the sign-up personalization question. The `value`
 * is the stable key stored server-side — keep in sync with the backend
 * `AgeRanges.Supported` allowlist.
 */
export const AGE_RANGES: { value: string; label: string }[] = [
  { value: "under_18", label: "Under 18" },
  { value: "18_24", label: "18–24" },
  { value: "25_34", label: "25–34" },
  { value: "35_44", label: "35–44" },
  { value: "45_54", label: "45–54" },
  { value: "55_64", label: "55–64" },
  { value: "65_plus", label: "65 or older" },
];

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

/**
 * Save the sign-up personalization fields. The local-news region is derived from
 * the ZIP server-side. Pass "" / null to leave a field unset.
 */
export async function setMyDemographics(
  zipCode: string | null,
  ageRange: string | null,
): Promise<Profile> {
  const { data } = await civicApi.put<Profile>("/profile/me/demographics", {
    zipCode: zipCode || null,
    ageRange: ageRange || null,
  });
  return data;
}
