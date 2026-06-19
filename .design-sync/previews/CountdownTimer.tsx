import { CountdownTimer } from "frontend-civic";

/**
 * CountdownTimer fetches the next election for a given scope ("National" | "State" |
 * "Local") on mount, then renders a days/hrs/min/sec clock. There is no backend in
 * this preview environment, so the fetch never resolves and the component stays in
 * its styled "Loading…" state — a bordered, elevated section with an uppercase
 * eyebrow ("Next national election") and a muted "Loading…" line.
 *
 * NOTE / learnings: the live countdown clock (the 4-cell day/hr/min/sec grid) cannot
 * render statically because it depends on a network response. The honest static state
 * is the loading chrome below, which is fully styled.
 */

// National scope — as used on the magazine Home page.
export const NationalLoading = () => (
  <div style={{ maxWidth: 480 }}>
    <CountdownTimer scope="National" testId="countdown-national" />
  </div>
);

// State scope, demonstrating the dynamic eyebrow label ("Next state election").
export const StateLoading = () => (
  <div style={{ maxWidth: 480 }}>
    <CountdownTimer scope="State" region="CA" testId="countdown-state" />
  </div>
);
