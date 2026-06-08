import { civicApi } from "./client";

export type ZeitgeistAxis = {
  axisKey: string;
  axisName: string;
  lowLabel: string;
  highLabel: string;
  averageScore: number;
  leanLabel: string;
  sampleSize: number;
};

export type ZeitgeistCoalition = {
  provisionId: string;
  slug: string;
  title: string;
  state: string;
  prevailingPosition: string;
  accepts: number;
  declines: number;
  participantCount: number;
  signal: string;
};

export type ZeitgeistQuizSignal = {
  topic: string;
  question: string;
  correctRate: number;
  responseCount: number;
};

export type Zeitgeist = {
  generatedAt: string;
  totals: {
    profileCount: number;
    coalitionCount: number;
    quizResponseCount: number;
  };
  axes: ZeitgeistAxis[];
  coalitions: ZeitgeistCoalition[];
  quizSignals: ZeitgeistQuizSignal[];
};

export async function getZeitgeist(): Promise<Zeitgeist> {
  const { data } = await civicApi.get<Zeitgeist>("/zeitgeist");
  return data;
}
