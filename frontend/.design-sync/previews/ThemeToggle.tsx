import { ThemeToggle } from "frontend";

// The ghost icon button as it appears in the navbar's controls cluster.
// Self-manages light/dark from localStorage + prefers-color-scheme; the static
// preview shows the light-mode (Moon) state.
export const InToolbar = () => (
  <div
    style={{
      display: "inline-flex",
      alignItems: "center",
      gap: 10,
      padding: "6px 12px",
      border: "1px solid var(--border)",
      borderRadius: 10,
      background: "var(--card)",
    }}
  >
    <span style={{ fontSize: 13, color: "var(--muted-foreground)" }}>Appearance</span>
    <ThemeToggle />
  </div>
);
