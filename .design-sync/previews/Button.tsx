import { Button } from "frontend-civic";

// The six variants, on the actions they map to in the magazine UI.
export const Variants = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
    <Button variant="primary">Co-sign bill</Button>
    <Button variant="secondary">Compare versions</Button>
    <Button variant="positive">Accept</Button>
    <Button variant="ghost">Copy link</Button>
    <Button variant="danger">Revoke</Button>
    <Button variant="link">Cancel</Button>
  </div>
);

// Two sizes plus the full-width block CTA.
export const Sizes = () => (
  <div style={{ display: "flex", flexDirection: "column", gap: 12, maxWidth: 320 }}>
    <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
      <Button size="md">Default (md)</Button>
      <Button size="sm">Compact (sm)</Button>
    </div>
    <Button variant="primary" fullWidth>
      Start your campaign
    </Button>
  </div>
);

export const Disabled = () => (
  <div style={{ display: "flex", gap: 12 }}>
    <Button variant="primary" disabled>
      Submitting…
    </Button>
    <Button variant="ghost" disabled>
      Unavailable
    </Button>
  </div>
);
