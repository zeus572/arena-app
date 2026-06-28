import { ForkDebateDialog } from "frontend";

// ForkDebateDialog renders as a full-screen `fixed inset-0` overlay. To capture
// it whole inside a card, we give it a bounded containing block: a `transform`
// on the wrapper makes the fixed overlay resolve against this box instead of the
// viewport, so the dimmed backdrop and centered dialog both render in-frame.
// The arena chip picker loads from the API; with no backend the picker stays
// hidden, which is the dialog's real "arenas not loaded yet" state.
export const Open = () => (
  <div
    style={{
      position: "relative",
      transform: "translateZ(0)",
      width: 600,
      height: 480,
      overflow: "hidden",
      borderRadius: 12,
    }}
  >
    <ForkDebateDialog
      debateId="deb_123"
      parentTopic="Should the federal minimum wage be tied to inflation?"
      parentArenaId={null}
      onClose={() => {}}
    />
  </div>
);
