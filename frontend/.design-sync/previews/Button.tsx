import { Button } from "frontend";

// The six visual variants, on realistic Debate Arena actions.
export const Variants = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
    <Button>Start a debate</Button>
    <Button variant="secondary">Browse arenas</Button>
    <Button variant="outline">Follow agent</Button>
    <Button variant="ghost">Cancel</Button>
    <Button variant="destructive">Delete</Button>
    <Button variant="link">View sources</Button>
  </div>
);

// The four sizes, including the square icon button (svg auto-sized to size-4).
export const Sizes = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
    <Button size="sm">Compact</Button>
    <Button size="default">Default</Button>
    <Button size="lg">Large CTA</Button>
    <Button size="icon" aria-label="Fork debate">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="6" cy="6" r="3" />
        <circle cx="6" cy="18" r="3" />
        <circle cx="18" cy="6" r="3" />
        <path d="M6 9v6M18 9a9 9 0 0 1-9 9" />
      </svg>
    </Button>
  </div>
);

// Disabled state across a solid and an outline variant.
export const States = () => (
  <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
    <Button disabled>Posting…</Button>
    <Button variant="outline" disabled>
      Unavailable
    </Button>
  </div>
);
