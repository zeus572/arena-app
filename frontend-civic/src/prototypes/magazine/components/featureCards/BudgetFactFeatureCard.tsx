import { ArrowUpRight } from "lucide-react";
import type { BudgetFact } from "@/api/budgetFacts";

/* Single-slot "Did you know?" budget contradiction. Compact, vertical variant of
   BudgetFactCard tuned to fit one column of the feature rotator: two perspectives
   stacked across a "BUT" hinge, with source links pinned to the bottom so the card
   matches its neighbour's height. Both perspectives are true — the tension is the point. */
export function BudgetFactFeatureCard({ fact }: { fact: BudgetFact }) {
  return (
    <article
      className="flex h-full flex-col border border-[var(--border)] bg-[var(--bg-elev)] p-6"
      data-testid="feature-budget-fact"
    >
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Did you know? · {fact.category}
      </p>
      <h2 className="display mt-2 text-2xl">{fact.tensionLabel}</h2>

      <div className="mt-4 space-y-2 text-sm leading-relaxed text-[var(--fg)]">
        <p>{fact.perspectiveA}</p>
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">But</p>
        <p>{fact.perspectiveB}</p>
      </div>

      {fact.explanation && (
        <p className="mt-3 text-sm italic leading-relaxed text-[var(--fg-soft)]">
          {fact.explanation}
        </p>
      )}

      <div className="mt-auto flex flex-wrap gap-x-5 gap-y-1 pt-4">
        <SourceLink label={fact.sourceA} url={fact.sourceUrlA} />
        <SourceLink label={fact.sourceB} url={fact.sourceUrlB} />
      </div>
    </article>
  );
}

function SourceLink({ label, url }: { label: string; url: string }) {
  if (!label) return null;
  if (!url) {
    return (
      <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        {label}
      </span>
    );
  }
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--accent)]"
    >
      {label} <ArrowUpRight className="h-3 w-3" />
    </a>
  );
}
