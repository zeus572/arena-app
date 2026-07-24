import { NavLink, Outlet } from "react-router-dom";
import { ShieldCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { SignInPrompt } from "@/prototypes/magazine/components/SignInPrompt";

const TABS = [
  { to: "engagement", label: "Engagement" },
  { to: "budget", label: "Budget" },
  { to: "tools", label: "Tools" },
];

/**
 * Shell for the (unadvertised) /admin/* area: signed-in gate + tab nav + <Outlet/>.
 * Admin authorization itself is enforced by the backend — child pages surface a 403 as
 * an "Admins only" state.
 */
export default function AdminShell() {
  const { isAuthenticated, isLoading } = useAuth();

  if (!isLoading && !isAuthenticated) {
    return (
      <section data-testid="admin-page">
        <SignInPrompt title="Sign in to continue" message="This area is for administrators only." />
      </section>
    );
  }

  return (
    <section data-testid="admin-page">
      <header>
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
          <ShieldCheck size={14} /> Admin
        </p>
        <nav className="mt-3 flex gap-1 border-b border-[var(--border)]" data-testid="admin-tabs">
          {TABS.map((t) => (
            <NavLink
              key={t.to}
              to={t.to}
              className={({ isActive }) =>
                `px-3 py-2 text-sm transition ${
                  isActive
                    ? "border-b-2 border-[var(--accent)] font-semibold text-[var(--fg)]"
                    : "border-b-2 border-transparent text-[var(--muted)] hover:text-[var(--fg)]"
                }`
              }
            >
              {t.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <div className="mt-6">
        <Outlet />
      </div>
    </section>
  );
}
