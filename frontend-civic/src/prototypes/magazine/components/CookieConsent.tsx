import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getConsent, setConsent, type ConsentChoice } from "@/lib/consent";

/**
 * First-visit cookie/analytics notice. Non-blocking: the product works whether
 * the reader accepts or declines. Renders nothing once a choice is stored.
 * Mounted at the app root and self-themed so it appears on every route.
 */
export default function CookieConsent() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    setVisible(getConsent() === null);
  }, []);

  if (!visible) return null;

  const choose = (choice: ConsentChoice) => {
    setConsent(choice);
    setVisible(false);
  };

  return (
    <div
      className="theme-magazine fixed inset-x-0 bottom-0 z-50 border-t border-[var(--border)] bg-[var(--bg)]/98 backdrop-blur"
      role="dialog"
      aria-label="Cookie notice"
      data-testid="cookie-consent"
    >
      <div className="mx-auto flex max-w-5xl flex-col gap-3 px-4 py-4 md:flex-row md:items-center md:justify-between md:px-8">
        <p className="text-sm leading-relaxed text-[var(--fg-soft)]">
          We use essential cookies to keep you signed in and, with your consent,
          privacy-light analytics to understand what’s useful. You can change your
          mind anytime.{" "}
          <Link to="/privacy" className="text-[var(--accent)] underline">
            Privacy Policy
          </Link>
        </p>
        <div className="flex shrink-0 gap-2">
          <button
            type="button"
            onClick={() => choose("declined")}
            className="border border-[var(--border)] px-4 py-2 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] transition hover:text-[var(--fg)]"
            data-testid="cookie-decline"
          >
            Decline
          </button>
          <button
            type="button"
            onClick={() => choose("accepted")}
            className="bg-[var(--accent)] px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white transition hover:opacity-90"
            data-testid="cookie-accept"
          >
            Accept
          </button>
        </div>
      </div>
    </div>
  );
}
