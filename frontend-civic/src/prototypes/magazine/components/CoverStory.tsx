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
        className="relative flex h-[320px] items-end p-6 text-white md:h-[420px] md:p-10"
        style={{
          background:
            "linear-gradient(135deg, oklch(0.42 0.18 30) 0%, oklch(0.22 0.12 280) 100%)",
        }}
      >
        <div className="absolute right-4 top-4 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] md:right-8 md:top-8">
          Cover story
          <ArrowUpRight className="h-4 w-4 transition group-hover:rotate-45" />
        </div>
        <div className="max-w-3xl">
          <p className="display text-xs font-semibold uppercase tracking-[0.3em]">
            {briefing.institution} · {briefing.status}
          </p>
          <h2 className="display mt-3 text-3xl md:text-6xl">
            {briefing.headline}
          </h2>
          <p className="mt-3 line-clamp-3 max-w-xl text-sm leading-relaxed text-white/90 md:mt-4 md:line-clamp-none md:text-base">
            {briefing.summary30}
          </p>
        </div>
      </div>
    </Link>
  );
}
