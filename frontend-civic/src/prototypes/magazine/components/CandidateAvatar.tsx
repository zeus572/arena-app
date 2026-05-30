import { useState } from "react";
import { cn } from "@/lib/cn";
import type { CandidateSummary } from "@/api/campaign";

// Stylized portrait avatars (illustrated, never photorealistic) live under
// /avatars/<avatarBaseUrl>.png. If one fails to load — or the candidate has
// no avatarBaseUrl yet — we gracefully fall back to a colored-initials disc.
// The initials disc also doubles as the loading state, layered beneath the
// <img> so the page never flashes empty.
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
  candidate: Pick<CandidateSummary, "slug" | "name" | "avatarBaseUrl">;
  size?: number;
  className?: string;
}) {
  const key = candidate.avatarBaseUrl?.trim() || candidate.slug;
  const src = key ? `/avatars/${key}.png` : null;
  const [imgFailed, setImgFailed] = useState(false);

  return (
    <span
      className={cn(
        "relative inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full font-semibold text-white",
        className,
      )}
      style={{
        width: size,
        height: size,
        backgroundColor: colorFor(candidate.slug),
        fontSize: size * 0.4,
      }}
      data-testid="candidate-avatar"
      aria-label={candidate.name}
    >
      {/* Initials sit underneath as the loading + fallback state. */}
      <span aria-hidden className="absolute inset-0 flex items-center justify-center">
        {initials(candidate.name)}
      </span>
      {src && !imgFailed && (
        <img
          src={src}
          alt=""
          loading="lazy"
          onError={() => setImgFailed(true)}
          className="absolute inset-0 h-full w-full object-cover"
        />
      )}
    </span>
  );
}
