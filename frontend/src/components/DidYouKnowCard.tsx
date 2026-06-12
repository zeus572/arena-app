import type { BudgetFact } from "@/api/types";
import { useTilt } from "@/hooks/useTilt";
import { Lightbulb, ExternalLink } from "lucide-react";

/* Two-panel "Did You Know?" card showing a budget contradiction: both
   perspectives are true, but they pull in opposite directions. Spans 2 grid
   columns (the caller applies the span class, same as QuoteCard). */
export function DidYouKnowCard({ fact }: { fact: BudgetFact }) {
  const tiltRef = useTilt<HTMLDivElement>({ maxTiltDeg: 4 });

  return (
    <div ref={tiltRef} className="feed-tilt-root h-full">
      <article className="feed-tilt-glow relative rounded-xl overflow-hidden border border-amber-500/20 h-full flex flex-col min-h-[200px]">
        {/* Header strip */}
        <div className="flex items-center gap-2 px-4 pt-3 pb-2 bg-[oklch(0.18_0.04_240)]">
          <span className="flex items-center gap-1 rounded-full bg-amber-500/15 text-amber-400 px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider">
            <Lightbulb size={9} /> Did You Know?
          </span>
          <span className="rounded-full bg-white/10 text-white/60 px-2 py-0.5 text-[9px] font-medium uppercase tracking-wider">
            {fact.category}
          </span>
          <h3 className="ml-auto text-[11px] font-bold text-white/85 truncate">
            {fact.tensionLabel}
          </h3>
        </div>

        {/* Two perspectives */}
        <div className="relative grid grid-cols-1 sm:grid-cols-2 flex-1">
          <div className="p-4 pb-5" style={{ background: "oklch(0.22 0.10 195)" }}>
            <p className="text-[12px] leading-relaxed text-white/90">{fact.perspectiveA}</p>
            {fact.sourceA && (
              <SourceLink source={fact.sourceA} url={fact.sourceUrlA} />
            )}
          </div>
          <div className="p-4 pb-5" style={{ background: "oklch(0.22 0.10 55)" }}>
            <p className="text-[12px] leading-relaxed text-white/90">{fact.perspectiveB}</p>
            {fact.sourceB && (
              <SourceLink source={fact.sourceB} url={fact.sourceUrlB} />
            )}
          </div>
          {/* "BUT" divider — centered on the seam (vertical on desktop,
              horizontal on mobile where panels stack) */}
          <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-10">
            <span className="block rounded-full bg-black/60 border border-white/20 text-white/90 font-black text-[10px] px-2.5 py-1 backdrop-blur-sm">
              BUT
            </span>
          </div>
        </div>

        {/* Explanation footer */}
        {fact.explanation && (
          <div className="px-4 py-2.5 bg-[oklch(0.16_0.03_240)] border-t border-white/5">
            <p className="text-[10px] leading-snug text-white/55 italic">{fact.explanation}</p>
          </div>
        )}
      </article>
    </div>
  );
}

function SourceLink({ source, url }: { source: string; url: string }) {
  if (!url) {
    return <p className="mt-2 text-[9px] uppercase tracking-wider text-white/40">{source}</p>;
  }
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      className="mt-2 inline-flex items-center gap-1 text-[9px] uppercase tracking-wider text-white/40 hover:text-white/80 transition-colors no-underline"
    >
      {source} <ExternalLink size={8} />
    </a>
  );
}
