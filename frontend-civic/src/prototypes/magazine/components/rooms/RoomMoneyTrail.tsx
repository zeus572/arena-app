import { useEffect, useState } from "react";

import { getRoomMoney, type RoomMoney, type RoomMoneyItem, type RoomMoneyStage } from "@/api/rooms";

/**
 * The Money Trail (PRD 05, designs 1s and 1t).
 *
 * Every item renders all five rungs including the empty ones, because the empty rungs are
 * usually the story: $1.15 trillion that has only been requested looks exactly like $1.15
 * trillion that has been spent unless four blanks are visible above it.
 *
 * There is no total across the ladder anywhere in this file, and there must never be one.
 * The same dollars appear at Requested, Appropriated, Obligated and Spent as they move, so
 * adding the rungs double-counts them. The API refuses to compute it; this refuses to show it.
 */
export function RoomMoneyTrail({ slug }: { slug: string }) {
  const [money, setMoney] = useState<RoomMoney | null>(null);

  useEffect(() => {
    let alive = true;
    getRoomMoney(slug)
      .then((m) => alive && setMoney(m))
      .catch(() => alive && setMoney(null));
    return () => {
      alive = false;
    };
  }, [slug]);

  if (!money || money.items.length === 0) return null;

  return (
    <section className="border-b border-[var(--border)] py-8" data-testid="room-money">
      <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">Money trail</p>
      <h2 className="mt-2 text-[28px] md:text-[40px]">Where the money actually is</h2>
      <p className="mt-3 max-w-[680px] text-[15px] text-[var(--fg-soft)]">
        Federal money passes through five legal stages. Coverage usually names the first and
        uses the verbs of the last. Each item below shows every stage, including the ones
        that are empty.
      </p>

      <div className="mt-8 flex flex-col gap-10">
        {money.items.map((item) => (
          <MoneyItemCard key={item.id} item={item} />
        ))}
      </div>

      <p className="mt-8 border-t border-[var(--border)] pt-3 text-[13px] text-[var(--muted)]">
        These figures are entered by hand from the sources cited. There is no automated
        appropriations feed behind this section, and nothing here is summed across stages —
        the same dollars appear at more than one rung as they move.
      </p>
    </section>
  );
}

function MoneyItemCard({ item }: { item: RoomMoneyItem }) {
  const reached = item.stages.filter((s) => s.applicability === "Present").length;

  return (
    <article
      className="border-t-2 border-[var(--fg)] pt-4"
      data-testid="money-item"
      data-slug={item.slug}
    >
      <div className="flex flex-col gap-1 md:flex-row md:items-baseline md:justify-between md:gap-8">
        <h3 className="text-[20px] leading-snug md:text-[24px]">{item.title}</h3>
        <p className="shrink-0 text-[12px] uppercase tracking-[0.16em] text-[var(--muted)]">
          {item.periodLabel}
        </p>
      </div>

      {/* The headline number always travels with the verb that is true of it. */}
      <p className="mt-3 flex flex-wrap items-baseline gap-x-3">
        <span className="display text-[30px] md:text-[38px]">{formatUsd(item.amountUsd)}</span>
        <span className="text-[15px] text-[var(--fg-soft)]">{item.currentStageVerb}</span>
      </p>

      {item.isMultiYear && (
        <p className="mt-1 text-[13px] text-[var(--state)]">
          This is a multi-year total, not an annual figure.
        </p>
      )}

      {/* --- the ladder ------------------------------------------------------- */}
      <ol className="mt-5" data-testid="money-ladder">
        {item.stages.map((stage) => (
          <LadderRung key={stage.stage} stage={stage} />
        ))}
      </ol>

      <p className="mt-2 text-[12px] text-[var(--muted)]">
        {reached} of {item.stages.length} stages reached.
        {!item.canSaySpent && " No part of this has been spent."}
      </p>

      {/* --- what it does not mean -------------------------------------------- */}
      {/* Inverse panel, not a tooltip: what a number is not is as important as the number. */}
      <div className="mt-5 bg-[var(--fg)] p-5 text-[var(--bg)]" data-testid="money-does-not-mean">
        <p className="text-[11px] uppercase tracking-[0.2em] opacity-70">
          What this does not mean
        </p>
        <p className="mt-2 max-w-[680px] text-[15px] leading-snug">
          {item.whatThisDoesNotMean}
        </p>
      </div>

      <div className="mt-5 grid gap-6 md:grid-cols-2">
        {item.decidesNext && (
          <div>
            <h4 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
              Who decides next
            </h4>
            <p className="mt-1 text-[15px]">{item.decidesNext}</p>
          </div>
        )}

        {item.estimateMethod && (
          <div>
            <h4 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
              How this figure was arrived at
            </h4>
            <p className="mt-1 text-[15px]">{item.estimateMethod}</p>
          </div>
        )}

        {item.exclusions.length > 0 && (
          <div>
            <h4 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
              Not included in this figure
            </h4>
            <ul className="mt-1 flex flex-col gap-1">
              {item.exclusions.map((e) => (
                <li key={e} className="text-[14px] text-[var(--fg-soft)]">
                  {e}
                </li>
              ))}
            </ul>
          </div>
        )}

        {item.comparisons.length > 0 && (
          <div data-testid="money-comparisons">
            <h4 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
              For scale
            </h4>
            <ul className="mt-1 flex flex-col gap-2">
              {item.comparisons.map((c) => (
                <li key={c.text} className="text-[14px]">
                  {/* A refused comparison is shown WITH its reason. The reader learns more
                      from seeing a bad comparison rejected than from never seeing it. */}
                  <span className={c.accepted ? "" : "text-[var(--muted)] line-through"}>
                    {c.text}
                  </span>
                  {!c.accepted && c.rejectionReason && (
                    <span className="mt-0.5 block text-[13px] text-[var(--state)]">
                      We do not use this: {c.rejectionReason}
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </article>
  );
}

function LadderRung({ stage }: { stage: RoomMoneyStage }) {
  const present = stage.applicability === "Present";
  const na = stage.applicability === "NotApplicable";

  return (
    <li
      className="grid grid-cols-[104px_1fr] items-baseline gap-x-4 border-t border-[var(--border)] py-2.5 md:grid-cols-[132px_140px_1fr]"
      data-testid={`money-rung-${stage.stage}`}
      data-applicability={stage.applicability}
    >
      <span
        className={
          "text-[11px] uppercase tracking-[0.16em] " +
          (present ? "text-[var(--fg)]" : "text-[var(--muted)]")
        }
      >
        {stage.stage}
      </span>

      <span
        className={
          "text-[17px] tabular-nums " + (present ? "" : "text-[var(--muted)]")
        }
      >
        {present ? formatUsd(stage.amountUsd) : na ? "—" : "empty"}
      </span>

      <span className="col-span-2 text-[13px] text-[var(--muted)] md:col-span-1">
        {na
          ? stage.notApplicableReason
          : present
            ? [stage.sourceOrganization, stage.asOf ? new Date(stage.asOf).toLocaleDateString() : null]
                .filter(Boolean)
                .join(" · ")
            : "Not reached yet."}
      </span>
    </li>
  );
}

/**
 * Whole units only, with the scale word spelled out.
 *
 * "$1.15 trillion" rather than "$1,150,000,000,000" because the second is unreadable, and
 * never a bare "1.15T" because an abbreviated scale is the easiest thing in this whole
 * section to misread by three orders of magnitude.
 */
function formatUsd(amount: number | null): string {
  if (amount === null) return "No figure published";

  const abs = Math.abs(amount);
  if (abs >= 1e12) return `$${trim(amount / 1e12)} trillion`;
  if (abs >= 1e9) return `$${trim(amount / 1e9)} billion`;
  if (abs >= 1e6) return `$${trim(amount / 1e6)} million`;
  return `$${amount.toLocaleString()}`;
}

function trim(n: number): string {
  return n.toFixed(2).replace(/\.?0+$/, "");
}
