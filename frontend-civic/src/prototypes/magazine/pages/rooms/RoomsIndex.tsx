import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listRooms, type RoomSummary } from "@/api/rooms";

/**
 * Room discovery. The handoff lists this as "not designed yet", so this is deliberately
 * plain: a list that tells you what each room is and when it last actually changed.
 */
export default function RoomsIndex() {
  const [rooms, setRooms] = useState<RoomSummary[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    listRooms()
      .then((r) => alive && setRooms(r))
      .catch(() => alive && setFailed(true))
      .finally(() => alive && setLoaded(true));
    return () => {
      alive = false;
    };
  }, []);

  return (
    <div className="rooms-square py-8" data-testid="rooms-index">
      <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">Rooms</p>
      <h1 className="mt-2 text-[34px] md:text-[44px]">Topic rooms</h1>
      <p className="mt-3 max-w-[640px] text-[16px] text-[var(--fg-soft)]">
        A room is a durable home for one ongoing subject. It does not expire when the news
        cycle moves on, and it tells you what actually changed since you were last here.
      </p>

      {!loaded && (
        <p className="mt-8 text-sm text-[var(--muted)]" data-testid="rooms-loading">
          Loading rooms…
        </p>
      )}

      {loaded && failed && (
        <p className="mt-8 text-sm text-[var(--state)]" data-testid="rooms-error">
          Could not load rooms. Try again in a moment.
        </p>
      )}

      {loaded && !failed && rooms.length === 0 && (
        <p className="mt-8 text-sm text-[var(--muted)]" data-testid="rooms-empty">
          No rooms are published yet.
        </p>
      )}

      <div className="mt-8 flex flex-col">
        {rooms.map((room) => (
          <Link
            key={room.id}
            to={`/rooms/${room.slug}`}
            className="border-t border-[var(--border)] py-6"
            data-testid="room-card"
          >
            <p className="text-[10px] uppercase tracking-[0.2em] text-[var(--muted)]">
              {room.kind === "Theme" ? "Theme room" : `Story · ${room.storyType ?? ""}`}
            </p>
            <h2 className="mt-2 text-[24px] leading-snug">{room.title}</h2>
            <p className="mt-2 max-w-[640px] text-[15px] text-[var(--fg-soft)]">{room.dek}</p>
            <p className="mt-3 text-[12px] text-[var(--muted)]">
              {room.lastMeaningfulUpdateAt
                ? `Last meaningful update ${new Date(room.lastMeaningfulUpdateAt).toLocaleDateString()}`
                : "No changes logged yet"}
              {" · "}r.{room.revision}
            </p>
          </Link>
        ))}
      </div>
    </div>
  );
}
