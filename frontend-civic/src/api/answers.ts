import { civicApi } from "./client";

export type AnswerConfidence = "NotSure" | "SomewhatSure" | "VerySure";
export type AnswerIntensity = "Low" | "Medium" | "High" | "NonNegotiable";

export type Answer = {
  id: string;
  questionId: string;
  questionExternalId: string;
  selectedChoiceKey: string;
  confidence: AnswerConfidence;
  intensity: AnswerIntensity;
  reasoningChoice?: string;
  freeTextReasoning?: string;
  createdAt: string;
  updatedAt: string;
};

export type SubmitAnswerInput = {
  questionId: string;
  selectedChoiceKey: string;
  confidence: AnswerConfidence;
  intensity: AnswerIntensity;
  reasoningChoice?: string;
  freeTextReasoning?: string;
};

export async function submitAnswer(input: SubmitAnswerInput): Promise<Answer> {
  const { data } = await civicApi.post<Answer>("/answers", input);
  return data;
}

export async function getMyAnswers(): Promise<Answer[]> {
  const { data } = await civicApi.get<Answer[]>("/answers/me");
  return data;
}
