import { civicApi } from "./client";
import { mapAdminError } from "./admin";

// Mirrors backend-civic AdminBudgetDto / CandidateBudgetDto.
export type CandidateBudget = {
  candidateId: string;
  slug: string;
  name: string;
  postsLast24h: number;
  intensity5Last24h: number;
  postsTotal: number;
  lastPostAt: string | null;
};

export type AdminBudget = {
  totalPosts: number;
  postsLast24h: number;
  candidates: CandidateBudget[];
};

export async function getAdminBudget(): Promise<AdminBudget> {
  try {
    const { data } = await civicApi.get<AdminBudget>("/admin/budget");
    return data;
  } catch (err: unknown) {
    mapAdminError(err);
  }
}
