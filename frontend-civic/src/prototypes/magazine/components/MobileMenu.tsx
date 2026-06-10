import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { NavLink, useLocation } from "react-router-dom";
import { Menu, X } from "lucide-react";
import { DEBATE_ARENA_URL } from "@/lib/links";
import { NAV_LINKS } from "../nav";

/**
 * Mobile "everything" menu. The bottom bar carries the five primary destinations; this hamburger →
 * slide-in drawer exposes the full top-level nav (Feed, Cohort, Zeitgeist, About, Debate Arena, …)
 * that the desktop top nav shows but the bottom bar can't fit. Desktop hides the trigger.
 */
export function MobileMenu() {
  const [open, setOpen] = useState(false);
  const location = useLocation();

  // Close on navigation.
  useEffect(() => {
    setOpen(false);
  }, [location.pathname]);

  // While open: lock body scroll and close on Escape.
  useEffect(() => {
    if (!open) return;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = prevOverflow;
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    [
      "block rounded-lg px-3 py-2.5 text-sm font-semibold uppercase tracking-wider transition",
      isActive
        ? "bg-[var(--accent)]/10 text-[var(--accent)]"
        : "text-[var(--fg-soft)] hover:bg-[var(--border)]/40 hover:text-[var(--fg)]",
    ].join(" ");

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label="Open menu"
        aria-expanded={open}
        data-testid="mobile-menu-button"
        className="inline-flex h-9 w-9 items-center justify-center rounded-full text-[var(--fg-soft)] transition hover:bg-[var(--border)]/40 hover:text-[var(--fg)] md:hidden"
      >
        <Menu className="h-5 w-5" />
      </button>

      {open && createPortal(
        <div className="fixed inset-0 z-40 md:hidden" data-testid="mobile-menu-overlay">
          <button
            type="button"
            aria-label="Close menu"
            tabIndex={-1}
            onClick={() => setOpen(false)}
            className="absolute inset-0 bg-black/30 backdrop-blur-sm"
          />
          <nav
            aria-label="All sections"
            data-testid="mobile-menu-panel"
            className="absolute inset-y-0 right-0 flex w-72 max-w-[82%] flex-col border-l border-[var(--border)] bg-[var(--bg-elev)] shadow-xl"
          >
            <div className="flex items-center justify-between border-b border-[var(--border)] px-4 py-3">
              <span className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
                Menu
              </span>
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Close menu"
                data-testid="mobile-menu-close"
                className="inline-flex h-8 w-8 items-center justify-center rounded-full text-[var(--fg-soft)] transition hover:bg-[var(--border)]/40 hover:text-[var(--fg)]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-2">
              {NAV_LINKS.map((l) => (
                <NavLink
                  key={l.to}
                  to={l.to}
                  end={l.end}
                  data-testid={`mobile-menu-${l.label.toLowerCase()}`}
                  className={linkClass}
                >
                  {l.label}
                </NavLink>
              ))}
              {/* Profile lives in the bottom bar too, but the drawer is the full directory. */}
              <NavLink to="/profile" data-testid="mobile-menu-profile" className={linkClass}>
                Profile
              </NavLink>
              <a
                href={DEBATE_ARENA_URL}
                target="_blank"
                rel="noreferrer"
                data-testid="mobile-menu-debate-arena"
                className="block rounded-lg px-3 py-2.5 text-sm font-semibold uppercase tracking-wider text-[var(--fg-soft)] transition hover:bg-[var(--border)]/40 hover:text-[var(--fg)]"
              >
                Debate Arena ↗
              </a>
            </div>
          </nav>
        </div>,
        document.body,
      )}
    </>
  );
}
