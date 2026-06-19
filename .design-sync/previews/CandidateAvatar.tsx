import { CandidateAvatar } from "frontend-civic";

// Colored-initials disc avatars. The illustrated PNGs live under
// /avatars/<base>.png and won't resolve in the preview, so each falls back to
// its deterministic colored-initials disc (the intended graceful fallback).
// The disc color is hashed from the slug, so each candidate is stable.

const maria = { slug: "maria-chen", name: "Maria Chen", avatarBaseUrl: null };
const james = { slug: "james-okafor", name: "James Okafor", avatarBaseUrl: null };
const priya = { slug: "priya-anand", name: "Priya Anand", avatarBaseUrl: null };
const tom = { slug: "tom-bradley", name: "Tom Bradley", avatarBaseUrl: null };

const row: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: 20,
  flexWrap: "wrap",
};

// A single candidate shown across the sizes used in the app (list, feed,
// profile header).
export const Sizes = () => (
  <div style={row}>
    <CandidateAvatar candidate={maria as any} size={32} />
    <CandidateAvatar candidate={maria as any} size={44} />
    <CandidateAvatar candidate={maria as any} size={52} />
    <CandidateAvatar candidate={maria as any} size={72} />
  </div>
);

// Different candidates get distinct hashed colors and two-letter initials.
export const Roster = () => (
  <div style={row}>
    <CandidateAvatar candidate={maria as any} size={56} />
    <CandidateAvatar candidate={james as any} size={56} />
    <CandidateAvatar candidate={priya as any} size={56} />
    <CandidateAvatar candidate={tom as any} size={56} />
  </div>
);

// Inline with a name label, as it appears in a candidate row.
export const WithLabel = () => (
  <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
    <CandidateAvatar candidate={maria as any} size={48} />
    <div>
      <div style={{ fontWeight: 600 }}>Maria Chen</div>
      <div style={{ fontSize: 13, color: "var(--muted)" }}>Senate · CA</div>
    </div>
  </div>
);
