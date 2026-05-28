import { civicApi } from "./client";

export type AxisScore = {
  axisKey: string;
  axisName: string;
  lowLabel: string;
  highLabel: string;
  order: number;
  score: number;
  confidence: number;
  intensity: number;
  supportingAnswerCount: number;
};

export type ArchetypeBlendItem = {
  archetypeKey: string;
  name: string;
  description: string;
  percent: number;
};

export type Profile = {
  userId: string;
  profileVersion: number;
  updatedAt: string;
  answerCount: number;
  axes: AxisScore[];
  archetypeBlend: ArchetypeBlendItem[];
};

export async function getMyProfile(): Promise<Profile> {
  const { data } = await civicApi.get<Profile>("/profile/me");
  return data;
}
