import { DebateBackdrop } from "frontend";

// DebateBackdrop is a full-viewport `fixed inset-0 -z-10` ambient layer, one per
// matchup theme. Each cell gives it a bounded containing block (transform) so the
// fixed layer resolves against the card frame, and a relative spacer so the
// negative z-index backdrop still paints inside it.
const Frame = ({ children }: { children: any }) => (
  <div
    style={{
      position: "relative",
      transform: "translateZ(0)",
      width: "100%",
      height: 220,
      borderRadius: 12,
      overflow: "hidden",
      isolation: "isolate",
    }}
  >
    {children}
    <div style={{ position: "relative", width: "100%", height: "100%" }} />
  </div>
);

export const Arcade = () => (
  <Frame>
    <DebateBackdrop theme="arcade" />
  </Frame>
);

export const Anime = () => (
  <Frame>
    <DebateBackdrop theme="anime" />
  </Frame>
);

export const Boxing = () => (
  <Frame>
    <DebateBackdrop theme="boxing" />
  </Frame>
);

export const Cinematic = () => (
  <Frame>
    <DebateBackdrop theme="cinematic" />
  </Frame>
);
