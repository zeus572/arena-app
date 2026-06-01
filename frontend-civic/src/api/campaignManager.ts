import { civicApi } from "./client";
import type { CandidateSummary, CampaignTone } from "./campaign";

export type CivicCampaignDifficulty = "Easy" | "Normal" | "Hard";
export type CivicCampaignStatus = "Active" | "Completed" | "Abandoned";
export type CivicCampaignActionType =
  | "PublishPost"
  | "RapidResponse"
  | "ShoreUpAxis"
  | "TargetIssue";

export type CivicRace = {
  raceKey: string;
  office: string;
  state: string | null;
  district: number | null;
  label: string;
  candidates: CandidateSummary[];
};

export type CivicCampaignSummary = {
  id: string;
  candidateSlug: string;
  candidateName: string;
  party: string;
  raceLabel: string;
  difficulty: CivicCampaignDifficulty;
  status: CivicCampaignStatus;
  currentWeek: number;
  totalWeeks: number;
  playerSupport: number;
  isLeading: boolean;
  won: boolean | null;
  createdAt: string;
  updatedAt: string;
};

export type CivicCampaignStanding = {
  candidateId: string;
  candidateSlug: string;
  candidateName: string;
  party: string;
  isPlayer: boolean;
  supportShare: number;
  momentum: number;
};

export type CivicCampaignWeek = {
  weekNumber: number;
  playerSupportAfter: number;
  salientIssues: string[];
  summary: string;
  createdAt: string;
};

export type CivicCampaignAction = {
  weekNumber: number;
  actionType: CivicCampaignActionType;
  target: string | null;
  tone: string | null;
  supportDelta: number;
  generatedPostId: string | null;
  summary: string;
  createdAt: string;
};

export type CivicActionOption = {
  actionType: CivicCampaignActionType;
  label: string;
  description: string;
  suggestedTarget: string | null;
};

export type CivicCampaignDetail = {
  id: string;
  candidateSlug: string;
  candidateName: string;
  party: string;
  candidateBio: string;
  avatarBaseUrl: string;
  raceKey: string;
  raceLabel: string;
  difficulty: CivicCampaignDifficulty;
  status: CivicCampaignStatus;
  currentWeek: number;
  totalWeeks: number;
  actionsRemaining: number;
  won: boolean | null;
  finalSupport: number | null;
  outcome: string | null;
  createdAt: string;
  updatedAt: string;
  standings: CivicCampaignStanding[];
  salientIssues: string[];
  availableActions: CivicActionOption[];
  thisWeekActions: CivicCampaignAction[];
  history: CivicCampaignWeek[];
};

export type TakeActionResult = {
  action: CivicCampaignAction;
  playerSupportAfter: number;
  actionsRemaining: number;
  generatedPostBody: string | null;
  campaign: CivicCampaignDetail;
};

export type AdvanceWeekResult = {
  completedWeek: number;
  playerSupportAfter: number;
  isLeading: boolean;
  standings: CivicCampaignStanding[];
  summary: string;
  campaignCompleted: boolean;
  campaign: CivicCampaignDetail;
};

export type CivicCampaignResults = {
  id: string;
  candidateName: string;
  raceLabel: string;
  won: boolean;
  finalSupport: number;
  finalRank: number;
  fieldSize: number;
  totalWeeks: number;
  outcome: string;
  finalStandings: CivicCampaignStanding[];
  supportTrend: CivicCampaignWeek[];
};

export type CreateCampaignBody = {
  candidateSlug: string;
  difficulty: CivicCampaignDifficulty;
  totalWeeks?: number;
};

export type TakeActionBody = {
  actionType: CivicCampaignActionType;
  target?: string;
  tone?: CampaignTone;
};

const BASE = "/campaign-manager";

export async function getRaces(): Promise<CivicRace[]> {
  const { data } = await civicApi.get<CivicRace[]>(`${BASE}/races`);
  return data;
}

export async function listCampaigns(): Promise<CivicCampaignSummary[]> {
  const { data } = await civicApi.get<CivicCampaignSummary[]>(`${BASE}/campaigns`);
  return data;
}

export async function getCampaign(id: string): Promise<CivicCampaignDetail | undefined> {
  try {
    const { data } = await civicApi.get<CivicCampaignDetail>(`${BASE}/campaigns/${id}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) return undefined;
    throw err;
  }
}

export async function createCampaign(body: CreateCampaignBody): Promise<CivicCampaignDetail> {
  const { data } = await civicApi.post<CivicCampaignDetail>(`${BASE}/campaigns`, body);
  return data;
}

export async function takeAction(id: string, body: TakeActionBody): Promise<TakeActionResult> {
  const { data } = await civicApi.post<TakeActionResult>(`${BASE}/campaigns/${id}/actions`, body);
  return data;
}

export async function advanceWeek(id: string): Promise<AdvanceWeekResult> {
  const { data } = await civicApi.post<AdvanceWeekResult>(`${BASE}/campaigns/${id}/advance`);
  return data;
}

export async function getCampaignResults(id: string): Promise<CivicCampaignResults> {
  const { data } = await civicApi.get<CivicCampaignResults>(`${BASE}/campaigns/${id}/results`);
  return data;
}
