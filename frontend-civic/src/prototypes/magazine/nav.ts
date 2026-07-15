/**
 * Navigation model. The nav is grouped into sections:
 *
 *  - PRIMARY: always-visible standalone links (the issue + the candidates feed).
 *  - "Compete": head-to-head play against friends / leagues. Shown FLAT on desktop
 *    (these are the high-engagement areas we want one tap away) and as a labeled
 *    group in the mobile drawer.
 *  - "Explore": interactive, hands-on civics tools. Shown as a DROPDOWN on desktop
 *    and a labeled group in the mobile drawer.
 *  - TRAILING: meta links (About) pinned to the end.
 *
 * The desktop top nav (Layout) and the mobile drawer (MobileMenu) both read this.
 */
export type NavLinkItem = { to: string; label: string; end?: boolean };
export type NavGroup = { heading: string; links: NavLinkItem[] };

export const NAV_PRIMARY: NavLinkItem[] = [
  { to: "/", label: "Home", end: true },
  { to: "/shorts", label: "Shorts" },
  { to: "/candidates", label: "Feed" },
];

export const NAV_COMPETE: NavGroup = {
  heading: "Compete",
  links: [
    { to: "/leagues", label: "Leagues" },
    { to: "/campaigns", label: "Campaign" },
    { to: "/cohort", label: "Cohort" },
    { to: "/coalition", label: "Coalition" },
  ],
};

export const NAV_EXPLORE: NavGroup = {
  heading: "Explore",
  links: [
    { to: "/bills", label: "Bills" },
    { to: "/quizzes", label: "Quizzes" },
    { to: "/zeitgeist", label: "Zeitgeist" },
    { to: "/briefings/who-gets-your-tax-dollar", label: "Tax Dollar" },
    { to: "/timelines/bill", label: "Bill Timeline" },
  ],
};

export const NAV_TRAILING: NavLinkItem[] = [{ to: "/about", label: "About" }];

/** All groups, in order — used by the mobile drawer to render labeled sections. */
export const NAV_GROUPS: NavGroup[] = [NAV_COMPETE, NAV_EXPLORE];
