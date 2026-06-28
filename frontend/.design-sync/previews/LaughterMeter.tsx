import { LaughterMeter } from "frontend";

// Roast format: lights up flame icons from roast turns + 🔥 reactions.
// (3 roasts · 7 fire → ~71% → 4 of 5 flames lit.) Designed for the dark debate
// page, so the card frames it on a dark surface.
const turns = [
  { type: "Roast", reactions: { fire: 4 } },
  { type: "Roast", reactions: { fire: 3 } },
  { type: "Roast", reactions: {} },
] as any;

export const CrowdHeat = () => (
  <div style={{ background: "linear-gradient(160deg, #1c1530, #0c0a09)", padding: 18, borderRadius: 16 }}>
    <LaughterMeter turns={turns} />
  </div>
);
