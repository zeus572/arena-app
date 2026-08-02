import { useMemo } from "react";
import { quoteOfDay } from "@/lib/quotes";

/**
 * A single quotable line in the site footer — the small, quiet placement, seen mostly at
 * the bottom of short pages (the daily games, a settings screen) where the footer is
 * actually in view.
 *
 * This is the quote OF THE DAY, not a rotating one: the footer is on every page in the
 * magazine layout, and a line that changed as you navigated would read as a glitch rather
 * than as freshness. It's stable for the reader's local calendar day and walks the whole
 * library before repeating, so it's months between encores.
 *
 * `maxLength` is the reason this is a separate concern from the other two placements. The
 * footer has one line to work with, and the library holds some long quotations (Humphrey's
 * moral-test line runs to 250 characters). Rather than clip someone's words — a clipped
 * quotation is a misquotation — quoteOfDay walks on to the next quote that fits.
 */
const FOOTER_MAX_LENGTH = 120;

export function FooterQuote() {
  // Pinned for the life of the mount so a re-render can't swap the line mid-read.
  const quote = useMemo(() => quoteOfDay({ maxLength: FOOTER_MAX_LENGTH }), []);

  return (
    <figure className="mx-auto max-w-xl text-center" data-testid="footer-quote">
      <blockquote className="text-sm italic leading-relaxed text-[var(--fg-soft)]">
        “{quote.text}”
      </blockquote>
      <figcaption className="mt-1.5 text-[0.65rem] font-semibold uppercase tracking-wider text-[var(--muted)]">
        {quote.speaker} · {quote.year}
      </figcaption>
    </figure>
  );
}
