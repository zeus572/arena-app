import { civicApi } from "./client";

export type CandidateOffice = "President" | "Senate" | "House";

export type CampaignTone =
  | "Stern"
  | "Angry"
  | "Casual"
  | "Hopeful"
  | "Sarcastic"
  | "Presidential"
  | "Folksy"
  | "Wonkish";

export type CandidateSummary = {
  id: string;
  slug: string;
  name: string;
  office: CandidateOffice;
  state: string | null;
  district: number | null;
  party: string;
  isIncumbent: boolean;
  bio: string;
  archetypeKey: string;
  defaultTone: CampaignTone;
  defaultIntensity: number;
  avatarBaseUrl: string;
  isFictional: boolean;
};

export type PlatformPlank = {
  id: string;
  title: string;
  body: string;
  issueTags: string[];
};

export type CandidateValue = {
  axisKey: string;
  axisName: string;
  lowLabel: string;
  highLabel: string;
  order: number;
  score: number;
};

export type IssueTone = {
  issue: string;
  tone: CampaignTone;
  toneLabel: string;
  intensity: number;
  intensityLabel: string;
};

export type CandidateSource = {
  id: string;
  kind: string;
  title: string;
  excerpt: string;
  issueTags: string[];
  priority: number;
};

export type CandidateDetail = CandidateSummary & {
  background: string;
  platformPlanks: PlatformPlank[];
  values: CandidateValue[];
  issueTones: IssueTone[];
  postCount: number;
};

export type PostFragment = {
  id: string;
  text: string;
  start: number;
  end: number;
  order: number;
  up: number;
  down: number;
};

export type CampaignPost = {
  id: string;
  body: string;
  tone: CampaignTone;
  toneLabel: string;
  intensity: number;
  intensityLabel: string;
  issueTags: string[];
  trigger: string;
  triggerBriefingSlug: string | null;
  triggerBriefingHeadline: string | null;
  triggerPostId: string | null;
  citedReference: string | null;
  up: number;
  down: number;
  createdAt: string;
  candidate: CandidateSummary | null;
  fragments: PostFragment[];
};

export type CampaignFeed = {
  items: CampaignPost[];
  nextCursor: string | null;
};

export type HeatmapFragment = PostFragment & { net: number };

export type PostHeatmap = {
  postId: string;
  body: string;
  fragments: HeatmapFragment[];
};

export type ReactionResult = {
  postId: string;
  fragmentId: string | null;
  postUp: number;
  postDown: number;
  fragmentUp: number | null;
  fragmentDown: number | null;
};

export type ElectionCycle = {
  id: string;
  slug: string;
  name: string;
  electionDate: string;
  primarySeasonStart: string;
  generalSeasonStart: string;
  isCurrent: boolean;
  daysUntilElection: number;
};

export type CandidateMatchItem = {
  candidate: CandidateSummary;
  score: number;
  reason: string;
};

export type CandidateMatches = {
  hasProfile: boolean;
  topMatches: CandidateMatchItem[];
  productiveChallenges: CandidateMatchItem[];
  surprisingAgreements: CandidateMatchItem[];
};

export type FeedFilters = {
  office?: string;
  party?: string;
  state?: string;
  district?: number;
  tone?: string;
  minIntensity?: number;
  issue?: string;
  sort?: "recent" | "top" | "controversial" | "trending";
  cursor?: string;
  limit?: number;
};

export type ReactionType = "up" | "down";

export async function getCandidates(filters: {
  office?: string;
  party?: string;
  state?: string;
  district?: number;
} = {}): Promise<CandidateSummary[]> {
  const { data } = await civicApi.get<CandidateSummary[]>("/candidates", { params: filters });
  return data;
}

export async function getCandidate(slug: string): Promise<CandidateDetail | undefined> {
  try {
    const { data } = await civicApi.get<CandidateDetail>(`/candidates/${slug}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) return undefined;
    throw err;
  }
}

export async function getCandidatePosts(slug: string, cursor?: string): Promise<CampaignFeed> {
  const { data } = await civicApi.get<CampaignFeed>(`/candidates/${slug}/posts`, {
    params: { cursor, limit: 20 },
  });
  return data;
}

export async function getCandidateSources(slug: string): Promise<CandidateSource[]> {
  const { data } = await civicApi.get<CandidateSource[]>(`/candidates/${slug}/sources`);
  return data;
}

export async function getCampaignFeed(filters: FeedFilters = {}): Promise<CampaignFeed> {
  const { data } = await civicApi.get<CampaignFeed>("/campaign/feed", { params: filters });
  return data;
}

export async function getPost(id: string): Promise<CampaignPost | undefined> {
  try {
    const { data } = await civicApi.get<CampaignPost>(`/posts/${id}`);
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) return undefined;
    throw err;
  }
}

export async function getPostHeatmap(id: string): Promise<PostHeatmap> {
  const { data } = await civicApi.get<PostHeatmap>(`/posts/${id}/heatmap`);
  return data;
}

export async function reactToPost(id: string, type: ReactionType): Promise<ReactionResult> {
  const { data } = await civicApi.post<ReactionResult>(`/posts/${id}/reactions`, { type });
  return data;
}

export async function removePostReaction(id: string): Promise<ReactionResult> {
  const { data } = await civicApi.delete<ReactionResult>(`/posts/${id}/reactions`);
  return data;
}

export async function reactToFragment(
  id: string,
  fragmentId: string,
  type: ReactionType,
): Promise<ReactionResult> {
  const { data } = await civicApi.post<ReactionResult>(
    `/posts/${id}/fragments/${fragmentId}/reactions`,
    { type },
  );
  return data;
}

export async function removeFragmentReaction(id: string, fragmentId: string): Promise<ReactionResult> {
  const { data } = await civicApi.delete<ReactionResult>(
    `/posts/${id}/fragments/${fragmentId}/reactions`,
  );
  return data;
}

export async function followCandidate(slug: string): Promise<void> {
  await civicApi.post(`/candidates/${slug}/follow`);
}

export async function unfollowCandidate(slug: string): Promise<void> {
  await civicApi.delete(`/candidates/${slug}/follow`);
}

export async function muteCandidate(slug: string): Promise<void> {
  await civicApi.post(`/candidates/${slug}/mute`);
}

export async function unmuteCandidate(slug: string): Promise<void> {
  await civicApi.delete(`/candidates/${slug}/mute`);
}

export async function getCandidateMatches(): Promise<CandidateMatches> {
  const { data } = await civicApi.get<CandidateMatches>("/me/candidate-matches");
  return data;
}

export async function getCurrentElectionCycle(): Promise<ElectionCycle | undefined> {
  try {
    const { data } = await civicApi.get<ElectionCycle>("/election/cycles/current");
    return data;
  } catch (err) {
    if ((err as { response?: { status?: number } }).response?.status === 404) return undefined;
    throw err;
  }
}
