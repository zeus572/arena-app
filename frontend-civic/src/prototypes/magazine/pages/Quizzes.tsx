import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Check, X, Trophy, Users } from "lucide-react";
import {
  getQuizQuestions,
  submitQuizResponse,
  type QuizQuestion,
  type QuizPollResult,
} from "@/api/quiz";

export default function MagazineQuizzes() {
  const [questions, setQuestions] = useState<QuizQuestion[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [index, setIndex] = useState(0);
  const [picked, setPicked] = useState<number | null>(null);
  const [poll, setPoll] = useState<QuizPollResult | null>(null);
  const [correctCount, setCorrectCount] = useState(0);
  const [done, setDone] = useState(false);

  function loadQuestions() {
    setLoaded(false);
    void getQuizQuestions()
      .then((qs) => setQuestions(qs))
      .finally(() => setLoaded(true));
  }

  useEffect(loadQuestions, []);

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="quiz-loading">
        Loading the quiz…
      </p>
    );
  }

  if (questions.length === 0) {
    return (
      <p className="py-12 text-base text-[var(--muted)]" data-testid="quiz-empty">
        No quiz questions are available right now.
      </p>
    );
  }

  if (done) {
    const total = questions.length;
    return (
      <article
        className="mx-auto max-w-2xl border border-[var(--border)] bg-[var(--bg-elev)] p-10 text-center"
        data-testid="quiz-results"
      >
        <Trophy className="mx-auto h-10 w-10 text-[var(--accent)]" />
        <h2 className="display mt-4 text-4xl">Quiz complete</h2>
        <p
          className="mt-3 text-lg text-[var(--fg-soft)]"
          data-testid="quiz-results-score"
        >
          You got <strong>{correctCount}</strong> of {total} right.
        </p>
        <p className="mt-2 text-sm text-[var(--muted)]">
          Your answers feed the global poll — a 60-day moving average of how everyone's doing.
        </p>
        <div className="mt-6 flex justify-center gap-3">
          <button
            type="button"
            onClick={() => {
              setIndex(0);
              setPicked(null);
              setPoll(null);
              setCorrectCount(0);
              setDone(false);
              // Pull a fresh, reshuffled set so "try again" isn't the same quiz.
              loadQuestions();
            }}
            className="rounded-full border border-[var(--accent)] px-5 py-2 text-sm font-semibold text-[var(--accent)]"
            data-testid="quiz-restart"
          >
            New questions
          </button>
          <Link
            to="/"
            className="rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white"
          >
            Back to the issue
          </Link>
        </div>
      </article>
    );
  }

  const q = questions[index];
  const answered = picked !== null;
  const correct = picked === q.correctAnswerIndex;

  const onPick = (i: number) => {
    if (answered) return;
    setPicked(i);
    if (i === q.correctAnswerIndex) setCorrectCount((c) => c + 1);
    // Record into the global poll and show the updated 60-day moving average.
    void submitQuizResponse(q.id, i)
      .then(setPoll)
      .catch(() => {
        // Fall back to the snapshot stats from the initial load if the write fails.
        setPoll({
          questionId: q.id,
          correctAnswerIndex: q.correctAnswerIndex,
          isCorrect: i === q.correctAnswerIndex,
          responseCount: q.responseCount,
          correctCount: Math.round(q.correctRate * q.responseCount),
          correctRate: q.correctRate,
          windowDays: 60,
        });
      });
  };

  const onNext = () => {
    if (index === questions.length - 1) {
      setDone(true);
    } else {
      setIndex((v) => v + 1);
      setPicked(null);
      setPoll(null);
    }
  };

  return (
    <article className="mx-auto max-w-2xl" data-testid="magazine-quiz">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Civics 101
      </p>
      <h1 className="display mt-2 text-4xl">Test what you know</h1>
      <p
        className="mt-2 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]"
        data-testid="quiz-progress"
      >
        Question {index + 1} of {questions.length} · fresh set every time
      </p>

      <section
        className="mt-8 border border-[var(--border)] bg-[var(--bg-elev)] p-6"
        data-testid="quiz-card"
      >
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
          {q.topic}
        </p>
        <h2 className="display mt-3 text-2xl leading-snug">{q.question}</h2>

        <ul className="mt-6 space-y-3">
          {q.options.map((opt, idx) => {
            const isPicked = picked === idx;
            const isCorrect = idx === q.correctAnswerIndex;
            let cls = "border-[var(--border)] bg-[var(--bg)] text-[var(--fg)]";
            if (answered) {
              if (isCorrect) cls = "border-emerald-500 bg-emerald-50 text-emerald-900";
              else if (isPicked) cls = "border-rose-500 bg-rose-50 text-rose-900";
              else cls = "border-[var(--border)] bg-[var(--bg)] text-[var(--muted)]";
            }
            return (
              <li key={idx}>
                <button
                  type="button"
                  disabled={answered}
                  onClick={() => onPick(idx)}
                  className={`flex w-full items-center justify-between border-2 px-4 py-3 text-left text-base font-medium transition ${cls}`}
                  data-testid={`quiz-option-${idx}`}
                >
                  <span>{opt}</span>
                  {answered && isCorrect && <Check className="h-5 w-5" />}
                  {answered && isPicked && !isCorrect && <X className="h-5 w-5" />}
                </button>
              </li>
            );
          })}
        </ul>

        {answered && (
          <div
            className="mt-6 border-l-4 border-[var(--accent)] bg-[var(--bg)] p-4 text-sm leading-relaxed"
            data-testid="quiz-explanation"
          >
            <p className="font-semibold">{correct ? "Nice." : "Not quite."}</p>
            <p className="mt-1 text-[var(--fg-soft)]">{q.explanation}</p>
            {q.relatedConceptSlug && (
              <p className="mt-2 text-xs uppercase tracking-wider">
                <Link
                  to={`/concepts/${q.relatedConceptSlug}`}
                  className="text-[var(--accent)] underline"
                >
                  Read the concept →
                </Link>
              </p>
            )}
          </div>
        )}

        {/* Global poll: how everyone is doing on this question (60-day moving average). */}
        {answered && poll && (
          <div
            className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4"
            data-testid="quiz-poll"
          >
            <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              <span className="flex items-center gap-1.5">
                <Users size={13} /> Global poll
              </span>
              <span data-testid="quiz-poll-count">
                {poll.responseCount} answer{poll.responseCount === 1 ? "" : "s"} · 60-day avg
              </span>
            </div>
            <div className="mt-2 flex items-baseline gap-2">
              <span
                className="display text-3xl text-[var(--accent)]"
                data-testid="quiz-poll-rate"
              >
                {Math.round(poll.correctRate * 100)}%
              </span>
              <span className="text-sm text-[var(--fg-soft)]">got this right</span>
            </div>
            <div className="mt-2 h-2 w-full overflow-hidden rounded bg-[var(--bg-elev)]">
              <div
                className="h-2 rounded bg-[var(--accent)]"
                style={{ width: `${Math.round(poll.correctRate * 100)}%` }}
              />
            </div>
          </div>
        )}

        <button
          type="button"
          disabled={!answered}
          onClick={onNext}
          className="mt-6 w-full rounded-full bg-[var(--accent)] py-3 text-sm font-semibold text-white disabled:opacity-50"
          data-testid="quiz-next"
        >
          {index === questions.length - 1 ? "See results" : "Next question"}
        </button>
      </section>
    </article>
  );
}
