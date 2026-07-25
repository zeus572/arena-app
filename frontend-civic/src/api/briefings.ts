import { civicApi } from "./client";
import type { CivicBriefing, CivicBriefingSummary } from "./types";

export type BriefingPage = {
  items: CivicBriefingSummary[];
  total: number;
  page: number;
  pageSize: number;
};

/**
 * Cheap "is there anything new?" signal for the feed. `latestCreatedAt` is the ISO
 * timestamp of the newest briefing visible to the caller (null if none), and `total`
 * is the count — both walled to the reader's locality, same as {@link getBriefings}.
 * Meant to be polled on an interval (only while the tab is active) so the client can
 * notice fresh stories without re-pulling a whole page.
 */
export type BriefingLatest = {
  latestCreatedAt: string | null;
  total: number;
};

export async function getBriefingsLatest(): Promise<BriefingLatest> {
  const { data } = await civicApi.get<BriefingLatest>("/briefings/latest");
  return data;
}

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
