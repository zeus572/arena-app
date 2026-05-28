import { civicApi } from "./client";
import type { CivicBriefing, CivicBriefingSummary } from "./types";

export async function getBriefings(): Promise<CivicBriefingSummary[]> {
  const { data } = await civicApi.get<CivicBriefingSummary[]>("/briefings");
  return data;
}

export async function getBriefingBySlug(
  slug: string,
): Promise<CivicBriefing | undefined> {
  try {
    const { data } = await civicApi.get<CivicBriefing>(`/briefings/${slug}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) {
      return undefined;
    }
    throw err;
  }
}
