// Preview provider for /design-sync (referenced by .design-sync/config.json via
// extraEntries + cfg.provider). The magazine components use react-router
// (<Link>, useNavigate) and render blank without a Router in context;
// MemoryRouter supplies it with no app wiring. Lives at the frontend-civic root
// so the converter's package-relative resolution (via the node_modules self-
// junction) finds it without a `../` escape. Not part of the app build.
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";

// The magazine palette, fonts, and base colors are all defined on the
// `.theme-magazine` class (not :root), so without this wrapper every token
// (var(--accent), var(--font-display), …) is unset — accent fills go invisible
// and serif display text falls back. The app wraps its UI in this class; designs
// built with this DS must too (see README/conventions).
export function DesignProvider({ children }: { children: ReactNode }) {
  return (
    <MemoryRouter>
      <div className="theme-magazine">{children}</div>
    </MemoryRouter>
  );
}
