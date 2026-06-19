import { ValueChip } from "frontend-civic";

// Selectable value pills — the selected one fills with the accent.
export const States = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 10, maxWidth: 420 }}>
    <ValueChip label="Lower taxes" selected />
    <ValueChip label="Public schools" />
    <ValueChip label="Climate action" />
    <ValueChip label="Gun rights" />
    <ValueChip label="Immigration reform" />
  </div>
);

export const Single = () => (
  <div style={{ display: "flex", gap: 10 }}>
    <ValueChip label="Unselected" />
    <ValueChip label="Selected" selected />
  </div>
);
