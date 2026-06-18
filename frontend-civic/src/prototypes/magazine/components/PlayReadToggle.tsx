import { Link } from "react-router-dom";

// The Play / Read segmented control. Play = the gamified dashboard ("/" →
// PlayerHome for active players); Read = the magazine ("/magazine"). It's pure
// navigation, not stateful — and it renders on BOTH views so a player can always
// get back to the dashboard from the magazine and vice-versa.
export const MAGAZINE_HOME = "/magazine";
export const PLAYER_HOME = "/";

export function PlayReadToggle({ active }: { active: "play" | "read" }) {
  const playActive = active === "play";
  return (
    <div
      className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] bg-[var(--accent)]/5 p-1"
      role="tablist"
      aria-label="Play or read"
    >
      <Link
        to={PLAYER_HOME}
        role="tab"
        aria-selected={playActive}
        aria-current={playActive ? "page" : undefined}
        className={`rounded-full px-4 py-1.5 text-[11px] font-bold uppercase tracking-[0.12em] transition ${
          playActive
            ? "bg-[var(--accent)] text-white"
            : "text-[var(--muted)] hover:text-[var(--accent)]"
        }`}
        data-testid="toggle-play"
      >
        Play
      </Link>
      <Link
        to={MAGAZINE_HOME}
        role="tab"
        aria-selected={!playActive}
        aria-current={!playActive ? "page" : undefined}
        className={`rounded-full px-4 py-1.5 text-[11px] font-bold uppercase tracking-[0.12em] transition ${
          !playActive
            ? "bg-[var(--accent)] text-white"
            : "text-[var(--muted)] hover:text-[var(--accent)]"
        }`}
        data-testid="toggle-read"
      >
        Read
      </Link>
    </div>
  );
}
