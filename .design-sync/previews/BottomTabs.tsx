import { BottomTabs } from "frontend-civic";

// The mobile bottom tab bar carries the five primary destinations: Home, Leagues,
// Campaign, Coalition, Profile. It is `position: fixed` to the viewport bottom and
// hidden on desktop (md:hidden), so we constrain to a phone-width frame and give it
// a tall relative shell so the fixed bar pins to the bottom of the preview cell.
export const MobileBar = () => (
  <div
    style={{
      position: "relative",
      width: 390,
      height: 320,
      maxWidth: "100%",
      overflow: "hidden",
      border: "1px solid var(--border)",
      borderRadius: 12,
    }}
  >
    <div style={{ padding: 16 }}>
      <p style={{ fontSize: 13, color: "var(--muted)", margin: 0 }}>
        Mobile shell — the bottom bar pins to the foot of the viewport.
      </p>
    </div>
    <BottomTabs />
  </div>
);
