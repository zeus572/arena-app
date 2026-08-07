import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import {
  getRoomMoney,
  type RoomActors,
  type RoomLatest,
  type RoomMoney,
  type ThemeRoomDetail,
  type TimelineEvent,
} from "@/api/rooms";
import { EvidenceMark } from "./EvidenceMark";
import { RoomClaimLedger } from "./RoomClaimLedger";

/**
 * The Situation Board (design 1b).
 *
 * A different destination, not a density dial. Same payload as the reading view — the API
 * serves one object graph to both, which is how "density changes the scaffolding, never the
 * facts" stays structurally true rather than something a reviewer has to keep checking.
 *
 * What changes is the scaffolding: no dek, no explanatory prose, no section introductions.
 * Everything is a labelled cell, and the whole room is meant to be scannable without
 * scrolling on a wide screen. A reader who already knows the story comes here; a reader who
 * does not should be in the reading view.
 */
export function RoomBoard({
  room,
  latest,
  timeline,
  actors,
}: {
  room: ThemeRoomDetail;
  latest: RoomLatest | null;
  timeline: TimelineEvent[];
  actors: RoomActors | null;
}) {
  const [money, setMoney] = useState<RoomMoney | null>(null);

  useEffect(() => {
    let alive = true;
    getRoomMoney(room.slug)
      .then((m) => alive && setMoney(m))
      .catch(() => alive && setMoney(null));
    return () => {
      alive = false;
    };
  }, [room.slug]);

  const now = timeline.filter((e) => e.marker === "Now" || e.marker === "Trigger").slice(-3);

  return (
    <div className="rooms-square" data-testid="room-board">
      {/* --- the sentence, and nothing else above it -------------------------- */}
      <section className="border-b-2 border-[var(--fg)] pb-6 pt-4">
        <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
          Where this stands · r.{room.revision}
        </p>
        <p className="display mt-2 max-w-[1100px] text-[22px] leading-snug md:text-[28px]">
          {room.statusSentenceUnderReview
            ? "This room's status sentence is being rewritten after a claim it rested on changed."
            : room.currentStatusSentence}
        </p>
      </section>

      <div className="grid gap-x-8 gap-y-0 lg:grid-cols-3">
        {/* --- latest ---------------------------------------------------------- */}
        <BoardCell
          title="Latest"
          note={
            latest
              ? `${latest.developments.length} logged · ${latest.excludedCount} left out`
              : undefined
          }
        >
          <ul className="flex flex-col">
            {(latest?.developments ?? []).slice(0, 8).map((d) => (
              <li
                key={d.id}
                className="border-t border-[var(--border)] py-2"
                data-testid="board-development"
              >
                <div className="flex items-baseline gap-2">
                  <span className="shrink-0 text-[11px] tabular-nums text-[var(--muted)]">
                    {new Date(d.occurredAt).toLocaleDateString(undefined, {
                      month: "short",
                      day: "numeric",
                    })}
                  </span>
                  <EvidenceMark status={d.evidenceStatus} size="inline" withWord={false} />
                  <span className="text-[14px] leading-snug">{d.headline}</span>
                </div>
              </li>
            ))}
          </ul>
        </BoardCell>

        {/* --- money ----------------------------------------------------------- */}
        <BoardCell
          title="Money"
          note={money ? `${money.items.length} items · no total across stages` : undefined}
        >
          <ul className="flex flex-col">
            {(money?.items ?? []).map((m) => (
              <li
                key={m.id}
                className="border-t border-[var(--border)] py-2"
                data-testid="board-money"
              >
                <div className="flex flex-wrap items-baseline justify-between gap-x-3">
                  <span className="text-[14px] leading-snug">{m.title}</span>
                  <span className="shrink-0 text-[14px] tabular-nums">
                    {m.amountUsd === null ? "no figure" : compactUsd(m.amountUsd)}
                  </span>
                </div>
                {/* The stage travels with the number everywhere, including here. */}
                <p className="text-[11px] uppercase tracking-[0.14em] text-[var(--muted)]">
                  {m.currentStage}
                </p>
              </li>
            ))}
          </ul>
        </BoardCell>

        {/* --- who decides ----------------------------------------------------- */}
        <BoardCell title="Who decides" note={actors ? `${actors.decides.length} with a vote` : undefined}>
          <ul className="flex flex-col">
            {(actors?.decides ?? []).map((a) => (
              <li key={a.id} className="border-t border-[var(--border)] py-2">
                <p className="text-[14px] leading-snug">{a.name}</p>
                <p className="text-[12px] text-[var(--muted)]">{a.leverageStatement}</p>
              </li>
            ))}
          </ul>
        </BoardCell>

        {/* --- unresolved ------------------------------------------------------ */}
        <BoardCell title="Unresolved">
          <p className="text-[16px] leading-snug">{room.topUnresolvedQuestion}</p>
        </BoardCell>

        {/* --- watch next ------------------------------------------------------ */}
        <BoardCell title="Watch next">
          <p className="text-[16px] leading-snug">{room.watchNext}</p>
        </BoardCell>

        {/* --- where we are on the clock --------------------------------------- */}
        <BoardCell title="Most recent turns">
          <ul className="flex flex-col">
            {now.map((e) => (
              <li key={`${e.occurredOn}-${e.label}`} className="border-t border-[var(--border)] py-2">
                <p className="text-[11px] tabular-nums text-[var(--muted)]">{e.occurredOn}</p>
                <p className="text-[14px] leading-snug">{e.label}</p>
              </li>
            ))}
          </ul>
        </BoardCell>
      </div>

      {/* --- the ledger, full width ------------------------------------------- */}
      <section className="border-t-2 border-[var(--fg)] pt-4">
        <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
          Claims &amp; evidence
        </p>
        <RoomClaimLedger slug={room.slug} compact />
      </section>

      <p className="mt-8 border-t border-[var(--border)] pt-3 text-[13px] text-[var(--muted)]">
        The board and the reading view are served the same payload. Nothing is omitted here
        that changes what is true — only the explanation around it.{" "}
        <Link to={`/rooms/${room.slug}`} className="underline">
          Reading view
        </Link>
        .
      </p>
    </div>
  );
}

function BoardCell({
  title,
  note,
  children,
}: {
  title: string;
  note?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="border-b border-[var(--border)] py-5">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <h2 className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">{title}</h2>
        {note && <p className="text-[11px] text-[var(--muted)]">{note}</p>}
      </div>
      <div className="mt-2">{children}</div>
    </section>
  );
}

/**
 * Short form for the board only, where the column is narrow and every row carries its stage.
 * The reading view spells the scale out in words; here "$1.15T" sits directly above the word
 * "Requested", so the stage is never lost even when the magnitude is abbreviated.
 */
function compactUsd(amount: number): string {
  const abs = Math.abs(amount);
  if (abs >= 1e12) return `$${trim(amount / 1e12)}T`;
  if (abs >= 1e9) return `$${trim(amount / 1e9)}B`;
  if (abs >= 1e6) return `$${trim(amount / 1e6)}M`;
  return `$${amount.toLocaleString()}`;
}

function trim(n: number): string {
  return n.toFixed(2).replace(/\.?0+$/, "");
}
