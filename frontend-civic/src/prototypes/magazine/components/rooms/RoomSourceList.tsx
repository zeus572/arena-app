import { useEffect, useState } from "react";

import { getRoomSources, type RoomSources as RoomSourcesData } from "@/api/rooms";

/**
 * Everything a room rests on, grouped by source type (design 1l).
 *
 * Shared by Theme and Story rooms because the honesty is the same in both: the list is
 * derived from the graph, so it cannot drift out of step with the evidence on the page, and
 * it says how many sources we hold the full text of rather than implying we hold all of them.
 */

/** Plain-language names for the SourceType enum; the member names are for us, not readers. */
const SOURCE_TYPE_LABEL: Record<string, string> = {
  PrimaryDocument: "Primary documents",
  GovernmentData: "Government data",
  DirectStatement: "Direct statements",
  Reporting: "Reporting",
  Analysis: "Analysis",
  PublicReaction: "Public reaction",
};

export function RoomSourceList({ slug }: { slug: string }) {
  const [sources, setSources] = useState<RoomSourcesData | null>(null);

  useEffect(() => {
    let alive = true;
    // Degrades on its own: a methodology section that cannot list its sources still has a
    // scope statement, a terminology guide and the mark key worth reading.
    getRoomSources(slug)
      .then((s) => alive && setSources(s))
      .catch(() => alive && setSources(null));
    return () => {
      alive = false;
    };
  }, [slug]);

  if (!sources || sources.total === 0) return null;

  return (
    <div data-testid="room-source-list">
      <div className="flex flex-wrap items-baseline gap-x-6 gap-y-1 border-t-2 border-[var(--fg)] pt-3">
        <p className="display text-[26px]">{sources.total}</p>
        <p className="text-[14px] text-[var(--fg-soft)]">
          sources behind this room, across {sources.groups.length}{" "}
          {sources.groups.length === 1 ? "type" : "types"}.
        </p>
        {/* Usually a small fraction, and saying so is the whole point. */}
        <p className="text-[13px] text-[var(--muted)]">
          We hold the full text of {sources.fullTextHeldCount}. For the rest we have a
          headline and a summary, so they corroborate a claim rather than being something we
          quoted from.
        </p>
      </div>

      <div className="mt-5 grid gap-6 md:grid-cols-2">
        {sources.groups.map((g) => (
          <div key={g.sourceType} data-testid={`source-group-${g.sourceType}`}>
            <h3 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
              {SOURCE_TYPE_LABEL[g.sourceType] ?? g.sourceType} · {g.count}
            </h3>
            <ul className="mt-2 flex flex-col">
              {g.sources.map((s) => (
                <li key={s.id} className="border-t border-[var(--border)] py-2">
                  <a
                    href={s.url}
                    target="_blank"
                    rel="noreferrer"
                    className="text-[14px] leading-snug underline"
                  >
                    {s.title}
                  </a>
                  <p className="mt-0.5 text-[12px] text-[var(--muted)]">
                    {[
                      s.organization,
                      s.publishedAt ? new Date(s.publishedAt).toLocaleDateString() : null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </p>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </div>
  );
}
