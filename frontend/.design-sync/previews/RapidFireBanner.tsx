import { RapidFireBanner } from "frontend";

// Rapid Fire format: speed-line banner with a big round counter. Designed for
// the dark debate page (light text on a low-opacity dark gradient), so the card
// frames it on a dark surface.
export const Round = () => (
  <div style={{ background: "linear-gradient(160deg, #1c1530, #0c0a09)", padding: 18, borderRadius: 16 }}>
    <RapidFireBanner totalTurns={10} currentTurn={7} />
  </div>
);
