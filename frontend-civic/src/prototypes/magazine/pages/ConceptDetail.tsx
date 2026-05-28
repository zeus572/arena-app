import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import type { Concept } from "@/api/types";
import { getConceptBySlug } from "@/api/concepts";

export default function MagazineConceptDetail() {
  const { slug } = useParams<{ slug: string }>();
  const [concept, setConcept] = useState<Concept | null | undefined>(undefined);
  const [picked, setPicked] = useState<string | null>(null);

  useEffect(() => {
    if (!slug) return;
    void getConceptBySlug(slug).then((c) => setConcept(c ?? null));
  }, [slug]);

  if (concept === undefined) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="concept-loading">
        Loading the concept…
      </p>
    );
  }

  if (concept === null) {
    return (
      <article data-testid="concept-not-found">
        <Link
          to="/"
          className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
        >
          ← Back to the issue
        </Link>
        <p className="mt-8 text-lg text-[var(--fg-soft)]">
          We don't have that concept in this issue yet.
        </p>
      </article>
    );
  }

  return (
    <article data-testid="magazine-concept">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to the issue
      </Link>

      <header className="mt-6">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Concept · {concept.category}
        </p>
        <h1 className="display mt-2 text-5xl leading-tight" data-testid="concept-title">
          {concept.title}
        </h1>
        <p className="mt-4 text-lg leading-relaxed text-[var(--fg-soft)]">
          {concept.plainDefinition}
        </p>
      </header>

      <section className="mt-10 border-t border-[var(--border)] pt-6">
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          Why it matters
        </p>
        <p className="mt-3 text-base leading-relaxed">{concept.whyItMatters}</p>
      </section>

      <section className="mt-10 grid gap-8 md:grid-cols-2">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Where you see it
          </p>
          <ul className="mt-3 space-y-2 text-base">
            {concept.whereYouSeeIt.map((w) => (
              <li key={w} className="flex items-start gap-2">
                <span className="mt-2 h-1 w-1 shrink-0 rounded-full bg-[var(--accent)]" />
                <span>{w}</span>
              </li>
            ))}
          </ul>
        </div>
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Common misunderstanding
          </p>
          <p className="mt-3 border-l-4 border-[var(--accent)] bg-[var(--bg-elev)] p-4 text-base leading-relaxed">
            {concept.commonMisunderstanding}
          </p>
        </div>
      </section>

      <section className="mt-10">
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          Current example
        </p>
        <p className="mt-3 text-base leading-relaxed">{concept.currentExample}</p>
      </section>

      {concept.relatedConcepts.length > 0 && (
        <section className="mt-10">
          <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Related concepts
          </p>
          <ul className="mt-3 flex flex-wrap gap-2" data-testid="concept-related">
            {concept.relatedConcepts.map((r) => (
              <li key={r}>
                <Link
                  to={`/concepts/${r}`}
                  className="border border-[var(--border)] bg-[var(--bg-elev)] px-3 py-1 text-xs uppercase tracking-wider text-[var(--fg)]"
                >
                  {r}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section
        className="mt-12 border border-dashed border-[var(--border)] bg-[var(--bg-elev)] p-6"
        data-testid="concept-try-it"
      >
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
          Try it
        </p>
        <p className="mt-2 text-xl font-medium leading-snug">
          {concept.tryItQuestion}
        </p>
        <div className="mt-4 grid gap-2">
          {[
            "A floor vote in both chambers",
            "Just a presidential signature",
            "Nothing — committee approval makes it law",
          ].map((opt) => (
            <button
              key={opt}
              type="button"
              onClick={() => setPicked(opt)}
              className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-left text-sm hover:border-[var(--accent)]"
              data-testid="concept-try-option"
            >
              {opt}
            </button>
          ))}
        </div>
        {picked && (
          <p className="mt-3 text-xs text-[var(--fg-soft)]" data-testid="concept-try-picked">
            You picked: <span className="font-semibold">{picked}</span>. Open
            discussion: how would you defend that?
          </p>
        )}
      </section>
    </article>
  );
}
