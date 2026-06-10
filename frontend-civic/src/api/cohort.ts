import { civicApi } from "./client";

export type CohortStanding = {
  rank: number;
  userId: string;
  displayName: string;
  isAgent: boolean;
  isMe: boolean;
  isFriend: boolean;
  weeklyPoints: number;
  activeDays: number;
};

export type Cohort = {
  cohortId: string;
  weekKey: string;
  weekStart: string;
  memberCount: number;
  targetSize: number;
  leagueName: string | null;
  friendsCount: number;
  yourRank: number;
  yourWeeklyPoints: number;
  leaderboard: CohortStanding[];
  generatedAt: string;
};

/** The caller's weekly cohort (up to 50 people) and its leaderboard. */
export async function getMyCohort(): Promise<Cohort> {
  const { data } = await civicApi.get<Cohort>("/cohort/me");
  return data;
}
