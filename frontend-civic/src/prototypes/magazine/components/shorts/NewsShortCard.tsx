import { Link } from "react-router-dom";
import { Newspaper } from "lucide-react";
import type { CivicBriefingSummary } from "@/api/types";
import { EphemeralReaction } from "./EphemeralReaction";
import { ShortCardShell } from "./ShortCardShell";

/**
 * Full-viewport Shorts card for a news-sourced briefing — the "from the news" fact card.
 * Leads with the upstream publisher and the headline (the factual hook), then the 30-second
 * summary, an ephemeral gut-check, and a tap into the full briefing.
 */
export function NewsShortCard({ briefing }: { briefing: CivicBriefingSummary }) {
  return (
    <ShortCardShell>
      <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
        <Newspaper className="h-4 w-4" /> In the news
        {briefing.sourcePublisher ? ` · ${briefing.sourcePublisher}` : ""}
      </p>

      <div className="my-4 flex flex-1 flex-col justify-center">
        <h2
          className="display text-3xl leading-tight text-[var(--fg)] md:text-4xl"
          data-testid="short-news-headline"
        >
          {briefing.headline}
        </h2>
        {briefing.summary30?.trim() && (
          <p className="mt-4 text-base leading-relaxed text-[var(--fg-soft)]">
            {briefing.summary30}
          </p>
        )}
      </div>

      <EphemeralReaction
        prompt="Did you know this?"
        options={[
          { key: "news-to-me", label: "News to me" },
          { key: "following", label: "Been following" },
        ]}
        testId="short-news-react"
      />
      <Link
        to={`/briefings/${briefing.slug}`}
        className="mt-3 self-end text-sm font-semibold text-[var(--accent)] hover:underline"
        data-testid="short-news-open"
      >
        Get the full briefing →
      </Link>
    </ShortCardShell>
  );
}
