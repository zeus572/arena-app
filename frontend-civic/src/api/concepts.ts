import { civicApi } from "./client";
import type { Concept } from "./types";

export async function getConcepts(): Promise<Concept[]> {
  const { data } = await civicApi.get<Concept[]>("/concepts");
  return data;
}

export async function getConceptBySlug(slug: string): Promise<Concept | undefined> {
  try {
    const { data } = await civicApi.get<Concept>(`/concepts/${slug}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) {
      return undefined;
    }
    throw err;
  }
}
