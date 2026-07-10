// A small per-source moniker shown on feed cards so readers can tell at a glance
// which outlet a briefing was synthesized from (NPR vs a local paper vs the real
// outlet behind a Google News channel).

type SourceStyle = { label: string; dot: string };

const GOOGLE_NEWS_DOT = "bg-[#4285f4]";

// Curated styling for the known feeds. The label keeps local outlets short enough
// to sit inline; anything not listed falls back to its raw name with a neutral dot
// (that fallback is the normal path for aggregator items, whose badge shows the
// real publisher, e.g. "Reuters"). BBC and Cascade PBS are no longer ingested but
// keep their styles so briefings generated from them still render nicely.
const SOURCE_STYLES: Record<string, SourceStyle> = {
  NPR: { label: "NPR", dot: "bg-[#d7372e]" },
  BBC: { label: "BBC", dot: "bg-[#b80000]" },
  "Washington State Standard": { label: "WA Standard", dot: "bg-[#1f7a4d]" },
  "Cascade PBS": { label: "Cascade PBS", dot: "bg-[#0a7ea4]" },
  "Maryland Matters": { label: "MD Matters", dot: "bg-[#8a5a00]" },
  CalMatters: { label: "CalMatters", dot: "bg-[#7a3da6]" },
  // Google News channel names — shown only when an item carried no publisher.
  "Google News": { label: "Google News", dot: GOOGLE_NEWS_DOT },
  "Google News Politics": { label: "GN Politics", dot: GOOGLE_NEWS_DOT },
  "Google News U.S.": { label: "GN U.S.", dot: GOOGLE_NEWS_DOT },
  "Google News WA": { label: "GN Washington", dot: GOOGLE_NEWS_DOT },
  "Google News MD": { label: "GN Maryland", dot: GOOGLE_NEWS_DOT },
  "Google News CA": { label: "GN California", dot: GOOGLE_NEWS_DOT },
};

export function SourceBadge({
  source,
  className = "",
}: {
  source: string | null | undefined;
  className?: string;
}) {
  if (!source) return null;
  const style = SOURCE_STYLES[source] ?? { label: source, dot: "bg-[var(--muted)]" };

  return (
    <span
      className={`inline-flex items-center gap-1 text-[0.65rem] font-semibold uppercase tracking-[0.15em] text-[var(--muted)] ${className}`}
      data-testid={`briefing-source-${source}`}
      title={`Source: ${source}`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} aria-hidden="true" />
      {style.label}
    </span>
  );
}
