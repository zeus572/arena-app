import { civicApi } from "./client";

export type QuizQuestion = {
  id: string;
  externalId: string;
  topic: string;
  question: string;
  options: string[];
  correctAnswerIndex: number;
  explanation: string;
  relatedConceptSlug: string | null;
  order: number;
};

export async function getQuizQuestions(): Promise<QuizQuestion[]> {
  const { data } = await civicApi.get<QuizQuestion[]>("/quiz/questions");
  return data;
}
