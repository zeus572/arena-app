import { civicApi } from "./client";
import { mapAdminError } from "./admin";

/** A puzzle as a REVIEWER sees it — payload includes the answer key. */
export type AdminDailyPuzzle = {
  id: string;
  kind: string;
  puzzleDate: string;
  edition: number;
  status: "Draft" | "Approved" | "Live" | "Retired";
  generationSource: string;
  locality: string | null;
  plays: number;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  payload: any;
};

export type AdminDailyBalance = {
  forkAxisCounts: Record<string, number>;
  magnitudeTotal: number;
  magnitudeSmallerCount: number;
  magnitudeSmallerShare: number;
  staleMagnitudeKeys: string[];
};

export async function getAdminDailyPuzzles(status?: string): Promise<AdminDailyPuzzle[]> {
  try {
    const { data } = await civicApi.get<AdminDailyPuzzle[]>("/admin/daily", {
      params: status ? { status } : undefined,
    });
    return data;
  } catch (err) {
    mapAdminError(err);
  }
}

export async function approveDailyPuzzle(id: string): Promise<void> {
  try {
    await civicApi.post(`/admin/daily/${id}/approve`);
  } catch (err) {
    mapAdminError(err);
  }
}

export async function rejectDailyPuzzle(id: string): Promise<void> {
  try {
    await civicApi.post(`/admin/daily/${id}/reject`);
  } catch (err) {
    mapAdminError(err);
  }
}

export async function getAdminDailyBalance(): Promise<AdminDailyBalance> {
  try {
    const { data } = await civicApi.get<AdminDailyBalance>("/admin/daily/balance");
    return data;
  } catch (err) {
    mapAdminError(err);
  }
}
