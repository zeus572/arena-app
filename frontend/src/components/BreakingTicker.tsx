import { useMemo } from "react";
import { Link } from "react-router-dom";
import { Lightbulb, Radio } from "lucide-react";

import type { DebateSummary } from "@/api/types";

interface BreakingTickerProps {
  debates: DebateSummary[];
}

interface Item {
  id: string;
  debateId: string;
  kind: "live" | "aha";
  text: string;
}

export function BreakingTicker({ debates }: BreakingTickerProps) {
  const items: Item[] = useMemo(() => {
    const live: Item[] = debates
      .filter((d) => d.status === "Active" || d.status === "Compromising")
      .slice(0, 4)
      .map((d) => ({ id: `live-${d.id}`, debateId: d.id, kind: "live", text: d.topic }));
    const aha: Item[] = debates
      .filter((d) => d.topQuote && d.topQuote.insightfulCount >= 1)
      .sort(
        (a, b) =>
          (b.topQuote?.insightfulCount ?? 0) * 10 +
          (b.topQuote?.reactionCount ?? 0) -
          ((a.topQuote?.insightfulCount ?? 0) * 10 + (a.topQuote?.reactionCount ?? 0)),
      )
      .slice(0, 5)
      .map((d) => ({
        id: `aha-${d.id}`,
        debateId: d.id,
        kind: "aha",
        text: d.topQuote!.text,
      }));

    const merged: Item[] = [];
    const max = Math.max(live.length, aha.length);
    for (let i = 0; i < max; i++) {
      if (live[i]) merged.push(live[i]);
      if (aha[i]) merged.push(aha[i]);
    }
    return merged;
  }, [debates]);

  if (items.length === 0) return null;

  // Duplicate the list so the marquee loop is seamless.
  const loop = [...items, ...items];

  return (
    <div className="feed-breaking-ticker relative mb-3 overflow-hidden rounded-full border border-border bg-card/60 backdrop-blur-sm">
      <div className="absolute left-0 top-0 bottom-0 z-10 w-12 bg-gradient-to-r from-card to-transparent pointer-events-none" />
      <div className="absolute right-0 top-0 bottom-0 z-10 w-12 bg-gradient-to-l from-card to-transparent pointer-events-none" />
      <div className="feed-breaking-ticker-track flex gap-6 whitespace-nowrap py-1.5 pl-4">
        {loop.map((item, i) => (
          <Link
            key={`${item.id}-${i}`}
            to={`/debates/${item.debateId}`}
            className="no-underline inline-flex items-center gap-1.5 text-[11px] text-muted-foreground hover:text-foreground transition-colors"
          >
            {item.kind === "live" ? (
              <span className="inline-flex items-center gap-1 text-red-500 font-bold uppercase tracking-wider text-[9px]">
                <Radio size={9} className="animate-pulse" /> Live
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 text-amber-500 font-bold uppercase tracking-wider text-[9px]">
                <Lightbulb size={9} /> Aha
              </span>
            )}
            <span className="font-medium truncate max-w-[60ch]">
              {item.kind === "aha" ? `"${item.text}"` : item.text}
            </span>
            <span className="text-muted-foreground/30">·</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
