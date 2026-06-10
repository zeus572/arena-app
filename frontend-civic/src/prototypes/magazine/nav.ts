/**
 * Top-level destinations shared by the desktop top nav and the mobile menu drawer. The mobile bottom
 * bar (see BottomTabs) shows only the five "primary" ones; everything else is reached via the drawer.
 */
export type NavLinkItem = { to: string; label: string; end?: boolean };

export const NAV_LINKS: NavLinkItem[] = [
  { to: "/", label: "Home", end: true },
  { to: "/candidates", label: "Feed" },
  { to: "/campaigns", label: "Campaign" },
  { to: "/leagues", label: "Leagues" },
  { to: "/coalition", label: "Coalition" },
  { to: "/cohort", label: "Cohort" },
  { to: "/zeitgeist", label: "Zeitgeist" },
  { to: "/quizzes", label: "Quizzes" },
  { to: "/about", label: "About" },
];
