import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Check, X, Users } from "lucide-react";
import {
  submitQuizResponse,
  type QuizQuestion,
  type QuizPollResult,
} from "@/api/quiz";

/* In-box civics quiz for the feature rotator. One question at a time; on answer it
   records the response and reveals the live 60-day global poll. While a question is
   answered we ask the rotator to pause (onLockChange) so it won't swap the card out
   from under someone reading the result; "Try another" advances within the set. */
export function QuizFeatureCard({
  questions,
  onLockChange,
}: {
  questions: QuizQuestion[];
  onLockChange: (locked: boolean) => void;
}) {
  const [index, setIndex] = useState(0);
  const [picked, setPicked] = useState<number | null>(null);
  const [poll, setPoll] = useState<QuizPollResult | null>(null);

  // Release the rotator lock if this card ever unmounts mid-answer.
  useEffect(() => () => onLockChange(false), [onLockChange]);

  const q = questions[index % questions.length];
  const answered = picked !== null;
  const correct = picked === q.correctAnswerIndex;

  const onPick = (i: number) => {
    if (answered) return;
    setPicked(i);
    onLockChange(true);
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

  const onAnother = () => {
    onLockChange(false);
    setPicked(null);
    setPoll(null);
    setIndex((v) => (v + 1) % questions.length);
  };

  return (
    <article
      className="flex h-full flex-col border border-[var(--border)] bg-[var(--bg-elev)] p-6"
      data-testid="feature-quiz"
    >
      <p className="flex items-center justify-between text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        <span>Civics 101 · Quick quiz</span>
        <span className="tracking-wider text-[var(--muted)]">{q.topic}</span>
      </p>
      <h2 className="display mt-2 text-2xl leading-snug">{q.question}</h2>

      <ul className="mt-4 space-y-2">
        {q.options.map((opt, idx) => {
          const isPicked = picked === idx;
          const isCorrect = idx === q.correctAnswerIndex;
          let cls = "border-[var(--border)] bg-[var(--bg)] text-[var(--fg)] hover:border-[var(--accent)]";
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
                className={`flex w-full items-center justify-between border-2 px-4 py-2.5 text-left text-sm font-medium transition ${cls}`}
                data-testid={`feature-quiz-option-${idx}`}
              >
                <span>{opt}</span>
                {answered && isCorrect && <Check className="h-4 w-4" />}
                {answered && isPicked && !isCorrect && <X className="h-4 w-4" />}
              </button>
            </li>
          );
        })}
      </ul>

      {answered && (
        <div className="mt-4 border-l-4 border-[var(--accent)] bg-[var(--bg)] p-3 text-sm leading-relaxed">
          <p className="font-semibold">{correct ? "Nice." : "Not quite."}</p>
          <p className="mt-1 text-[var(--fg-soft)]">{q.explanation}</p>
          {q.relatedConceptSlug && (
            <Link
              to={`/concepts/${q.relatedConceptSlug}`}
              className="mt-2 inline-block text-xs font-semibold uppercase tracking-wider text-[var(--accent)] underline"
            >
              Read the concept →
            </Link>
          )}
        </div>
      )}

      {answered && poll && (
        <div className="mt-3 border border-[var(--border)] bg-[var(--bg)] p-3" data-testid="feature-quiz-poll">
          <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            <span className="flex items-center gap-1.5">
              <Users size={13} /> Global poll
            </span>
            <span>
              {poll.responseCount} answer{poll.responseCount === 1 ? "" : "s"} · 60-day avg
            </span>
          </div>
          <div className="mt-1.5 flex items-baseline gap-2">
            <span className="display text-2xl text-[var(--accent)]">
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

      <div className="mt-auto pt-4">
        {answered ? (
          <button
            type="button"
            onClick={onAnother}
            className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)] hover:underline"
            data-testid="feature-quiz-another"
          >
            Try another →
          </button>
        ) : (
          <Link
            to="/quizzes"
            className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--accent)]"
          >
            Take the full quiz →
          </Link>
        )}
      </div>
    </article>
  );
}
