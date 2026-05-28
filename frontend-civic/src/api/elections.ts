import { civicApi } from "./client";

export type ElectionScope = "National" | "State" | "Local";

export type Election = {
  id: string;
  slug: string;
  name: string;
  scope: ElectionScope;
  scheduledAt: string; // ISO 8601 UTC
  region: string | null;
  description: string | null;
};

export type ElectionQuery = {
  scope?: ElectionScope | Lowercase<ElectionScope>;
  region?: string;
};

function buildParams(q: ElectionQuery | undefined): Record<string, string> | undefined {
  if (!q) return undefined;
  const params: Record<string, string> = {};
  if (q.scope) params.scope = q.scope;
  if (q.region) params.region = q.region;
  return Object.keys(params).length ? params : undefined;
}

export async function getUpcomingElections(
  query?: ElectionQuery,
): Promise<Election[]> {
  const { data } = await civicApi.get<Election[]>("/elections", {
    params: buildParams(query),
  });
  return data;
}

export async function getNextElection(
  query?: ElectionQuery,
): Promise<Election | undefined> {
  try {
    const { data } = await civicApi.get<Election>("/elections/next", {
      params: buildParams(query),
    });
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) {
      return undefined;
    }
    throw err;
  }
}
