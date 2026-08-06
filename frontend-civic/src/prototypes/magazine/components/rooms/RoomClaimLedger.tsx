import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { getRoomClaims, type RoomClaims } from "@/api/rooms";
import { EvidenceMark, STATUS_WORD } from "./EvidenceMark";

/**
 * The claims ledger (design 1n).
 *
 * Sorted least-settled first, which is the opposite of flattering. Design 1n gives the
 * reason in one line: that is where you are most likely to be misled. A page that led with
 * its eleven confirmed claims would be advertising; this leads with the disputed one.
 *
 * False and Unsupported claims stay in the list. Removing them would erase the record that
 * the claim exists and what the evidence does about it, which is the more useful fact.
 */
export function RoomClaimLedger({
  slug,
  compact = false,
}: {
  slug: string;
  compact?: boolean;
}) {
  const [data, setData] = useState<RoomClaims | null>(null);

  useEffect(() => {
    let alive = true;
    getRoomClaims(slug)
      .then((c) => alive && setData(c))
      .catch(() => alive && setData(null));
    return () => {
      alive = false;
    };
  }, [slug]);

  if (!data || data.total === 0) return null;

  const shown = compact ? data.claims.slice(0, 6) : data.claims;

  return (
    <section
      className={compact ? "" : "border-b border-[var(--border)] py-8"}
      data-testid="room-claims"
    >
      {!compact && (
        <>
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">
            Claims &amp; evidence
          </p>
          <h2 className="mt-2 text-[28px] md:text-[40px]">What we can and cannot say</h2>
        </>
      )}

      <div className="mt-4 flex flex-wrap items-baseline gap-x-6 gap-y-1">
        <p className="text-[14px]">
          <strong>{data.unsettledCount}</strong> of {data.total} claims in this room are
          unsettled.
        </p>
        {/* The shape of the evidence is information: eleven confirmed and one disputed is a
            different room from three of each, and the sorted list alone does not show it. */}
        <ul className="flex flex-wrap gap-x-4 gap-y-1">
          {Object.entries(data.countsByStatus).map(([status, count]) => (
            <li key={status} className="text-[12px] text-[var(--muted)]">
              {STATUS_WORD[status as keyof typeof STATUS_WORD] ?? status} · {count}
            </li>
          ))}
        </ul>
      </div>

      <p className="mt-2 text-[13px] text-[var(--muted)]">
        Least settled first — that is where you are most likely to be misled.
      </p>

      <ul className="mt-4 flex flex-col">
        {shown.map((c) => (
          <li
            key={c.id}
            className="border-t border-[var(--border)] py-3"
            data-testid="ledger-claim"
            data-status={c.status}
          >
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <EvidenceMark status={c.status} size="inline" />
              <Link
                to={`/claims/${c.slug}`}
                className="max-w-[720px] text-[15px] leading-snug underline decoration-[var(--border)] underline-offset-2"
              >
                {c.text}
              </Link>
            </div>

            {!compact && (
              <div className="mt-1.5 flex flex-wrap gap-x-5 gap-y-0.5 pl-1 text-[12px] text-[var(--muted)]">
                <span>{c.kind}</span>
                <span>
                  {c.supportingCount} supporting · {c.contradictingCount} contradicting
                </span>
              </div>
            )}

            {!compact && c.whatWouldSettleIt && (
              <p className="mt-1.5 max-w-[720px] pl-1 text-[13px] text-[var(--fg-soft)]">
                <span className="text-[var(--muted)]">Would be settled by:</span>{" "}
                {c.whatWouldSettleIt}
              </p>
            )}
          </li>
        ))}
      </ul>

      {compact && data.claims.length > shown.length && (
        <p className="mt-3 text-[12px] text-[var(--muted)]">
          {data.claims.length - shown.length} more in the reading view.
        </p>
      )}
    </section>
  );
}
