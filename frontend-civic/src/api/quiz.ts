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
  /** Share (0..1) of people who answered correctly in the trailing 60 days. */
  correctRate: number;
  /** How many responses the 60-day moving average is based on. */
  responseCount: number;
};

export type QuizPollResult = {
  questionId: string;
  correctAnswerIndex: number;
  isCorrect: boolean;
  responseCount: number;
  correctCount: number;
  correctRate: number;
  windowDays: number;
};

/** Fetch a freshly shuffled set of quiz questions (dynamic on every load). */
export async function getQuizQuestions(count?: number): Promise<QuizQuestion[]> {
  const { data } = await civicApi.get<QuizQuestion[]>("/quiz/questions", {
    params: count ? { count } : undefined,
  });
  return data;
}

/** Record this person's answer and get back the updated global poll for the question. */
export async function submitQuizResponse(
  questionId: string,
  selectedIndex: number,
): Promise<QuizPollResult> {
  const { data } = await civicApi.post<QuizPollResult>(
    `/quiz/questions/${questionId}/responses`,
    { selectedIndex },
  );
  return data;
}
