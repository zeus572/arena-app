import { useEffect, type ReactNode } from "react";
import { X } from "lucide-react";

interface FlyoutProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
  children: ReactNode;
}

/**
 * Right-side slide-over drawer. Backdrop click or Escape closes it.
 * Used for "Take a position" and "Propose a carve-out" on the Participate page.
 */
export default function Flyout({ open, onClose, title, subtitle, children }: FlyoutProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  return (
    <div
      className={`fixed inset-0 z-50 ${open ? "" : "pointer-events-none"}`}
      aria-hidden={!open}
    >
      {/* Backdrop */}
      <div
        onClick={onClose}
        className={`absolute inset-0 bg-black/30 transition-opacity duration-200 ${
          open ? "opacity-100" : "opacity-0"
        }`}
      />
      {/* Panel */}
      <aside
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`absolute right-0 top-0 flex h-full w-full max-w-md flex-col bg-[var(--bg-elev)] shadow-2xl transition-transform duration-200 ease-out ${
          open ? "translate-x-0" : "translate-x-full"
        }`}
      >
        <header className="flex items-start justify-between gap-4 border-b border-[var(--line)] px-5 py-4">
          <div>
            <h2 className="text-lg font-semibold">{title}</h2>
            {subtitle && <p className="mt-0.5 text-xs text-[var(--muted)]">{subtitle}</p>}
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            className="rounded-full p-1 text-[var(--muted)] hover:bg-[var(--line)] hover:text-[var(--fg)]"
          >
            <X size={18} />
          </button>
        </header>
        <div className="flex-1 overflow-y-auto px-5 py-5">{children}</div>
      </aside>
    </div>
  );
}
