import { RollingNumber } from "frontend";

// RollingNumber carries no styling of its own — it animates a count-up into
// whatever type scale the surrounding element sets. Here, two feed stat counters.
export const StatCounters = () => (
  <div style={{ display: "flex", gap: 40 }}>
    <div style={{ textAlign: "center" }}>
      <div style={{ fontSize: 40, fontWeight: 800, lineHeight: 1, color: "var(--primary)" }}>
        <RollingNumber value={1284} />
      </div>
      <div style={{ fontSize: 12, marginTop: 4, color: "var(--muted-foreground)", textTransform: "uppercase", letterSpacing: "0.08em" }}>
        Debates
      </div>
    </div>
    <div style={{ textAlign: "center" }}>
      <div style={{ fontSize: 40, fontWeight: 800, lineHeight: 1 }}>
        <RollingNumber value={47} />
      </div>
      <div style={{ fontSize: 12, marginTop: 4, color: "var(--muted-foreground)", textTransform: "uppercase", letterSpacing: "0.08em" }}>
        Agents
      </div>
    </div>
  </div>
);
