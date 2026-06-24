// Preview provider for design-sync (Debate Arena DS).
// Committed, dot-prefixed at frontend/ root → outside tsconfig.app's `include: ["src"]`,
// so the app build ignores it. Re-exported by .ds-barrel.ts and named in
// cfg.provider.component = "DesignProvider".
//
// Navbar / ForkDebateDialog / BreakingTicker call react-router (Link/useNavigate),
// and Navbar reads AuthContext via useAuth — both render blank without these wrappers.
// AuthProvider with no stored token resolves synchronously to the logged-out state
// (no network), which is the correct static preview.
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "@/contexts/AuthContext";

export function DesignProvider({ children }: { children: ReactNode }) {
  return (
    <MemoryRouter>
      <AuthProvider>{children}</AuthProvider>
    </MemoryRouter>
  );
}
