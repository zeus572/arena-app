import { Quote } from "lucide-react";
import type { CivicQuote } from "@/lib/quotes";
import { EphemeralReaction } from "./EphemeralReaction";
import { ShortCardShell } from "./ShortCardShell";

/**
 * Full-viewport Shorts card for a quote from a public figure. The quotation carries
 * the card on its own, so unlike the news/bill cards there's no body copy and no deep
 * link — there's nowhere truer to send a reader than the words themselves. The
 * attribution block does the work a CTA usually does: it tells you who said it, when,
 * and which document it's in, so anyone who doubts it can go check.
 *
 * The reaction is the ephemeral kind (local only, no fabricated tally) for the same
 * reason as the think-deeper card: there is no server-side opinion endpoint for a
 * free-form prompt, and inventing one would be worse than offering none.
 */
export function QuoteShortCard({ quote }: { quote: CivicQuote }) {
  // Shorts type is large by default; long quotations step down so they clear the
  // fold on a short phone instead of pushing the react bar into the shell's scroll.
  const size =
    quote.text.length > 220
      ? "text-xl md:text-2xl"
      : quote.text.length > 120
        ? "text-2xl md:text-3xl"
        : "text-3xl md:text-4xl";

  return (
    <ShortCardShell>
      <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
        <Quote className="h-4 w-4" /> Quotable · {quote.theme}
      </p>

      <div className="my-4 flex flex-1 flex-col justify-center">
        <blockquote
          className={`display leading-tight text-[var(--fg)] ${size}`}
          data-testid="short-quote-text"
        >
          “{quote.text}”
        </blockquote>
        <p className="mt-5 text-sm font-semibold text-[var(--fg-soft)]">— {quote.speaker}</p>
        <p className="mt-1 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          {quote.context} · {quote.source}, {quote.year}
        </p>
      </div>

      <EphemeralReaction
        prompt="Does that still hold up?"
        options={[
          { key: "holds", label: "Still true" },
          { key: "dated", label: "Not anymore" },
        ]}
        testId="short-quote-react"
      />
    </ShortCardShell>
  );
}
