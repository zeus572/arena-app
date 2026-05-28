import { civicApi } from "./client";

export type ReceiptTension = {
  axisKey: string;
  axisName: string;
  framing: string;
};

export type ValuesReceipt = {
  id: string;
  createdAt: string;
  answerCountAtTime: number;
  profileVersionAtTime: number;
  learnedInsights: string[];
  changedAxes: string[];
  uncertainAreas: string[];
  tensions: ReceiptTension[];
};

export async function buildReceipt(): Promise<ValuesReceipt> {
  const { data } = await civicApi.post<ValuesReceipt>("/receipts");
  return data;
}

export async function getMyReceipts(): Promise<ValuesReceipt[]> {
  const { data } = await civicApi.get<ValuesReceipt[]>("/receipts/me");
  return data;
}

export async function getReceipt(id: string): Promise<ValuesReceipt | undefined> {
  try {
    const { data } = await civicApi.get<ValuesReceipt>(`/receipts/${id}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) {
      return undefined;
    }
    throw err;
  }
}
