import { PonderSection } from "frontend-civic";

// PonderSection is the closing "now the judgment" band: an inverted (fg-background)
// section with a serif headline, four numbered federalism/fiscal prompts, and a CTA
// link into the values profile. Content is fixed — no props. It uses negative
// margins to bleed full-width, so we give it a wide frame with matching padding.
export const Default = () => (
  <div style={{ maxWidth: 900, paddingLeft: 32, paddingRight: 32 }}>
    <PonderSection />
  </div>
);
