import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button, ButtonLink } from "../components/Button";
import { getQuestions, type Question } from "@/api/questions";
import {
  submitAnswer,
  type AnswerConfidence,
  type AnswerIntensity,
} from "@/api/answers";

const CONFIDENCE_OPTIONS: { value: AnswerConfidence; label: string }[] = [
  { value: "NotSure", label: "Not sure" },
  { value: "SomewhatSure", label: "Somewhat sure" },
  { value: "VerySure", label: "Very sure" },
];

const INTENSITY_OPTIONS: { value: AnswerIntensity; label: string }[] = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
  { value: "NonNegotiable", label: "Non-negotiable" },
];

export default function MagazineOnboarding() {
  const [questions, setQuestions] = useState<Question[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [index, setIndex] = useState(0);
  const [choiceKey, setChoiceKey] = useState<string | null>(null);
  const [confidence, setConfidence] = useState<AnswerConfidence>("SomewhatSure");
  const [intensity, setIntensity] = useState<AnswerIntensity>("Medium");
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    void getQuestions({ type: "simple_pairing", take: 10 })
      .then((qs) => setQuestions(qs))
      .finally(() => setLoaded(true));
  }, []);

  function resetForm() {
    setChoiceKey(null);
    setConfidence("SomewhatSure");
    setIntensity("Medium");
  }

  async function next() {
    const q = questions[index];
    if (!q || choiceKey === null) return;

    setSubmitting(true);
    try {
      await submitAnswer({
        questionId: q.id,
        selectedChoiceKey: choiceKey,
        confidence,
        intensity,
      });
    } finally {
      setSubmitting(false);
    }

    if (index + 1 >= questions.length) {
      setDone(true);
      return;
    }
    setIndex(index + 1);
    resetForm();
  }

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading the question set…
      </p>
    );
  }

  if (questions.length === 0) {
    return (
      <p className="py-12 text-base text-[var(--muted)]">
        No questions are available yet.
      </p>
    );
  }

  if (done) {
    return (
      <article
        className="mx-auto max-w-3xl py-16 text-center"
        data-testid="onboarding-done"
      >
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Profile updated
        </p>
        <h1 className="display mt-3 text-5xl">Thank you.</h1>
        <p className="mt-6 text-lg leading-relaxed text-[var(--fg-soft)]">
          We've recorded {questions.length} of your answers. Your Civic Compass
          will reflect them.
        </p>
        <div className="mt-8 flex justify-center gap-4">
          <Button
            onClick={() => navigate("/profile")}
            data-testid="see-profile"
          >
            See your profile
          </Button>
          <ButtonLink to="/" variant="ghost">
            Back to the issue
          </ButtonLink>
        </div>
      </article>
    );
  }

  const q = questions[index];
  const progress = `Question ${index + 1} of ${questions.length}`;

  return (
    <article className="mx-auto max-w-3xl" data-testid="onboarding">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mt-8">
        <p
          className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]"
          data-testid="progress"
        >
          {progress}
        </p>
        {q.topic && (
          <p className="mt-2 text-xs uppercase tracking-wider text-[var(--muted)]">
            {q.topic}
          </p>
        )}
        <h1 className="display mt-3 text-4xl md:text-5xl">{q.prompt}</h1>
      </header>

      <section className="mt-10 space-y-4" data-testid="choices">
        {q.choices.map((c) => {
          const selected = c.key === choiceKey;
          return (
            <button
              key={c.key}
              type="button"
              onClick={() => setChoiceKey(c.key)}
              data-testid={`choice-${c.key}`}
              data-selected={selected}
              className={`block w-full border-2 px-6 py-5 text-left text-lg leading-relaxed transition ${
                selected
                  ? "border-[var(--accent)] bg-[var(--accent)] text-white"
                  : "border-[var(--border)] bg-[var(--bg-elev)] text-[var(--fg)] hover:border-[var(--accent)]"
              }`}
            >
              <span className="display mr-3 text-sm font-semibold">{c.key}.</span>
              {c.label}
            </button>
          );
        })}
      </section>

      <section className="mt-10 grid gap-6 md:grid-cols-2">
        <div>
          <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
            How sure are you?
          </p>
          <div
            className="mt-3 flex flex-wrap gap-2"
            data-testid="confidence-options"
          >
            {CONFIDENCE_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                onClick={() => setConfidence(o.value)}
                data-testid={`confidence-${o.value}`}
                data-selected={confidence === o.value}
                className={`rounded-full border-2 px-4 py-1.5 text-sm font-semibold transition ${
                  confidence === o.value
                    ? "border-[var(--accent)] bg-[var(--accent)] text-white"
                    : "border-[var(--border)] bg-[var(--bg-elev)] text-[var(--fg)]"
                }`}
              >
                {o.label}
              </button>
            ))}
          </div>
        </div>

        <div>
          <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
            How important is this to you?
          </p>
          <div
            className="mt-3 flex flex-wrap gap-2"
            data-testid="intensity-options"
          >
            {INTENSITY_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                onClick={() => setIntensity(o.value)}
                data-testid={`intensity-${o.value}`}
                data-selected={intensity === o.value}
                className={`rounded-full border-2 px-4 py-1.5 text-sm font-semibold transition ${
                  intensity === o.value
                    ? "border-[var(--accent)] bg-[var(--accent)] text-white"
                    : "border-[var(--border)] bg-[var(--bg-elev)] text-[var(--fg)]"
                }`}
              >
                {o.label}
              </button>
            ))}
          </div>
        </div>
      </section>

      <div className="mt-12 flex items-center justify-between">
        <p className="text-xs text-[var(--muted)]">
          {choiceKey === null
            ? "Pick A or B to continue."
            : "Adjust confidence/intensity if you'd like, then continue."}
        </p>
        <Button
          onClick={next}
          disabled={choiceKey === null || submitting}
          data-testid="next-button"
        >
          {index + 1 === questions.length ? "Finish" : "Next question"}
        </Button>
      </div>
    </article>
  );
}
