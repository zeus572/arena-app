import { civicApi } from "./client";
import type { CivicBriefing, CivicBriefingSummary } from "./types";

export type BriefingPage = {
  items: CivicBriefingSummary[];
  total: number;
  page: number;
  pageSize: number;
};

export async function getBriefings(
  page = 1,
  pageSize = 20,
): Promise<BriefingPage> {
  const { data } = await civicApi.get<BriefingPage>("/briefings", {
    params: { page, pageSize },
  });
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
