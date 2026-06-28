// A small per-source moniker shown on feed cards so readers can tell at a glance
// which outlet a briefing was synthesized from (NPR vs BBC vs a local paper).

type SourceStyle = { label: string; dot: string };

// Curated styling for the known feeds. The label keeps local outlets short enough
// to sit inline; anything not listed falls back to its raw name with a neutral dot.
const SOURCE_STYLES: Record<string, SourceStyle> = {
  NPR: { label: "NPR", dot: "bg-[#d7372e]" },
  BBC: { label: "BBC", dot: "bg-[#b80000]" },
  "Washington State Standard": { label: "WA Standard", dot: "bg-[#1f7a4d]" },
  "Cascade PBS": { label: "Cascade PBS", dot: "bg-[#0a7ea4]" },
  "Maryland Matters": { label: "MD Matters", dot: "bg-[#8a5a00]" },
  CalMatters: { label: "CalMatters", dot: "bg-[#7a3da6]" },
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
