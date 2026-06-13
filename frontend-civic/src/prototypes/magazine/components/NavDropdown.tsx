import { useEffect, useRef, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import type { NavLinkItem } from "../nav";

/**
 * Desktop nav dropdown for a section (e.g. "Explore"). Click to open; closes on
 * outside click, Escape, or navigation. The trigger reflects the active state when
 * any child route is active.
 */
export function NavDropdown({
  label,
  links,
}: {
  label: string;
  links: NavLinkItem[];
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const location = useLocation();

  const containsActive = links.some(
    (l) => location.pathname === l.to || location.pathname.startsWith(l.to + "/"),
  );

  // Close on navigation.
  useEffect(() => {
    setOpen(false);
  }, [location.pathname]);

  // Close on outside click + Escape while open.
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    window.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        data-testid={`top-nav-${label.toLowerCase()}`}
        className={[
          "text-xs font-semibold uppercase tracking-wider transition",
          containsActive || open
            ? "text-[var(--accent)]"
            : "text-[var(--muted)] hover:text-[var(--fg)]",
        ].join(" ")}
      >
        {label}
        <span aria-hidden className="ml-0.5 text-[0.85em] opacity-70">
          {open ? "▴" : "▾"}
        </span>
      </button>

      {open && (
        <div
          role="menu"
          aria-label={label}
          data-testid={`top-nav-${label.toLowerCase()}-menu`}
          className="absolute left-0 top-full z-30 mt-2 min-w-44 border border-[var(--border)] bg-[var(--bg-elev)] py-1 shadow-lg"
        >
          {links.map((l) => (
            <NavLink
              key={l.to}
              to={l.to}
              role="menuitem"
              data-testid={`top-nav-${l.label.toLowerCase().replace(/\s+/g, "-")}`}
              className={({ isActive }) =>
                [
                  "block px-4 py-2 text-xs font-semibold uppercase tracking-wider transition",
                  isActive
                    ? "bg-[var(--accent)]/10 text-[var(--accent)]"
                    : "text-[var(--fg-soft)] hover:bg-[var(--border)]/40 hover:text-[var(--fg)]",
                ].join(" ")
              }
            >
              {l.label}
            </NavLink>
          ))}
        </div>
      )}
    </div>
  );
}
