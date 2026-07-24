import { useLocation, useNavigate } from "react-router-dom";
import { ArrowUp, X } from "lucide-react";
import { useNewStories } from "@/lib/newStories";

/**
 * Floating "N new stories" pill. Appears when the background watcher
 * ({@link useNewStories}) has detected briefings published since the reader caught up.
 * Tapping it reloads the feed (and routes Home if they're elsewhere); the ✕ dismisses
 * without reloading. Hidden entirely when there's nothing new.
 */
export function NewStoriesBanner() {
  const { newCount, refresh, dismiss } = useNewStories();
  const navigate = useNavigate();
  const location = useLocation();

  if (newCount <= 0) return null;

  const label =
    newCount === 1 ? "1 new story" : `${newCount} new stories`;

  const handleShow = () => {
    refresh(); // resets the baseline + bumps feedVersion so the feed reloads
    if (location.pathname !== "/") navigate("/");
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  return (
    <div
      className="pointer-events-none fixed inset-x-0 top-16 z-30 flex justify-center px-4 md:top-24"
      role="status"
      aria-live="polite"
      data-testid="new-stories-banner"
    >
      <div className="pointer-events-auto flex items-center gap-1 rounded-full border border-[var(--accent)] bg-[var(--accent)] pl-1 pr-1 text-white shadow-lg">
        <button
          type="button"
          onClick={handleShow}
          className="flex items-center gap-2 rounded-full px-4 py-2 text-xs font-semibold uppercase tracking-wider transition hover:bg-white/10"
          data-testid="new-stories-show"
        >
          <ArrowUp className="h-4 w-4" aria-hidden />
          {label} · Show
        </button>
        <button
          type="button"
          onClick={dismiss}
          className="flex h-8 w-8 items-center justify-center rounded-full transition hover:bg-white/10"
          aria-label="Dismiss new stories"
          data-testid="new-stories-dismiss"
        >
          <X className="h-4 w-4" aria-hidden />
        </button>
      </div>
    </div>
  );
}
