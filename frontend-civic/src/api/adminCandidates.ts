import { civicApi } from "./client";
import { mapAdminError } from "./admin";
import type { CampaignPost } from "./campaign";

/** Thrown when the backend skips generation (candidate on cooldown or over daily budget) — HTTP 409. */
export class GenerationSkippedError extends Error {}

export type GeneratePostBody = {
  triggerBriefingId?: string;
  force?: boolean;
};

/** POST /api/admin/candidates/:slug/posts/generate — triggers a real LLM generation (spends budget). */
export async function generateCandidatePost(
  slug: string,
  body: GeneratePostBody,
): Promise<CampaignPost> {
  try {
    const { data } = await civicApi.post<CampaignPost>(
      `/admin/candidates/${slug}/posts/generate`,
      body,
    );
    return data;
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 409) {
      throw new GenerationSkippedError(
        "Generation skipped — candidate is on cooldown or over daily budget. Use force to override.",
      );
    }
    mapAdminError(err);
  }
}
