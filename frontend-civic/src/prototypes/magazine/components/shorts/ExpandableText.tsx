import { useState } from "react";
import { cn } from "@/lib/cn";

/**
 * Prose that stops mid-thought, with a control to see the rest.
 *
 * Shorts cards ask you to react to something ("Would you want this to pass?"), which only
 * works if you can read the whole thing first. Where the feed's list endpoint serves a
 * cut-down teaser, this renders the teaser plus a "See more" that fetches and swaps in the
 * full text.
 *
 * The control only appears when `loadFull` is supplied — a caller with nothing more to show
 * passes nothing and gets plain text, so there is never a button that does nothing.
 * The fetched text is kept, so collapsing and re-expanding costs one request, not two.
 */
export function ExpandableText({
  preview,
  loadFull,
  className,
  testId,
}: {
  /** What to show while collapsed — typically a server-truncated teaser. */
  preview: string;
  /** Fetches the untruncated text. Omit when `preview` is already complete. */
  loadFull?: () => Promise<string>;
  className?: string;
  testId?: string;
}) {
  const [full, setFull] = useState<string | null>(null);
  const [expanded, setExpanded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  const toggle = async () => {
    if (busy) return;
    if (expanded) {
      setExpanded(false);
      return;
    }
    if (full) {
      setExpanded(true);
      return;
    }

    setBusy(true);
    setFailed(false);
    try {
      const text = await loadFull!();
      setFull(text);
      setExpanded(true);
    } catch {
      // Say so rather than leaving a tap that visibly does nothing.
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className={className} data-testid={testId}>
      <p
        className="text-base leading-relaxed text-[var(--fg-soft)]"
        data-testid={testId ? `${testId}-body` : undefined}
      >
        {expanded && full ? full : preview}
      </p>

      {loadFull && (
        <button
          type="button"
          onClick={() => void toggle()}
          disabled={busy}
          aria-expanded={expanded}
          data-testid={testId ? `${testId}-toggle` : undefined}
          className={cn(
            "mt-1 text-sm font-semibold text-[var(--accent)] transition",
            "hover:underline disabled:opacity-60",
          )}
        >
          {busy ? "Loading…" : expanded ? "See less" : "See more"}
        </button>
      )}

      {failed && (
        <p
          className="mt-1 text-sm text-[var(--muted)]"
          data-testid={testId ? `${testId}-error` : undefined}
        >
          Couldn't load the rest. Tap to try again.
        </p>
      )}
    </div>
  );
}
