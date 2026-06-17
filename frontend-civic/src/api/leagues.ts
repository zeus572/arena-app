import { civicApi } from "./client";
import type { CampaignPost, CampaignTone, ReactionResult, ReactionType } from "./campaign";

// ---------------------------------------------------------------- Types

export type LeagueMemberRole = "Owner" | "Member";
export type LeagueRoundStatus = "OpenForResponses" | "Voting" | "Closed";

export type LeagueSummary = {
  id: string;
  name: string;
  description: string | null;
  seasonNumber: number;
  memberCount: number;
  maxMembers: number;
  myRole: LeagueMemberRole;
  hasLinkedCampaign: boolean;
  activeRoundId: string | null;
  activeRoundStatus: LeagueRoundStatus | null;
  createdAt: string;
  updatedAt: string;
};

export type LeagueMember = {
  id: string;
  userId: string;
  role: LeagueMemberRole;
  displayName: string;
  avatarUrl: string | null;
  campaignId: string | null;
  candidateName: string | null;
  candidateSlug: string | null;
  party: string | null;
  joinedAt: string;
};

export type LeagueStanding = {
  memberId: string;
  userId: string;
  displayName: string;
  avatarUrl: string | null;
  candidateName: string | null;
  party: string | null;
  rank: number;
  leagueScore: number;
  roundPoints: number;
  campaignScore: number;
  supportShare: number | null;
  won: boolean | null;
  isMe: boolean;
};

export type LeagueRoundSummary = {
  id: string;
  roundNumber: number;
  status: LeagueRoundStatus;
  briefingSlug: string;
  headline: string;
  entryCount: number;
  iHaveEntered: boolean;
  winnerMemberId: string | null;
  winnerDisplayName: string | null;
  responsesCloseAt: string | null;
  votingCloseAt: string | null;
  createdAt: string;
};

export type LeagueDetail = {
  id: string;
  name: string;
  description: string | null;
  ownerUserId: string;
  seasonNumber: number;
  maxMembers: number;
  myRole: LeagueMemberRole;
  myMemberId: string;
  myCampaignId: string | null;
  createdAt: string;
  updatedAt: string;
  members: LeagueMember[];
  standings: LeagueStanding[];
  activeRound: LeagueRoundSummary | null;
  rounds: LeagueRoundSummary[];
};

export type LeagueInvite = {
  id: string;
  code: string;
  joinPath: string;
  /** Set when this is a personal email invite; null for an open share link. */
  email: string | null;
  /** True once a personal invite's recipient has joined. */
  accepted: boolean;
  expiresAt: string | null;
  maxUses: number | null;
  useCount: number;
  isValid: boolean;
  createdAt: string;
};

export type EmailInviteStatus = "invited" | "already_member" | "already_invited" | "invalid";

export type EmailInviteResult = {
  email: string;
  status: EmailInviteStatus;
  invite: LeagueInvite | null;
};

export type LeagueInvitePreview = {
  code: string;
  leagueName: string;
  memberCount: number;
  maxMembers: number;
  inviterDisplayName: string | null;
  inviterEmail: string | null;
  inviterAvatarUrl: string | null;
  isValid: boolean;
  reason: string | null;
  alreadyMember: boolean;
  isFull: boolean;
};

/**
 * Privacy-safe invite preview for signed-out visitors. Powers the enticing "X members, organized
 * by …" card on the join page before the visitor has an account. No inviter email / alreadyMember
 * (those need a signed-in caller).
 */
export type LeagueInvitePublicPreview = {
  code: string;
  leagueName: string;
  memberCount: number;
  maxMembers: number;
  organizerDisplayName: string | null;
  organizerAvatarUrl: string | null;
  isValid: boolean;
  reason: string | null;
  isFull: boolean;
};

export type NewsResponseOptionDetail = {
  id: string;
  label: string;
  angle: string;
  tone: string;
  body: string;
};

export type LeagueRoundEntry = {
  id: string;
  memberId: string;
  displayName: string;
  avatarUrl: string | null;
  isMe: boolean;
  optionLabel: string | null;
  pointsEarned: number;
  net: number;
  isWinner: boolean;
  post: CampaignPost;
};

export type LeagueRoundDetail = {
  id: string;
  leagueId: string;
  roundNumber: number;
  status: LeagueRoundStatus;
  myRole: LeagueMemberRole;
  briefingSlug: string;
  headline: string;
  summary: string;
  valuesInConflict: string[];
  tags: string[];
  responsesCloseAt: string | null;
  votingCloseAt: string | null;
  iHaveEntered: boolean;
  canSubmit: boolean;
  cannotSubmitReason: string | null;
  options: NewsResponseOptionDetail[];
  entriesVisible: boolean;
  entries: LeagueRoundEntry[];
  winnerMemberId: string | null;
  winnerDisplayName: string | null;
};

export type LeagueRoundResults = {
  id: string;
  roundNumber: number;
  headline: string;
  winnerMemberId: string | null;
  winnerDisplayName: string | null;
  entries: LeagueRoundEntry[];
};

// ---------------------------------------------------------------- Requests

export type CreateLeagueBody = { name: string; description?: string; displayName?: string; email?: string | null; avatarUrl?: string | null };
export type CreateInviteBody = { expiresAt?: string | null; maxUses?: number | null };
export type JoinLeagueBody = { displayName?: string; email?: string | null; avatarUrl?: string | null };
export type OpenRoundBody = { briefingSlug: string; responsesCloseAt?: string | null; votingCloseAt?: string | null };
export type SubmitEntryBody = { optionId: string; tone?: CampaignTone };

const BASE = "/leagues";

function notFoundToUndefined<T>(err: unknown): T | undefined {
  if ((err as { response?: { status?: number } }).response?.status === 404) return undefined;
  throw err;
}

// ---------------------------------------------------------------- Leagues

export async function listMyLeagues(): Promise<LeagueSummary[]> {
  const { data } = await civicApi.get<LeagueSummary[]>(BASE);
  return data;
}

export async function getLeague(id: string): Promise<LeagueDetail | undefined> {
  try {
    const { data } = await civicApi.get<LeagueDetail>(`${BASE}/${id}`);
    return data;
  } catch (err) {
    return notFoundToUndefined<LeagueDetail>(err);
  }
}

export async function createLeague(body: CreateLeagueBody): Promise<LeagueDetail> {
  const { data } = await civicApi.post<LeagueDetail>(BASE, body);
  return data;
}

export async function linkCampaign(id: string, campaignId: string): Promise<LeagueDetail> {
  const { data } = await civicApi.post<LeagueDetail>(`${BASE}/${id}/link-campaign`, { campaignId });
  return data;
}

export async function refreshIdentity(id: string, body: JoinLeagueBody): Promise<LeagueDetail> {
  const { data } = await civicApi.post<LeagueDetail>(`${BASE}/${id}/refresh-identity`, body);
  return data;
}

export async function leaveLeague(id: string): Promise<void> {
  await civicApi.post(`${BASE}/${id}/leave`);
}

// ---------------------------------------------------------------- Invites

export async function createInvite(id: string, body: CreateInviteBody = {}): Promise<LeagueInvite> {
  const { data } = await civicApi.post<LeagueInvite>(`${BASE}/${id}/invites`, body);
  return data;
}

export async function listInvites(id: string): Promise<LeagueInvite[]> {
  const { data } = await civicApi.get<LeagueInvite[]>(`${BASE}/${id}/invites`);
  return data;
}

export async function inviteByEmail(id: string, emails: string[]): Promise<EmailInviteResult[]> {
  const { data } = await civicApi.post<EmailInviteResult[]>(`${BASE}/${id}/invites/email`, { emails });
  return data;
}

export async function revokeInvite(id: string, inviteId: string): Promise<void> {
  await civicApi.delete(`${BASE}/${id}/invites/${inviteId}`);
}

export async function previewInvite(code: string): Promise<LeagueInvitePreview | undefined> {
  try {
    const { data } = await civicApi.get<LeagueInvitePreview>(`${BASE}/join/${code}`);
    return data;
  } catch (err) {
    return notFoundToUndefined<LeagueInvitePreview>(err);
  }
}

/** Anonymous-friendly preview for signed-out visitors landing on a join link. */
export async function previewInvitePublic(code: string): Promise<LeagueInvitePublicPreview | undefined> {
  try {
    const { data } = await civicApi.get<LeagueInvitePublicPreview>(`${BASE}/join/${code}/public`);
    return data;
  } catch (err) {
    return notFoundToUndefined<LeagueInvitePublicPreview>(err);
  }
}

export async function joinByCode(code: string, body: JoinLeagueBody): Promise<LeagueDetail> {
  const { data } = await civicApi.post<LeagueDetail>(`${BASE}/join/${code}`, body);
  return data;
}

// ---------------------------------------------------------------- Rounds

export async function listRounds(id: string): Promise<LeagueRoundSummary[]> {
  const { data } = await civicApi.get<LeagueRoundSummary[]>(`${BASE}/${id}/rounds`);
  return data;
}

export async function openRound(id: string, body: OpenRoundBody): Promise<LeagueRoundDetail> {
  const { data } = await civicApi.post<LeagueRoundDetail>(`${BASE}/${id}/rounds`, body);
  return data;
}

export async function getRound(id: string, roundId: string): Promise<LeagueRoundDetail | undefined> {
  try {
    const { data } = await civicApi.get<LeagueRoundDetail>(`${BASE}/${id}/rounds/${roundId}`);
    return data;
  } catch (err) {
    return notFoundToUndefined<LeagueRoundDetail>(err);
  }
}

export async function submitEntry(id: string, roundId: string, body: SubmitEntryBody): Promise<LeagueRoundDetail> {
  const { data } = await civicApi.post<LeagueRoundDetail>(`${BASE}/${id}/rounds/${roundId}/entries`, body);
  return data;
}

export async function voteEntry(
  id: string,
  roundId: string,
  entryId: string,
  type: ReactionType,
): Promise<ReactionResult> {
  const { data } = await civicApi.post<ReactionResult>(
    `${BASE}/${id}/rounds/${roundId}/entries/${entryId}/vote`,
    { type },
  );
  return data;
}

export async function unvoteEntry(id: string, roundId: string, entryId: string): Promise<ReactionResult> {
  const { data } = await civicApi.delete<ReactionResult>(
    `${BASE}/${id}/rounds/${roundId}/entries/${entryId}/vote`,
  );
  return data;
}

export async function startVoting(id: string, roundId: string): Promise<LeagueRoundDetail> {
  const { data } = await civicApi.post<LeagueRoundDetail>(`${BASE}/${id}/rounds/${roundId}/start-voting`);
  return data;
}

export async function closeRound(id: string, roundId: string): Promise<LeagueRoundResults> {
  const { data } = await civicApi.post<LeagueRoundResults>(`${BASE}/${id}/rounds/${roundId}/close`);
  return data;
}

export async function getRoundResults(id: string, roundId: string): Promise<LeagueRoundResults | undefined> {
  try {
    const { data } = await civicApi.get<LeagueRoundResults>(`${BASE}/${id}/rounds/${roundId}/results`);
    return data;
  } catch (err) {
    return notFoundToUndefined<LeagueRoundResults>(err);
  }
}
