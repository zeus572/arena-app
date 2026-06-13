import { useEffect, useId, useRef, useState, type ReactNode } from "react";

/**
 * Inline jargon explainer. Wrap a technical term to give it a dotted underline and a
 * small definition bubble on hover, focus, or tap. Accessible: the trigger is a button,
 * the bubble is role="tooltip" wired via aria-describedby, and Escape dismisses it.
 *
 * Definitions for the coalition game's jargon live in GLOSSARY; an unknown term renders
 * as plain text so a typo never hides content.
 */
const GLOSSARY: Record<string, { title: string; body: string }> = {
  plank: {
    title: "Plank",
    body: "One concrete policy provision in a platform — the single proposal a coalition forms around.",
  },
  teeth: {
    title: "Teeth",
    body: "How enforceable a provision is — real, binding specifics rather than vague or symbolic language.",
  },
  relegate: {
    title: "Relegate",
    body: "Drop to a lower league next cycle. Like relegation in sports, weaker records move down a tier.",
  },
  promote: {
    title: "Promote",
    body: "Move up to a tougher league next cycle, where bridging a wider spectrum earns more.",
  },
};

export function Term({ term, children }: { term: string; children?: ReactNode }) {
  const entry = GLOSSARY[term.toLowerCase()];
  const [open, setOpen] = useState(false);
  const id = useId();
  const ref = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    window.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onDown);
    return () => {
      window.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onDown);
    };
  }, [open]);

  // Unknown term → render the label as-is, no affordance.
  if (!entry) return <>{children ?? term}</>;

  return (
    <span ref={ref} className="relative inline-block">
      <button
        type="button"
        aria-describedby={open ? id : undefined}
        onClick={() => setOpen((v) => !v)}
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        data-testid={`term-${term.toLowerCase()}`}
        className="cursor-help border-b border-dotted border-current font-semibold decoration-from-font focus-visible:outline focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
      >
        {children ?? entry.title}
      </button>
      {open && (
        <span
          role="tooltip"
          id={id}
          className="absolute bottom-full left-1/2 z-40 mb-2 w-56 -translate-x-1/2 border border-[var(--border)] bg-[var(--bg-elev)] p-3 text-left text-xs font-normal normal-case leading-snug tracking-normal text-[var(--fg-soft)] shadow-lg"
        >
          <span className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-[var(--fg)]">
            {entry.title}
          </span>
          {entry.body}
        </span>
      )}
    </span>
  );
}
