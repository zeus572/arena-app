import { civicApi } from "./client";

export type DebateInitResponse = {
  debateId: string;
  debateUrl: string;
};

export async function requestDebateFromBriefing(
  slug: string,
): Promise<DebateInitResponse> {
  const { data } = await civicApi.post<DebateInitResponse>(
    `/briefings/${slug}/debate`,
    {},
  );
  return data;
}
