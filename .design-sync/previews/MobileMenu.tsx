import { useEffect } from "react";
import { MobileMenu } from "frontend-civic";

/**
 * MobileMenu is a hamburger trigger that opens a right-side slide-in drawer via a
 * React portal to document.body. The drawer exposes the full nav directory:
 * primary links (Home, Feed), the labeled groups Compete (Leagues, Campaign,
 * Cohort, Coalition) and Explore (Quizzes, Zeitgeist, Tax Dollar, Bill Timeline),
 * plus Profile & settings, Civic Compass, About, and the Debate Arena link.
 *
 * The component manages its own `open` state and only renders the trigger button by
 * default. To show the open drawer statically, we click the trigger after mount.
 */
function useAutoOpen(testId: string) {
  useEffect(() => {
    const t = window.setTimeout(() => {
      const btn = document.querySelector<HTMLButtonElement>(
        `[data-testid="${testId}"]`,
      );
      btn?.click();
    }, 50);
    return () => window.clearTimeout(t);
  }, [testId]);
}

// The collapsed trigger: a 36px hamburger icon button (md:hidden in the real shell).
export const Trigger = () => (
  <div style={{ display: "flex", justifyContent: "flex-end", padding: 8 }}>
    <MobileMenu />
  </div>
);

// The open drawer overlay with the full nav directory. Auto-opens on mount.
export const OpenDrawer = () => {
  useAutoOpen("mobile-menu-button");
  return (
    <div style={{ minHeight: 560 }}>
      <MobileMenu />
    </div>
  );
};
