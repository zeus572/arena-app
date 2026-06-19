import { useEffect } from "react";
import { NavDropdown } from "frontend-civic";

/**
 * NavDropdown is the desktop top-nav section dropdown. In the real Layout it renders
 * the "Explore" section: Quizzes, Zeitgeist, Tax Dollar, Bill Timeline. Click the
 * trigger to open a bordered menu panel anchored below it. It manages its own `open`
 * state, so to show the open panel statically we click the trigger after mount.
 */
const EXPLORE_LINKS = [
  { to: "/quizzes", label: "Quizzes" },
  { to: "/zeitgeist", label: "Zeitgeist" },
  { to: "/briefings/who-gets-your-tax-dollar", label: "Tax Dollar" },
  { to: "/timelines/bill", label: "Bill Timeline" },
];

function useAutoOpen(testId: string) {
  useEffect(() => {
    const t = window.setTimeout(() => {
      document
        .querySelector<HTMLButtonElement>(`[data-testid="${testId}"]`)
        ?.click();
    }, 50);
    return () => window.clearTimeout(t);
  }, [testId]);
}

// The closed trigger: an uppercase "Explore ▾" nav button.
export const Trigger = () => (
  <div style={{ padding: 16 }}>
    <NavDropdown label="Explore" links={EXPLORE_LINKS} />
  </div>
);

// The open menu panel listing the Explore destinations. Auto-opens on mount; we leave
// headroom below so the absolutely-positioned panel is fully visible.
export const OpenMenu = () => {
  useAutoOpen("top-nav-explore");
  return (
    <div style={{ padding: 16, paddingBottom: 220 }}>
      <NavDropdown label="Explore" links={EXPLORE_LINKS} />
    </div>
  );
};
