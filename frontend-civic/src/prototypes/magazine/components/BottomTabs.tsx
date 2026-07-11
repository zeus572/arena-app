import { NavLink } from "react-router-dom";
import { Home, Trophy, Handshake, User, Megaphone, Zap } from "lucide-react";

type Tab = {
  to: string;
  label: string;
  icon: typeof Home;
  end?: boolean;
  testId: string;
  /** Visually highlighted destination (the casual Shorts feed). */
  accent?: boolean;
};

// The primary destinations. Shorts is the highlighted casual entry point; everything
// else not here is reached via the mobile menu drawer (MobileMenu).
const tabs: Tab[] = [
  { to: "/", label: "Home", icon: Home, end: true, testId: "tab-home" },
  { to: "/shorts", label: "Shorts", icon: Zap, testId: "tab-shorts", accent: true },
  { to: "/leagues", label: "Leagues", icon: Trophy, testId: "tab-leagues" },
  { to: "/campaigns", label: "Campaign", icon: Megaphone, testId: "tab-campaign" },
  { to: "/coalition", label: "Coalition", icon: Handshake, testId: "tab-coalition" },
  { to: "/settings", label: "Profile", icon: User, testId: "tab-profile" },
];

export function BottomTabs() {
  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-30 border-t border-[var(--border)] bg-[var(--bg-elev)]/95 backdrop-blur md:hidden"
      data-testid="bottom-tabs"
    >
      <ul className="mx-auto flex max-w-md items-stretch justify-around px-1 py-1">
        {tabs.map((t) => {
          const Icon = t.icon;
          return (
            <li key={t.to} className="flex-1">
              <NavLink
                to={t.to}
                end={t.end}
                data-testid={t.testId}
                className={({ isActive }) =>
                  [
                    "motion-press flex flex-col items-center gap-0.5 py-2 text-[10px] font-semibold uppercase tracking-[0.1em] transition",
                    // Shorts stays accent-colored even when inactive so it reads as
                    // the standout "new, fun" destination.
                    t.accent
                      ? "text-[var(--accent)]"
                      : isActive
                        ? "text-[var(--accent)]"
                        : "text-[var(--muted)] hover:text-[var(--fg)]",
                  ].join(" ")
                }
              >
                <Icon className={t.accent ? "h-5 w-5 fill-current" : "h-5 w-5"} />
                {t.label}
              </NavLink>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
