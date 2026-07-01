import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * Shared full-viewport wrapper for every Shorts card. Owns the vertical rhythm so
 * all card kinds stay consistent and — the reason it exists — so they survive short
 * viewports and iOS safe areas instead of assuming a roomy one:
 *
 *  - Top/bottom padding fold in `env(safe-area-inset-*)`, so content clears the
 *    notch / status bar at the top and the home indicator / browser chrome at the
 *    bottom. (Needs `viewport-fit=cover` in index.html for the insets to be nonzero.)
 *  - The shell scrolls as a safety valve (`overflow-y-auto`), so when tall content
 *    can't fit a short screen the bottom react bar + CTA are reachable rather than
 *    stranded below the fold — the iPhone Pro-vs-Plus squeeze. `overscroll-contain`
 *    keeps that inner scroll from fighting the feed's vertical snap paging.
 *
 * The base paddings (4.5rem top / 1.5rem bottom) are a touch tighter than the old
 * fixed 5rem/2rem to give the middle more breathing room on shorter phones.
 */
export function ShortCardShell({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      data-testid="short-card-shell"
      className={cn(
        "mx-auto flex h-full w-full max-w-xl flex-col overflow-y-auto overscroll-contain px-5",
        className,
      )}
      style={{
        // Clear the overlay header (~60px) + the status-bar safe area.
        paddingTop: "max(4.5rem, calc(env(safe-area-inset-top) + 3.75rem))",
        // Clear the home indicator / Safari bottom bar; never less than 1.5rem.
        paddingBottom: "max(1.5rem, calc(env(safe-area-inset-bottom) + 1.25rem))",
      }}
    >
      {children}
    </div>
  );
}
