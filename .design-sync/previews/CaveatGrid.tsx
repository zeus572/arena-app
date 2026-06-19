import { CaveatGrid } from "frontend-civic";

// CaveatGrid is a self-contained "where the model gets fuzzy" grid: six fixed caveat
// cards (consumption, housing, local taxes, capital income, behavior, employer FICA)
// laid out responsively (1/2/3 columns). Content is baked in — no props.
export const Default = () => (
  <div style={{ maxWidth: 980 }}>
    <CaveatGrid />
  </div>
);

// Narrower frame to confirm the grid reflows to fewer columns gracefully.
export const NarrowColumn = () => (
  <div style={{ maxWidth: 420 }}>
    <CaveatGrid />
  </div>
);
