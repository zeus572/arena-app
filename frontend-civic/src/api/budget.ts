import { civicApi } from "./client";

export type BudgetCategory = {
  key: string;
  name: string;
  description: string;
  order: number;
};

export type BudgetAllocation = {
  categoryKey: string;
  points: number;
};

export type BudgetSession = {
  id: string;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  totalPoints: number;
  isComplete: boolean;
  allocations: BudgetAllocation[];
};

export async function getBudgetCategories(): Promise<BudgetCategory[]> {
  const { data } = await civicApi.get<BudgetCategory[]>("/budget/categories");
  return data;
}

export async function startBudgetSession(): Promise<BudgetSession> {
  const { data } = await civicApi.post<BudgetSession>("/budget/sessions");
  return data;
}

export async function getCurrentBudgetSession(): Promise<BudgetSession | null> {
  const resp = await civicApi.get<BudgetSession | null | "">(
    "/budget/sessions/me/current",
  );
  // ASP.NET serializes Ok(null) as 204 No Content with empty body; treat that as "no current session".
  if (
    resp.status === 204 ||
    resp.data === undefined ||
    resp.data === null ||
    (typeof resp.data === "string" && resp.data === "")
  ) {
    return null;
  }
  return resp.data;
}

export async function setBudgetAllocations(
  sessionId: string,
  allocations: BudgetAllocation[],
): Promise<BudgetSession> {
  const { data } = await civicApi.put<BudgetSession>(
    `/budget/sessions/${sessionId}/allocations`,
    { allocations },
  );
  return data;
}

export async function completeBudgetSession(
  sessionId: string,
): Promise<BudgetSession> {
  const { data } = await civicApi.post<BudgetSession>(
    `/budget/sessions/${sessionId}/complete`,
  );
  return data;
}
