import { SplitBar } from "frontend-civic";

// SplitBar renders the federal-vs-state/local apportionment as a single horizontal
// bar: federal segment in blue (var(--federal)), state/local in terracotta
// (var(--state)), widths set by the two shares (each clamped to 0..1). The large
// variant prints in-bar percentages plus a labeled footer; the small variant is the
// slim per-row bar the scaling table reuses. Shares below are the engine's real
// `combined.federalShare` / `stateShare` for representative households.

// $85,000 single in California — 63.6% federal / 36.4% state & local.
export const Canonical = () => (
  <div style={{ maxWidth: 560 }}>
    <SplitBar federalShare={0.636} stateShare={0.364} />
  </div>
);

// $250,000 married/joint in New York — federal share dips toward 61% as high
// state income + property tax pull the split toward state & local.
export const HighIncomeNewYork = () => (
  <div style={{ maxWidth: 560 }}>
    <SplitBar federalShare={0.609} stateShare={0.391} />
  </div>
);

// Small, label-less variant as embedded in a ScalingTable row ($60,000 single,
// Texas — no state income tax, so federal carries ~66% of the bill).
export const SlimRowVariant = () => (
  <div style={{ width: 160 }}>
    <SplitBar federalShare={0.659} stateShare={0.341} size="sm" showLabels={false} />
  </div>
);
