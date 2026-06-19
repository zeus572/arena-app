import { Flyout } from "frontend-civic";

// Canonical: the slide-over open with the "Take a position" form from the
// Participate page. Authored open so the panel, header, and body all render.
export const TakeAPosition = () => (
  <Flyout
    open
    onClose={() => {}}
    title="Take a position"
    subtitle="Carbon border adjustment — Section 3"
  >
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <p style={{ fontSize: 14, lineHeight: 1.5 }}>
        Pick where you land on the core trade-off. You can save a draft now and
        co-sign a matching version later.
      </p>
      <label style={{ display: "block", fontSize: 13, fontWeight: 600 }}>
        Phase-in period
        <select style={{ display: "block", marginTop: 6, width: "100%", padding: 8 }}>
          <option>Immediate (next cycle)</option>
          <option>Three-year ramp</option>
          <option>Tied to trading-partner parity</option>
        </select>
      </label>
      <label style={{ display: "block", fontSize: 13, fontWeight: 600 }}>
        Revenue use
        <textarea
          rows={3}
          defaultValue="Rebate to low-income households; remainder to grid modernization."
          style={{ display: "block", marginTop: 6, width: "100%", padding: 8 }}
        />
      </label>
    </div>
  </Flyout>
);

// Variant: the "Propose a carve-out" flow, a different title/subtitle and body.
export const ProposeCarveOut = () => (
  <Flyout
    open
    onClose={() => {}}
    title="Propose a carve-out"
    subtitle="Who should this provision exempt?"
  >
    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
      <p style={{ fontSize: 14, lineHeight: 1.5 }}>
        Name a group the rule should not apply to and say why. Carve-outs that
        bridge a wider spectrum earn more breadth.
      </p>
      <input
        defaultValue="Small farms under 50 acres"
        style={{ width: "100%", padding: 8 }}
      />
      <textarea
        rows={4}
        defaultValue="Compliance cost is disproportionate at this scale and emissions impact is marginal."
        style={{ width: "100%", padding: 8 }}
      />
    </div>
  </Flyout>
);
