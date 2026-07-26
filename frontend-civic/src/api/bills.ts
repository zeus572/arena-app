import { civicApi } from "./client";

/** A bill's position on one value axis (user-independent), as returned on list rows. */
export type BillAxisScore = {
  axisKey: string;
  /** Where the bill pushes on this axis (-1..+1). */
  score: number;
  /** Confidence in the position (0..1). */
  confidence: number;
};

export type BillSummary = {
  id: string;
  externalId: string;
  title: string;
  shortTitle: string | null;
  identifier: string;
  sponsor: string;
  party: string | null;
  status: string;
  jurisdiction: string;
  jurisdictionRegion: string | null;
  introducedDate: string;
  latestActionDate: string | null;
  teaser: string;
  axisCount: number;
  /** Per-axis value positions the bill implicates. Empty for older payloads. */
  axes: BillAxisScore[];
};

export type BillAxisAlignment = {
  axisKey: string;
  axisName: string;
  lowLabel: string;
  highLabel: string;
  order: number;
  billScore: number;
  billConfidence: number;
  rationale: string;
  evidence: string | null;
  /** The user's own score on this axis (-1..+1), or null when they have no compass. */
  userScore: number | null;
  /** "aligned" | "mixed" | "tension" when both are known, else null. */
  alignment: "aligned" | "mixed" | "tension" | null;
};

export type BillDetail = {
  id: string;
  externalId: string;
  congress: number;
  billType: string;
  number: number;
  identifier: string;
  title: string;
  shortTitle: string | null;
  summary: string;
  synthesisSummary: string | null;
  sponsor: string;
  party: string | null;
  status: string;
  jurisdiction: string;
  jurisdictionRegion: string | null;
  introducedDate: string;
  latestActionDate: string | null;
  fullTextUrl: string | null;
  sourceUrl: string | null;
  hasUserCompass: boolean;
  overallAlignmentPercent: number | null;
  axes: BillAxisAlignment[];
};

export async function listBills(jurisdiction?: string): Promise<BillSummary[]> {
  const { data } = await civicApi.get<BillSummary[]>("/bills", {
    params: jurisdiction ? { jurisdiction } : undefined,
  });
  return data;
}

export async function getBill(id: string): Promise<BillDetail> {
  const { data } = await civicApi.get<BillDetail>(`/bills/${id}`);
  return data;
}
