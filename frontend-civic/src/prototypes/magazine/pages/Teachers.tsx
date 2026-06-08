import { Link } from "react-router-dom";

const trustSignals = [
  {
    title: "Sourced",
    body: "Every briefing links to primary records — congressional pages, court documents, agency notices.",
  },
  {
    title: "Balanced",
    body: "Strongest arguments on both sides. Values labeled. No editorial position.",
  },
  {
    title: "Age-appropriate",
    body: "Each briefing carries an audience label (Middle School / High School / College).",
  },
];

const classroomPrompts = [
  "Name two values in conflict in this story, and one that the article didn't mention.",
  "Identify the branch of government that acted, and one branch that could respond.",
  "Rewrite the headline in your own words, then write a counter-headline.",
];

const parentStarters = [
  "What did you read or watch today that you weren't sure was true?",
  "Who do you think has the most power in this story — and why?",
  "Can you steelman the side you disagree with?",
];

export default function MagazineTeachers() {
  return (
    <article data-testid="magazine-teachers">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to the issue
      </Link>

      <header className="mt-6">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          For teachers and parents
        </p>
        <h1 className="display mt-2 text-5xl leading-tight">
          Built for the classroom and the dinner table.
        </h1>
        <p className="mt-4 text-lg leading-relaxed text-[var(--fg-soft)]">
          Civersify is designed to make current events teachable without making
          them partisan. Here's how we approach sourcing, perspective, and
          age-appropriateness.
        </p>
      </header>

      <section
        className="mt-12 grid gap-6 md:grid-cols-3"
        data-testid="teachers-trust-signals"
      >
        {trustSignals.map((s) => (
          <div
            key={s.title}
            className="border border-[var(--border)] bg-[var(--bg-elev)] p-5"
          >
            <h3 className="display text-xl">{s.title}</h3>
            <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">
              {s.body}
            </p>
          </div>
        ))}
      </section>

      <section className="mt-12" data-testid="teachers-classroom-prompts">
        <h2 className="display border-b border-[var(--border)] pb-3 text-2xl">
          Classroom discussion prompts
        </h2>
        <p className="mt-4 text-base leading-relaxed text-[var(--fg-soft)]">
          Use any briefing with prompts like these. Each one targets analysis,
          institutional reasoning, or perspective-taking.
        </p>
        <ol className="mt-5 space-y-3 text-base">
          {classroomPrompts.map((p, i) => (
            <li key={i} className="grid grid-cols-[2rem_1fr] gap-3">
              <span className="display text-lg font-bold text-[var(--muted)]">
                {i + 1}.
              </span>
              <span>{p}</span>
            </li>
          ))}
        </ol>
      </section>

      <section className="mt-12" data-testid="teachers-parent-starters">
        <h2 className="display border-b border-[var(--border)] pb-3 text-2xl">
          Parent conversation starters
        </h2>
        <ol className="mt-5 space-y-3 text-base">
          {parentStarters.map((p, i) => (
            <li key={i} className="grid grid-cols-[2rem_1fr] gap-3">
              <span className="display text-lg font-bold text-[var(--muted)]">
                {i + 1}.
              </span>
              <span>{p}</span>
            </li>
          ))}
        </ol>
      </section>

      <section className="mt-12 border border-dashed border-[var(--border)] bg-[var(--bg-elev)] p-6">
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
          Printable guide preview
        </p>
        <p className="display mt-2 text-xl">A one-page handout per briefing.</p>
        <p className="mt-2 text-base text-[var(--fg-soft)]">
          Front: 30-second summary, who acted, words to know, three prompts.
          Back: source links and a homework extension. Printable from any
          briefing page.
        </p>
      </section>
    </article>
  );
}
