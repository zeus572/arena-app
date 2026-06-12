import { ArrowUpRight } from "lucide-react";
import type { BudgetFact } from "@/api/budgetFacts";

/* Editorial "Did You Know?" treatment for a budget contradiction: two columns
   of body text divided by a rule with a "BUT" hinge, in the magazine's
   bordered-section style. Both perspectives are true — the tension is the
   point. */
export function BudgetFactCard({ fact }: { fact: BudgetFact }) {
  return (
    <article className="border border-[var(--border)] bg-[var(--bg-elev)]">
      <header className="flex flex-wrap items-baseline gap-x-3 gap-y-1 border-b border-[var(--border)] px-6 py-4 md:px-8">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Did you know?
        </p>
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          {fact.category}
        </p>
        <h3 className="display w-full text-2xl md:w-auto md:flex-1 md:text-right md:text-xl">
          {fact.tensionLabel}
        </h3>
      </header>

      <div className="relative grid md:grid-cols-2">
        <div className="p-6 md:p-8 md:pr-10">
          <Perspective text={fact.perspectiveA} source={fact.sourceA} url={fact.sourceUrlA} />
        </div>
        <div className="border-t border-[var(--border)] p-6 md:border-l md:border-t-0 md:p-8 md:pl-10">
          <Perspective text={fact.perspectiveB} source={fact.sourceB} url={fact.sourceUrlB} />
        </div>
        <span className="display absolute left-1/2 top-1/2 hidden -translate-x-1/2 -translate-y-1/2 bg-[var(--bg-elev)] px-2 py-1 text-sm font-semibold uppercase tracking-[0.2em] text-[var(--accent)] md:block">
          But
        </span>
      </div>

      {fact.explanation && (
        <footer className="border-t border-[var(--border)] px-6 py-3 md:px-8">
          <p className="text-sm italic leading-relaxed text-[var(--fg-soft)]">
            {fact.explanation}
          </p>
        </footer>
      )}
    </article>
  );
}

function Perspective({ text, source, url }: { text: string; source: string; url: string }) {
  return (
    <>
      <p className="text-base leading-relaxed text-[var(--fg)]">{text}</p>
      {source &&
        (url ? (
          <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-3 inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--accent)]"
          >
            {source} <ArrowUpRight className="h-3 w-3" />
          </a>
        ) : (
          <p className="mt-3 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            {source}
          </p>
        ))}
    </>
  );
}
