import { civicApi } from "./client";

export type QuestionChoice = {
  key: string;
  label: string;
};

export type Question = {
  id: string;
  externalId: string;
  type: string;
  prompt: string;
  topic?: string;
  order: number;
  choices: QuestionChoice[];
};

export async function getQuestions(opts?: {
  type?: string;
  take?: number;
}): Promise<Question[]> {
  const params = new URLSearchParams();
  if (opts?.type) params.set("type", opts.type);
  if (opts?.take) params.set("take", String(opts.take));
  const qs = params.toString();
  const { data } = await civicApi.get<Question[]>(
    `/questions${qs ? `?${qs}` : ""}`,
  );
  return data;
}
