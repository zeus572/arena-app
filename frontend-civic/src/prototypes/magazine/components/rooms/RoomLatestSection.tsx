import { Link } from "react-router-dom";
import type { RoomLatest } from "@/api/rooms";
import { EvidenceMark } from "./EvidenceMark";

/**
 * The Latest section (design 1g).
 *
 * The sidebar is not decoration. A bounded list is only trustworthy if the bound is
 * stated, so "what we left out" ships beside the list with the real numbers and the
 * inclusion rule printed in full. An empty room says so plainly rather than hiding the
 * section — zero of zero is a true disclosure, and padding it would not be.
 */
export function RoomLatestSection({ latest }: { latest: RoomLatest }) {
  const count = latest.developments.length;

  return (
    <section className="border-b border-[var(--border)] py-8" data-testid="room-latest">
      <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">Latest</p>
      <h2 className="mt-2 text-[28px] md:text-[40px]">What has changed</h2>
      <p className="mt-3 max-w-[640px] text-[15px] text-[var(--fg-soft)]">
        {count === 0 ? (
          <>
            No developments logged yet. We have considered {latest.articlesConsidered}{" "}
            candidate{latest.articlesConsidered === 1 ? "" : "s"} in the last{" "}
            {latest.windowDays} days and judged none of them to have changed anything.
          </>
        ) : (
          <>
            {count} development{count === 1 ? "" : "s"} in {latest.windowDays} days. We
            logged {latest.articlesConsidered} candidates and judged {count} of them to
            have changed something.
          </>
        )}
      </p>

      <div className="mt-6 grid gap-8 md:grid-cols-[1fr_290px]">
        <div>
          {latest.developments.map((d) => (
            <div
              key={d.id}
              className="flex flex-col gap-2 border-t border-[var(--border)] py-5 md:flex-row md:gap-6"
              data-testid="development-row"
            >
              <time
                className="w-[110px] flex-none text-[13px] text-[var(--muted)]"
                dateTime={d.occurredAt}
              >
                {new Date(d.occurredAt).toLocaleDateString()}
              </time>
              <div className="min-w-0">
                <p className="text-[11px] uppercase tracking-[0.18em] text-[var(--muted)]">
                  {d.category}
                </p>
                <h3 className="mt-1 text-[19px] leading-snug md:text-[21px]">
                  {d.headline}
                </h3>
                <p className="mt-2 text-[15px]">
                  <strong>Why it matters.</strong> {d.whyItMatters}
                </p>
                <p className="mt-3 flex flex-wrap items-center gap-3 text-[12px] text-[var(--muted)]">
                  <EvidenceMark status={d.evidenceStatus} size="inline" />
                  <span>{d.inclusionReason}</span>
                  {d.storySlug && (
                    <Link to={`/rooms/${d.storySlug}`} className="underline">
                      Story room
                    </Link>
                  )}
                </p>
              </div>
            </div>
          ))}
          {count === 0 && (
            <p
              className="border-t border-[var(--border)] py-6 text-[14px] text-[var(--muted)]"
              data-testid="latest-empty"
            >
              Nothing has met the bar yet. That is a real answer, not a loading state.
            </p>
          )}
        </div>

        <aside className="bg-[var(--bg-inset)] p-5" data-testid="latest-what-we-left-out">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            What we left out
          </p>
          <p className="mt-2 text-[14px]">
            {latest.excludedCount} candidate{latest.excludedCount === 1 ? "" : "s"} did not
            meet the bar. Something is logged here only when:
          </p>
          <ul className="mt-3 flex list-disc flex-col gap-1.5 pl-4 text-[13px] text-[var(--fg-soft)]">
            {latest.inclusionRules.map((r) => (
              <li key={r}>{r}</li>
            ))}
          </ul>
          {latest.exclusionRules.length > 0 && (
            <p className="mt-4 text-[13px] text-[var(--muted)]">
              {latest.exclusionRules[0]}
            </p>
          )}
        </aside>
      </div>
    </section>
  );
}
