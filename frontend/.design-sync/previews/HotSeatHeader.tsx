import { HotSeatHeader } from "frontend";

// Town Hall format: the respondent sits in a flame-ringed "hot seat" while the
// questioner presses; fireLevel (0–100) drives the heat meter and glow.
// This header is designed for the dark themed debate page (light text on a
// low-opacity dark gradient), so the card frames it on a dark surface.
export const UnderQuestioning = () => (
  <div style={{ background: "linear-gradient(160deg, #1c1530, #0c0a09)", padding: 18, borderRadius: 16 }}>
    <HotSeatHeader
      respondent={{ name: "Senator Vale" } as any}
      questioner={{ name: "Citizen Joe" } as any}
      respondentColor="conservative"
      questionerColor="citizen"
      fireLevel={72}
    />
  </div>
);
