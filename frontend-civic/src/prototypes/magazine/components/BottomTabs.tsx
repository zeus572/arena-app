import { NavLink } from "react-router-dom";
import { Home, GraduationCap, Compass, User } from "lucide-react";

type Tab = {
  to: string;
  label: string;
  icon: typeof Home;
  end?: boolean;
  testId: string;
};

const tabs: Tab[] = [
  { to: "/", label: "Home", icon: Home, end: true, testId: "tab-home" },
  { to: "/quizzes", label: "Quizzes", icon: GraduationCap, testId: "tab-quizzes" },
  { to: "/onboarding", label: "Compass", icon: Compass, testId: "tab-compass" },
  { to: "/profile", label: "You", icon: User, testId: "tab-you" },
];

export function BottomTabs() {
  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-30 border-t border-[var(--border)] bg-[var(--bg-elev)]/95 backdrop-blur md:hidden"
      data-testid="bottom-tabs"
    >
      <ul className="mx-auto flex max-w-md items-stretch justify-around px-2 py-1">
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
                    "flex flex-col items-center gap-0.5 py-2 text-[10px] font-semibold uppercase tracking-[0.12em] transition",
                    isActive
                      ? "text-[var(--accent)]"
                      : "text-[var(--muted)] hover:text-[var(--fg)]",
                  ].join(" ")
                }
              >
                <Icon className="h-5 w-5" />
                {t.label}
              </NavLink>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
