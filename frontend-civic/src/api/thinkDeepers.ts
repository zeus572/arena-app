import { civicApi } from "./client";
import type { ThinkDeeper } from "./types";

export async function getThinkDeeperBySlug(
  slug: string,
): Promise<ThinkDeeper | undefined> {
  try {
    const { data } = await civicApi.get<ThinkDeeper>(`/think-deepers/${slug}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) {
      return undefined;
    }
    throw err;
  }
}
