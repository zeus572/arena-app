import { civicApi } from "./client";
import { mapAdminError } from "./admin";
export { ForbiddenError } from "./admin";

// Mirrors backend-civic EngagementDto (System.Text.Json camel-cases property names;
// dictionary keys like byArea are preserved verbatim, i.e. the area labels).

export type FeatureStat = {
  key: string;
  label: string;
  area: string;
  users: number;
  events: number;
  activeShort: number;
  activeLong: number;
  lastAt: string | null;
};

export type AreaStat = {
  area: string;
  users: number;
  activeLong: number;
};

export type StateStat = {
  state: string;
  profiles: number;
  engagedUsers: number;
  byArea: Record<string, number>;
};

export type BreadthBucket = {
  areasTouched: number;
  users: number;
};

export type Untracked = {
  key: string;
  label: string;
  note: string;
};

export type EngagementSummary = {
  profiles: number;
  engagedUsers: number;
  activeUsersShort: number;
  activeUsersLong: number;
  anonymousEvents: number;
};

export type Engagement = {
  generatedAt: string;
  shortWindowDays: number;
  longWindowDays: number;
  summary: EngagementSummary;
  features: FeatureStat[];
  areas: AreaStat[];
  byState: StateStat[];
  breadth: BreadthBucket[];
  untracked: Untracked[];
};

export async function getEngagement(): Promise<Engagement> {
  try {
    const { data } = await civicApi.get<Engagement>("/admin/engagement");
    return data;
  } catch (err: unknown) {
    mapAdminError(err);
  }
}
