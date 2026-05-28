import { Share2 } from "lucide-react";
import type { CivicBriefing } from "@/api/types";

export function SharePreviewCard({ briefing }: { briefing: CivicBriefing }) {
  return (
    <div className="my-10 border border-[var(--border)] bg-[var(--bg-elev)]">
      <div
        className="p-8 text-white"
        style={{
          background:
            "linear-gradient(135deg, oklch(0.45 0.18 25), oklch(0.28 0.14 300))",
        }}
      >
        <p className="display text-xs font-semibold uppercase tracking-[0.3em]">
          Public Lab · Briefing
        </p>
        <p className="display mt-3 text-3xl">{briefing.headline}</p>
        <p className="mt-3 text-sm leading-relaxed text-white/90">
          {briefing.summary30}
        </p>
      </div>
      <div className="flex items-center justify-between border-t border-[var(--border)] px-6 py-3">
        <p className="text-xs font-semibold text-[var(--muted)]">
          Share as a post — preview only
        </p>
        <button className="flex items-center gap-2 rounded-full bg-[var(--accent)] px-4 py-1.5 text-sm font-semibold text-white">
          <Share2 className="h-4 w-4" /> Share
        </button>
      </div>
    </div>
  );
}
