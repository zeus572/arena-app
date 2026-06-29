import { Link } from "react-router-dom";
import { Lightbulb } from "lucide-react";
import type { CivicBriefingSummary } from "@/api/types";
import { EphemeralReaction } from "./EphemeralReaction";

/**
 * Full-viewport Shorts card built from a briefing's "think deeper" question — a
 * thought-provoking prompt that informs and invites reflection. There's no server
 * opinion endpoint for free-form prompts, so the quick interaction is an ephemeral
 * gut-check; the real depth is one tap away in the full briefing.
 */
export function ThinkDeeperShortCard({
  briefing,
}: {
  briefing: CivicBriefingSummary;
}) {
  return (
    <div className="mx-auto flex h-full w-full max-w-xl flex-col px-5 pb-8 pt-20">
      <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
        <Lightbulb className="h-4 w-4" /> Think deeper
        {briefing.keyConcept ? ` · ${briefing.keyConcept}` : ""}
      </p>

      <div className="my-4 flex flex-1 flex-col justify-center">
        <blockquote
          className="display text-3xl leading-tight text-[var(--fg)] md:text-4xl"
          data-testid="short-thinkdeeper-question"
        >
          “{briefing.thinkDeeperQuestion}”
        </blockquote>
        <p className="mt-5 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          From: {briefing.headline}
          {briefing.sourcePublisher ? ` · ${briefing.sourcePublisher}` : ""}
        </p>
      </div>

      <EphemeralReaction
        prompt="Where do you land?"
        options={[
          { key: "rethinking", label: "Rethinking it" },
          { key: "standing", label: "Standing firm" },
        ]}
        testId="short-thinkdeeper-react"
      />
      <Link
        to={`/briefings/${briefing.slug}`}
        className="mt-3 self-end text-sm font-semibold text-[var(--accent)] hover:underline"
        data-testid="short-thinkdeeper-open"
      >
        Read the briefing →
      </Link>
    </div>
  );
}
