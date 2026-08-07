import { useState } from "react";
import type { ChangeLogEntry, RoomDelta } from "@/api/rooms";
import { EvidenceMark } from "./EvidenceMark";
import type { ClaimStatus } from "@/api/rooms";

/**
 * The change ribbon and its unfurled delta ledger (designs 1a and 1d).
 *
 * Two rules from the handoff are load-bearing here and easy to lose:
 *
 *  1. Corrections are NEVER folded into "updated". The API returns them in a separate
 *     array precisely so this component cannot merge them by accident, and they get their
 *     own band with their own label.
 *  2. The withheld edits are COUNTED HONESTLY. "11 edits we did not bother you with" is
 *     the line that earns the reader's trust in everything above it — hiding the number
 *     would make the ribbon look like it was suppressing things.
 */
export function DeltaRibbon({ delta, slug }: { delta: RoomDelta; slug: string }) {
  const [open, setOpen] = useState(false);

  const changeTypes = [
    ...new Set([...delta.corrections, ...delta.meaningfulChanges].map((c) => c.label)),
  ];
  const total = delta.meaningfulChanges.length + delta.corrections.length;

  return (
    <section
      className="border-b border-[var(--border)]"
      data-testid="delta-ribbon"
      data-changes={total}
    >
      <div className="flex flex-wrap items-center gap-3 py-3.5">
        <span
          aria-hidden
          className="inline-block h-[7px] w-[7px] bg-[var(--accent)]"
        />
        <span className="text-[14px] font-semibold">
          {total} {total === 1 ? "change" : "changes"} since your last visit
        </span>
        {/* Change TYPES, never article counts — the ribbon reports what kind of thing
            happened, not how much was written about it. */}
        <span className="text-[13px] text-[var(--muted)]">{changeTypes.join(" · ")}</span>
        <button
          type="button"
          className="ml-auto inline-flex min-h-[44px] items-center border-b border-[var(--fg)] text-[13px]"
          onClick={() => setOpen((v) => !v)}
          data-testid="delta-toggle"
        >
          {open ? "Hide" : "Show me"}
        </button>
      </div>

      {open && (
        <div className="border-t border-[var(--border)] pb-6" data-testid="delta-ledger">
          <p className="display mt-5 text-[21px] leading-snug md:text-[26px]">
            {total} meaningful {total === 1 ? "change" : "changes"}.
            <br />
            <span className="text-[var(--muted)]">
              {delta.withheldCount}{" "}
              {delta.withheldCount === 1 ? "edit" : "edits"} we did not bother you with.
            </span>
          </p>
          <p className="mt-1 text-[12px] text-[var(--muted)]">
            r.{delta.fromRevision} → r.{delta.toRevision}
          </p>

          {/* Corrections first, and visually separated. */}
          {delta.corrections.length > 0 && (
            <div className="mt-6" data-testid="delta-corrections">
              {delta.corrections.map((c, i) => (
                <DeltaRow key={`c-${i}`} entry={c} correction />
              ))}
            </div>
          )}

          {delta.meaningfulChanges.map((c, i) => (
            <DeltaRow key={`m-${i}`} entry={c} />
          ))}

          {delta.withheldByType.length > 0 && (
            <div
              className="mt-6 bg-[var(--bg-inset)] p-4 text-[13px] text-[var(--muted)]"
              data-testid="delta-withheld"
            >
              Not shown:{" "}
              {delta.withheldByType.map((w) => `${w.count} × ${w.type}`).join(", ")}.{" "}
              <a href={`/rooms/${slug}/changelog`} className="underline">
                Full changelog
              </a>
            </div>
          )}
        </div>
      )}
    </section>
  );
}

function DeltaRow({ entry, correction }: { entry: ChangeLogEntry; correction?: boolean }) {
  const isStatusMove = entry.fromValue && entry.toValue;

  return (
    <div
      className="flex gap-4 border-t border-[var(--border)] py-4"
      data-testid={correction ? "delta-row-correction" : "delta-row"}
    >
      <span
        className={[
          "w-[70px] flex-none pt-[2px] text-[11px] font-bold uppercase tracking-[0.16em]",
          correction ? "text-[var(--accent)]" : "text-[var(--federal)]",
        ].join(" ")}
      >
        {entry.label}
      </span>
      <div className="min-w-0">
        <p className="text-[16px] font-semibold">{entry.headline}</p>
        {entry.whyItMatters && (
          <p className="mt-1 text-[14px] text-[var(--fg-soft)]">{entry.whyItMatters}</p>
        )}
        {isStatusMove && (
          <p className="mt-2 flex flex-wrap items-center gap-2 text-[13px]">
            {/* The transition rendered literally: old mark, word, arrow, new mark, word. */}
            <EvidenceMark status={entry.fromValue as ClaimStatus} size="inline" />
            <span aria-hidden>→</span>
            <EvidenceMark status={entry.toValue as ClaimStatus} size="inline" />
          </p>
        )}
      </div>
    </div>
  );
}
