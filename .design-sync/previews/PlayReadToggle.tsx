import { PlayReadToggle } from "frontend-civic";

// The segmented control with "Play" active — as it renders on the player
// dashboard (Mission Control).
export const PlayActive = () => (
  <div style={{ display: "flex", justifyContent: "flex-end", maxWidth: 360 }}>
    <PlayReadToggle active="play" />
  </div>
);

// The same control with "Read" active — as it renders atop the magazine view.
export const ReadActive = () => (
  <div style={{ display: "flex", justifyContent: "flex-end", maxWidth: 360 }}>
    <PlayReadToggle active="read" />
  </div>
);

// Both states side by side so the active-segment styling is easy to compare.
export const BothStates = () => (
  <div style={{ display: "flex", flexDirection: "column", gap: 16, alignItems: "flex-start" }}>
    <PlayReadToggle active="play" />
    <PlayReadToggle active="read" />
  </div>
);
