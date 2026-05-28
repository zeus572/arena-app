import { civicApi } from "./client";

export type BillStepStatus = "Done" | "Current" | "Upcoming";

export type BillTimelineStep = {
  id: string;
  externalId: string;
  label: string;
  description: string;
  branch: string;
  status: BillStepStatus;
  order: number;
};

export async function getBillTimeline(): Promise<BillTimelineStep[]> {
  const { data } = await civicApi.get<BillTimelineStep[]>("/bill-timeline");
  return data;
}
