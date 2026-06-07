import { civicApi } from "./client";

export interface SpectrumCell {
  bucket: string;
  covered: boolean;
}

export interface SpectrumBar {
  cells: SpectrumCell[];
  coveredBuckets: number;
  totalBuckets: number;
  distance: number;
  deadline: string | null;
  leadingVersionId: string | null;
}

export interface CoalitionSubQuestion {
  key: string;
  prompt: string;
  tradeoff: string | null;
  options: string[];
  origin: string;
}

export interface CoalitionVersion {
  id: string;
  label: string | null;
  text: string;
  positions: Record<string, string>;
  specificity: number;
  authorUserId: string | null;
  accepts: number;
  declines: number;
}

export interface CoalitionParticipant {
  userId: string;
  bucket: string;
  isAgent: boolean;
  hasPositioned: boolean;
}

export interface CoalitionOutcome {
  finalState: string;
  plankVersionId: string | null;
  signers: string[] | null;
  coveredBuckets: number;
  specificity: number;
  movedSigners: number;
  diedReason: string | null;
}

export interface ProvisionSummary {
  id: string;
  slug: string;
  title: string;
  state: string;
  distance: number;
  coveredBuckets: number;
  totalBuckets: number;
  deadline: string | null;
  gapWidth: number;
  difficulty: string;
  governance: boolean;
}

export interface CampaignRecord {
  planksPassed: number;
  totalBreadth: number;
  avgBreadth: number;
  totalMovedSigners: number;
  governanceRatio: number;
  weightedScore: number;
}

export interface Cadence {
  score: number;
  last7Days: boolean[];
}

export interface Plank {
  provisionId: string;
  title: string;
  breadth: number;
  gapWidth: number;
  governance: boolean;
}

export interface Recommended {
  id: string;
  title: string;
  state: string;
  gapWidth: number;
  difficulty: string;
}

export interface Me {
  userId: string;
  skill: number;
  skillLabel: string;
  record: CampaignRecord;
  cadence: Cadence;
  leagueId: string | null;
  leagueName: string | null;
  leagueGapTier: number;
  movement: string;
  recentPlanks: Plank[];
  recommended: Recommended[];
  reasoningXp: number;
  scarcePoints: number;
  todayReasoning: number;
  dailyReasoningCap: number;
}

export interface ActResult {
  points: number;
  currency: string;
}

export interface Standing {
  rank: number;
  userId: string;
  displayName: string;
  isAgent: boolean;
  score: number;
  coalitionsSigned: number;
  totalBreadth: number;
  movedCount: number;
}

export interface League {
  id: string;
  name: string;
  gapTier: number;
  difficultyLabel: string;
  buckets: string[];
  standings: Standing[];
}

export interface ProvisionDetail {
  id: string;
  slug: string;
  title: string;
  neutralText: string;
  state: string;
  relevantAxes: string[];
  deadline: string | null;
  subQuestions: CoalitionSubQuestion[];
  versions: CoalitionVersion[];
  participants: CoalitionParticipant[];
  spectrumBar: SpectrumBar;
  outcome: CoalitionOutcome | null;
  yourUserId: string | null;
  youJoined: boolean;
  gapWidth: number;
  difficulty: string;
  governance: boolean;
}

const BASE = "/coalition/provisions";

export async function listProvisions(): Promise<ProvisionSummary[]> {
  const { data } = await civicApi.get<ProvisionSummary[]>(BASE);
  return data;
}

export async function getProvision(id: string): Promise<ProvisionDetail> {
  const { data } = await civicApi.get<ProvisionDetail>(`${BASE}/${id}`);
  return data;
}

export async function joinProvision(id: string, bucket: string): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>(`${BASE}/${id}/join`, { bucket });
  return data;
}

export async function takePosition(
  id: string,
  body: { stance: string; intensity: string; bucket?: string; reasoningTag?: string },
): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>(`${BASE}/${id}/positions`, body);
  return data;
}

export async function proposeAmendment(
  id: string,
  positions: Record<string, string>,
  label?: string,
): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>(`${BASE}/${id}/amendments`, { positions, label });
  return data;
}

export async function castAcceptance(
  id: string,
  versionId: string,
  accept: boolean,
  intensity = "Medium",
): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>(`${BASE}/${id}/acceptances`, {
    versionId,
    accept,
    intensity,
  });
  return data;
}

export async function agentStep(id: string): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>(`${BASE}/${id}/agent-step`);
  return data;
}

export async function seedCoalition(): Promise<void> {
  await civicApi.post("/coalition/seed");
}

export async function getMe(): Promise<Me> {
  const { data } = await civicApi.get<Me>("/coalition/me");
  return data;
}

export async function getLeagues(): Promise<League[]> {
  const { data } = await civicApi.get<League[]>("/coalition/leagues");
  return data;
}

export async function composeLeagues(): Promise<League[]> {
  const { data } = await civicApi.post<League[]>("/coalition/leagues/compose");
  return data;
}

export async function proposeFreeformAmendment(id: string, text: string): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>(`${BASE}/${id}/amendments/freeform`, { text });
  return data;
}

export async function recordAct(id: string, type: string, payload?: string): Promise<ActResult> {
  const { data } = await civicApi.post<ActResult>(`${BASE}/${id}/acts`, { type, payload });
  return data;
}

export async function birthFromBriefing(briefingId: string): Promise<ProvisionDetail> {
  const { data } = await civicApi.post<ProvisionDetail>("/coalition/birth", { briefingId });
  return data;
}
