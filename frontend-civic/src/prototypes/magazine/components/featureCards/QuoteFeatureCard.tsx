import { Quote } from "lucide-react";
import type { CivicQuote } from "@/lib/quotes";

/* A quotable from a public figure, sized for one slot of the feature rotator.
   Vertically centered rather than top-aligned: unlike the budget/quiz cards this has
   one short block of content, and hanging it at the top of a tall slot reads as a
   half-empty card. The attribution is pinned to the bottom (mt-auto) so the card
   matches its neighbours' height regardless of how long the quote runs.

   The source line is deliberately shown, not hidden — a quotation a reader can't
   trace is a rumor, and this app is in the business of the opposite. */
export function QuoteFeatureCard({ quote }: { quote: CivicQuote }) {
  // Long quotations get a smaller type size so they still fit the slot without the
  // card growing past its neighbours.
  const size = quote.text.length > 160 ? "text-xl md:text-2xl" : "text-2xl md:text-3xl";

  return (
    <article
      className="flex h-full flex-col border border-[var(--border)] bg-[var(--bg-elev)] p-6"
      data-testid="feature-quote"
    >
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Quotable · {quote.theme}
      </p>

      <div className="flex flex-1 flex-col justify-center py-4">
        <Quote className="h-5 w-5 text-[var(--accent)]" aria-hidden />
        <blockquote
          className={`display mt-3 leading-tight text-[var(--fg)] ${size}`}
          data-testid="feature-quote-text"
        >
          “{quote.text}”
        </blockquote>
      </div>

      <div className="mt-auto pt-2">
        <p className="text-sm font-semibold text-[var(--fg-soft)]">— {quote.speaker}</p>
        <p className="mt-1 text-xs uppercase tracking-wider text-[var(--muted)]">
          {quote.context} · {quote.source}, {quote.year}
        </p>
      </div>
    </article>
  );
}
