import { cn } from "@/lib/cn";
import type { CandidateSummary } from "@/api/campaign";

// Stylized initials avatar — deliberately illustrative, never photorealistic,
// so candidates read as clearly fictional. Color is derived from the slug.
const PALETTE = [
  "#6366f1",
  "#0891b2",
  "#16a34a",
  "#d97706",
  "#dc2626",
  "#9333ea",
  "#0d9488",
  "#db2777",
];

function colorFor(slug: string): string {
  let hash = 0;
  for (let i = 0; i < slug.length; i++) hash = (hash * 31 + slug.charCodeAt(i)) | 0;
  return PALETTE[Math.abs(hash) % PALETTE.length];
}

function initials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? "")
    .join("");
}

export function CandidateAvatar({
  candidate,
  size = 44,
  className,
}: {
  candidate: Pick<CandidateSummary, "slug" | "name">;
  size?: number;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center justify-center rounded-full font-semibold text-white",
        className,
      )}
      style={{
        width: size,
        height: size,
        backgroundColor: colorFor(candidate.slug),
        fontSize: size * 0.4,
      }}
      aria-hidden
      data-testid="candidate-avatar"
    >
      {initials(candidate.name)}
    </span>
  );
}
