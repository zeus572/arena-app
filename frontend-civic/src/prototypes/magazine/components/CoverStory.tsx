import { Link } from "react-router-dom";
import { ArrowUpRight } from "lucide-react";
import type { CivicBriefingSummary } from "@/api/types";

export function CoverStory({ briefing }: { briefing: CivicBriefingSummary }) {
  return (
    <Link
      to={`/briefings/${briefing.slug}`}
      className="group block overflow-hidden border border-[var(--border)]"
    >
      <div
        className="relative flex h-[360px] items-end p-8 text-white md:h-[460px] md:p-16"
        style={{
          background:
            "linear-gradient(135deg, oklch(0.42 0.18 30) 0%, oklch(0.22 0.12 280) 100%)",
        }}
      >
        <div className="absolute right-6 top-6 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] md:right-10 md:top-10">
          Cover story
          <ArrowUpRight className="h-4 w-4 transition group-hover:rotate-45" />
        </div>
        <div className="min-w-0 max-w-3xl">
          <p className="display text-xs font-semibold uppercase tracking-[0.3em]">
            {briefing.institution} · {briefing.status}
            {briefing.sourcePublisher && (
              <span
                className="ml-2 font-normal text-white/75"
                data-testid={`cover-source-${briefing.sourcePublisher}`}
              >
                · {briefing.sourcePublisher}
              </span>
            )}
          </p>
          <h2 className="display mt-3 text-3xl [overflow-wrap:anywhere] hyphens-auto md:text-6xl">
            {briefing.headline}
          </h2>
          <p className="mt-3 line-clamp-3 max-w-xl text-sm leading-relaxed text-white/90 [overflow-wrap:anywhere] md:mt-4 md:line-clamp-2 md:text-base">
            {briefing.summary30}
          </p>
        </div>
      </div>
    </Link>
  );
}
