import { useState } from "react";
import type { TimelineEvent } from "@/api/rooms";

const MARKER_LABEL: Record<TimelineEvent["marker"], string> = {
  Agreed: "Nobody disputes this happened",
  Contested: "A contested decision",
  Trigger: "The triggering event",
  Now: "Today",
};

/**
 * The Understand timeline (design 1h).
 *
 * Two things the design insists on, and this implements:
 *  - Opening a point shows WHAT WAS KNOWN AT THE TIME, not what is known now. That is the
 *    educational payoff, and it comes straight from the data.
 *  - The text alternative is required, not optional. A horizontal track of squares means
 *    nothing to a screen reader, so the ordered list below IS the timeline and the visual
 *    track is aria-hidden decoration sitting over it.
 */
export function RoomTimeline({ events }: { events: TimelineEvent[] }) {
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  return (
    <section className="border-b border-[var(--border)] py-8" data-testid="room-timeline">
      <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">
        Understand
      </p>
      <h2 className="mt-2 text-[28px] md:text-[40px]">How we got here</h2>
      <p className="mt-3 max-w-[640px] text-[15px] text-[var(--fg-soft)]">
        Hollow marks are agreed events, solid ones are contested decisions. Opening a point
        shows what was known <em>on that date</em> — not what is known now.
      </p>

      {/* Decorative. The list below carries the content. */}
      <div
        className="mt-8 hidden items-start gap-0 overflow-x-auto md:flex"
        aria-hidden="true"
      >
        {events.map((e, i) => (
          <button
            key={i}
            type="button"
            onClick={() => setOpenIndex(openIndex === i ? null : i)}
            className="w-[118px] flex-none border-t-2 border-[var(--border)] pr-3 pt-4 text-left"
            tabIndex={-1}
          >
            <span
              className="mb-3 inline-block h-4 w-4"
              style={
                e.marker === "Agreed"
                  ? { border: "2px solid var(--fg)" }
                  : e.marker === "Contested"
                    ? { background: "var(--federal)" }
                    : e.marker === "Trigger"
                      ? { background: "var(--state)" }
                      : { background: "var(--accent)" }
              }
            />
            <span className="block text-[13px] font-bold">
              {new Date(e.occurredOn).getFullYear()}
            </span>
            <span className="mt-1 block text-[13px] leading-snug text-[var(--fg-soft)]">
              {e.label}
            </span>
          </button>
        ))}
      </div>

      <ol className="mt-6 flex flex-col md:mt-8" data-testid="timeline-text-alternative">
        {events.map((e, i) => (
          <li key={i} className="border-t border-[var(--border)] py-4">
            <button
              type="button"
              className="min-h-[44px] w-full text-left"
              onClick={() => setOpenIndex(openIndex === i ? null : i)}
              aria-expanded={openIndex === i}
              data-testid="timeline-event"
            >
              <span className="flex flex-wrap items-baseline gap-3">
                <span className="text-[13px] font-bold">
                  {formatDate(e.occurredOn, e.occurredPrecision)}
                </span>
                <span className="text-[16px]">{e.label}</span>
                <span className="text-[11px] uppercase tracking-[0.16em] text-[var(--muted)]">
                  {MARKER_LABEL[e.marker]}
                </span>
              </span>
              {e.description && (
                <span className="mt-1 block text-[14px] text-[var(--fg-soft)]">
                  {e.description}
                </span>
              )}
            </button>

            {openIndex === i && e.whatWasKnownThen && (
              <p
                className="mt-3 bg-[var(--bg-inset)] p-4 text-[14px]"
                data-testid="timeline-known-then"
              >
                <strong>What was known then.</strong> {e.whatWasKnownThen}
              </p>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}

function formatDate(iso: string, precision: TimelineEvent["occurredPrecision"]) {
  const d = new Date(iso);
  if (precision === "Year") return String(d.getFullYear());
  if (precision === "Month") {
    return d.toLocaleDateString(undefined, { year: "numeric", month: "long" });
  }
  return d.toLocaleDateString();
}
