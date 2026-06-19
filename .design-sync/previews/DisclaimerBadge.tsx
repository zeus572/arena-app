import { DisclaimerBadge } from "frontend-civic";

// Canonical: the trust badge as it appears beside a Virtual Candidate's name.
export const Default = () => (
  <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
    <span className="display" style={{ fontSize: 34, lineHeight: 1 }}>
      Marisol Vega
    </span>
    <DisclaimerBadge />
  </div>
);

// In context: the badge sits inline with a role/office line on a candidate card.
export const InContext = () => (
  <div style={{ maxWidth: 420 }}>
    <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8 }}>
      <span className="display" style={{ fontSize: 26 }}>
        Dr. Aaron Whitfield
      </span>
      <DisclaimerBadge />
    </div>
    <p style={{ marginTop: 4, fontSize: 13, textTransform: "uppercase", letterSpacing: "0.06em" }}>
      Candidate for U.S. Senate · Ohio
    </p>
  </div>
);
