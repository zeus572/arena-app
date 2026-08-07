import { useCallback, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import {
  followRoom,
  getRoomActors,
  getRoomLatest,
  getRoomTimeline,
  getRoom,
  markRoomSeen,
  unfollowRoom,
  type RoomActors,
  type RoomLatest,
  type StoryRoomDetail,
  type ThemeRoomDetail,
  type TimelineEvent,
} from "@/api/rooms";
import { Button } from "../../components/Button";
import { EvidenceMark } from "../../components/rooms/EvidenceMark";
import { DeltaRibbon } from "../../components/rooms/DeltaRibbon";
import { RoomLatestSection } from "../../components/rooms/RoomLatestSection";
import { RoomTimeline } from "../../components/rooms/RoomTimeline";
import { RoomActorMap } from "../../components/rooms/RoomActorMap";
import { RoomBoard } from "../../components/rooms/RoomBoard";
import { RoomClaimLedger } from "../../components/rooms/RoomClaimLedger";
import { RoomInteractions } from "../../components/rooms/RoomInteractions";
import { RoomMoneyTrail } from "../../components/rooms/RoomMoneyTrail";
import { RoomSources } from "../../components/rooms/RoomSources";
import { StoryRoom } from "./StoryRoom";

/**
 * The Theme Room front door (design 1a, "Dispatch").
 *
 * Orient someone in sixty seconds and make the next thing to watch unmissable. The order
 * is load-bearing: status sentence, then what changed, then the three essential facts,
 * then the open question and what to watch.
 *
 * Board view (design 1b) is a separate destination rather than a density dial across every
 * module — the cheaper of the two paths the handoff left open. `?view=board` switches it,
 * following the pattern /bills already uses for its three views.
 */
export default function RoomDetail() {
  const { slug = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const board = searchParams.get("view") === "board";

  const [room, setRoom] = useState<ThemeRoomDetail | null>(null);
  const [story, setStory] = useState<StoryRoomDetail | null>(null);
  const [latest, setLatest] = useState<RoomLatest | null>(null);
  const [timeline, setTimeline] = useState<TimelineEvent[]>([]);
  const [actors, setActors] = useState<RoomActors | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [missing, setMissing] = useState(false);
  const [following, setFollowing] = useState(false);
  const [followBusy, setFollowBusy] = useState(false);
  const [followError, setFollowError] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    setLoaded(false);
    setMissing(false);
    setStory(null);

    (async () => {
      const detail = await getRoom(slug).catch(() => undefined);
      if (!alive) return;

      if (!detail) {
        setMissing(true);
        setLoaded(true);
        return;
      }

      // One URL, two shapes. A Story Room has none of the sections below it — no status
      // sentence, no Latest, no actor map — so it renders its own page rather than
      // borrowing the Theme Room's and leaving most of it blank.
      if (detail.kind === "Story") {
        setStory(detail);
        setLoaded(true);
        markRoomSeen(slug, detail.revision).catch(() => {
          /* Best effort; a failed bookmark must not break the page. */
        });
        return;
      }

      setRoom(detail);
      setFollowing(detail.viewer.following);

      // Sections load in parallel and each degrades on its own — a room whose timeline
      // fails to load is still worth reading.
      const [l, t, a] = await Promise.all([
        getRoomLatest(slug).catch(() => null),
        getRoomTimeline(slug).catch(() => []),
        getRoomActors(slug).catch(() => null),
      ]);
      if (!alive) return;

      setLatest(l);
      setTimeline(t);
      setActors(a);
      setLoaded(true);

      // Mark seen only AFTER the delta has been rendered from this payload — doing it
      // earlier would clear the ribbon the reader came here to see.
      markRoomSeen(slug, detail.revision).catch(() => {
        /* Best effort; a failed bookmark must not break the page. */
      });
    })();

    return () => {
      alive = false;
    };
  }, [slug]);

  const toggleFollow = useCallback(async () => {
    if (!room) return;
    setFollowBusy(true);
    setFollowError(null);
    const next = !following;
    try {
      if (next) await followRoom(room.slug);
      else await unfollowRoom(room.slug);
      setFollowing(next);
    } catch {
      // Never swallow silently — the user pressed a button and deserves to know.
      setFollowError(
        next
          ? "Could not follow this room. You may need to sign in and verify your email."
          : "Could not unfollow this room.",
      );
    } finally {
      setFollowBusy(false);
    }
  }, [room, following]);

  if (!loaded) {
    return (
      <p className="py-16 text-sm text-[var(--muted)]" data-testid="room-loading">
        Loading room…
      </p>
    );
  }

  if (story) return <StoryRoom room={story} />;

  if (missing || !room) {
    return (
      <div className="py-16" data-testid="room-missing">
        <h1 className="text-2xl">Room not found</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">
          It may not be published yet, or it may be scoped to a different state.
        </p>
        <Link to="/rooms" className="mt-4 inline-block text-sm underline">
          All rooms
        </Link>
      </div>
    );
  }

  // A separate destination, not a density dial. Same fetched payload either way — the
  // board omits explanation, never facts.
  if (board) {
    return (
      <>
        <BoardHeader room={room} onExit={() => setSearchParams({}, { replace: true })} />
        <RoomBoard room={room} latest={latest} timeline={timeline} actors={actors} />
      </>
    );
  }

  return (
    <article className="rooms-square" data-testid="room-detail" data-slug={room.slug}>
      {/* --- header ------------------------------------------------------------ */}
      <header className="flex flex-col gap-6 border-b border-[var(--border)] pb-8 pt-6 md:flex-row md:gap-10">
        <div className="flex-1">
          <p className="text-[11px] font-bold uppercase tracking-[0.24em] text-[var(--accent)]">
            Theme Room · {room.status} · Reviewed {room.monitoringCadence.toLowerCase()}
          </p>
          <h1 className="mt-3 max-w-[640px] text-[34px] leading-[1.05] md:text-[52px]">
            {room.title}
          </h1>
          <p className="mt-4 max-w-[640px] text-[17px] text-[var(--fg-soft)] md:text-[19px]">
            {room.dek}
          </p>
          {room.alternateTitles.length > 0 && (
            <p className="mt-3 text-[13px] text-[var(--muted)]">
              Also called {room.alternateTitles.join(", ")}.
            </p>
          )}
          {room.contentNote && (
            <p
              className="mt-4 border-l-[3px] border-[var(--state)] bg-[var(--state-soft)] px-4 py-2 text-[14px]"
              data-testid="room-content-note"
            >
              {room.contentNote}
            </p>
          )}
        </div>

        <aside className="w-full border-[var(--border)] md:w-[212px] md:border-l md:pl-6">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            Last meaningful update
          </p>
          <p className="mt-1 text-[14px]">
            {room.lastMeaningfulUpdateAt
              ? new Date(room.lastMeaningfulUpdateAt).toLocaleDateString()
              : "No changes logged yet"}
          </p>
          <p className="mt-3 text-[12px] text-[var(--muted)]">Revision r.{room.revision}</p>

          <div className="mt-5">
            <Button
              variant={following ? "secondary" : "primary"}
              size="sm"
              fullWidth
              onClick={toggleFollow}
              disabled={followBusy}
              // The shared Button's sm size is 34px tall. Rooms are designed to 44px touch
              // targets (1aa/1bb), and there is an e2e test at 390px that measures it, so
              // the floor is set here rather than by resizing Button for the whole app.
              className="min-h-[44px]"
              data-testid="room-follow"
            >
              {followBusy ? "…" : following ? "Following" : "Follow this room"}
            </Button>
            {followError && (
              <p className="mt-2 text-[12px] text-[var(--state)]" data-testid="room-follow-error">
                {followError}
              </p>
            )}
          </div>

          <button
            type="button"
            className="mt-4 inline-flex min-h-[44px] items-center text-[12px] underline"
            onClick={() =>
              setSearchParams(board ? {} : { view: "board" }, { replace: true })
            }
            data-testid="room-view-toggle"
          >
            {board ? "Back to reading view" : "Situation board"}
          </button>
        </aside>
      </header>

      {/* --- status sentence --------------------------------------------------- */}
      <section
        className="border-b border-[var(--border)] border-t-2 border-t-[var(--fg)] py-7"
        data-testid="room-status"
      >
        <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
          Where this stands
        </p>
        {room.statusSentenceUnderReview ? (
          <p
            className="mt-3 max-w-[960px] text-[17px] text-[var(--state)]"
            data-testid="room-status-under-review"
          >
            This room's status sentence is being rewritten after a claim it rested on
            changed. Everything below is unaffected — the facts and their evidence marks are
            current.
          </p>
        ) : (
          <p className="display mt-3 max-w-[960px] text-[24px] leading-snug md:text-[31px]">
            {room.currentStatusSentence}
          </p>
        )}
      </section>

      {room.viewer.delta?.hasChanges && (
        <DeltaRibbon delta={room.viewer.delta} slug={room.slug} />
      )}

      {/* --- three essential facts --------------------------------------------- */}
      {room.essentialFacts.length > 0 && (
        <section
          className="grid gap-6 border-b border-[var(--border)] py-7 md:grid-cols-3 md:gap-0"
          data-testid="room-essential-facts"
        >
          {room.essentialFacts.map((fact, i) => (
            <div
              key={fact.ordinal}
              className={
                i > 0
                  ? "md:border-l md:border-[var(--border)] md:pl-6"
                  : "md:pr-6"
              }
              data-testid="essential-fact"
            >
              <p className="display text-[26px] text-[var(--accent)]">{i + 1}</p>
              <p className="mt-2 text-[17px] leading-snug">{fact.text}</p>
              {fact.claimStatus && (
                <p className="mt-3">
                  {/* The mark renders from the CLAIM, so a correction reaches this line
                      without anyone editing the room. */}
                  <EvidenceMark status={fact.claimStatus} size="inline" />
                  {fact.claimSlug && (
                    <Link
                      to={`/claims/${fact.claimSlug}`}
                      className="ml-3 text-[12px] underline"
                    >
                      Evidence
                    </Link>
                  )}
                </p>
              )}
            </div>
          ))}
        </section>
      )}

      {/* --- open question + watch next ---------------------------------------- */}
      <section className="grid gap-8 border-b border-[var(--border)] py-8 md:grid-cols-[1.35fr_1fr]">
        <div>
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            Still unresolved
          </p>
          <p className="display mt-3 text-[21px] leading-snug md:text-[25px]">
            {room.topUnresolvedQuestion}
          </p>
        </div>
        <div className="bg-[var(--fg)] p-6 text-[var(--bg)]" data-testid="room-watch-next">
          <p className="text-[11px] uppercase tracking-[0.2em] opacity-70">Watch next</p>
          <p className="display mt-3 text-[21px] leading-snug md:text-[25px]">
            {room.watchNext}
          </p>
        </div>
      </section>

      {latest && <RoomLatestSection latest={latest} />}
      {timeline.length > 0 && <RoomTimeline events={timeline} />}
      {actors && <RoomActorMap slug={room.slug} initial={actors} />}
      <RoomMoneyTrail slug={room.slug} />
      <RoomInteractions slug={room.slug} />
      <RoomClaimLedger slug={room.slug} />
      <RoomSources room={room} />
    </article>
  );
}

/** A deliberately thin board header: title, and the way back. Nothing else earns the space. */
function BoardHeader({
  room,
  onExit,
}: {
  room: ThemeRoomDetail;
  onExit: () => void;
}) {
  return (
    <header className="rooms-square flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2 pt-6">
      <div>
        <p className="text-[11px] font-bold uppercase tracking-[0.24em] text-[var(--accent)]">
          Situation board
        </p>
        <h1 className="mt-1 text-[26px] leading-tight md:text-[34px]">{room.title}</h1>
      </div>
      <button
        type="button"
        onClick={onExit}
        className="min-h-[44px] text-[13px] underline"
        data-testid="room-view-toggle"
      >
        Back to reading view
      </button>
    </header>
  );
}
