import type { ThemeRoomDetail } from "@/api/rooms";
import { EvidenceMarkLegend } from "./EvidenceMark";
import { RoomSourceList } from "./RoomSourceList";

/**
 * Sources & Methodology (design 1l), minus its social half.
 *
 * The Conversation Map is deliberately not built — there is no compliant social connector,
 * no deletion reconciliation and no trust-and-safety owner, and PRD 08 Gate 3 forbids
 * shipping public-reaction ingestion without all of them. Rather than quietly omit the
 * section, the page says so. A methodology page with a silent gap is worse than one that
 * names what it does not cover.
 *
 * The evidence-mark key lives here too, because the vocabulary has to be explained
 * somewhere a reader can actually find it.
 */

export function RoomSources({ room }: { room: ThemeRoomDetail }) {
  return (
    <section className="py-8" data-testid="room-sources">
      <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">
        Sources &amp; methodology
      </p>
      <h2 className="mt-2 text-[28px] md:text-[40px]">How this room is made</h2>

      <div className="mt-6">
        <RoomSourceList slug={room.slug} />
      </div>

      <div className="mt-8 grid gap-8 md:grid-cols-2">
        <div>
          <h3 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            What counts as a change
          </h3>
          <p className="mt-2 text-[15px]">{room.scopeStatement}</p>

          {room.terminologyNotes.length > 0 && (
            <div className="mt-6" data-testid="room-terminology">
              <h3 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
                Words we chose carefully
              </h3>
              <dl className="mt-3 flex flex-col">
                {room.terminologyNotes.map((n) => (
                  <div key={n.term} className="border-t border-[var(--border)] py-3">
                    <dt className="text-[14px] font-semibold">{n.term}</dt>
                    <dd className="mt-1 text-[14px] text-[var(--fg-soft)]">{n.note}</dd>
                  </div>
                ))}
              </dl>
            </div>
          )}

          <div
            className="mt-6 border border-[var(--state)] bg-[var(--state-soft)] p-4"
            data-testid="no-conversation-map"
          >
            <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--state)]">
              Not in this room
            </p>
            <p className="mt-2 text-[14px]">
              There is no Conversation Map here. Summarising public discussion responsibly
              needs compliant access to each platform, a way to honour deletions, and a
              person reviewing every featured example. Until all three exist, showing a
              partial version would imply a rigour we do not have.
            </p>
          </div>
        </div>

        <div>
          <h3 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            What the marks mean
          </h3>
          <div className="mt-3">
            <EvidenceMarkLegend />
          </div>
          <p className="mt-5 text-[13px] text-[var(--muted)]">
            Claims marked <strong>False</strong> or <strong>Unsupported</strong> stay in the
            ledger. Deleting them would erase the record that the claim exists and what the
            evidence does about it.
          </p>
        </div>
      </div>
    </section>
  );
}
