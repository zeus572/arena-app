import { civicApi } from "./client";

export type BudgetFact = {
  id: string;
  factDate: string;
  category: string;
  tensionLabel: string;
  perspectiveA: string;
  sourceA: string;
  sourceUrlA: string;
  perspectiveB: string;
  sourceB: string;
  sourceUrlB: string;
  explanation: string;
};

export async function fetchBudgetFacts(): Promise<BudgetFact[]> {
  const res = await civicApi.get<BudgetFact[]>("/budget-facts");
  return res.data;
}
