import { Link } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { PlayReadToggle, PLAYER_HOME } from "../components/PlayReadToggle";
import MagazineHome from "./Home";

// The explicit "Read" route (/magazine). It's the magazine Home plus a way back
// to the player dashboard — without this, a player who switched to Read had no
// obvious return path. Reached from PlayerHome's Play/Read toggle and its
// "Open the magazine" links.
export default function MagazineReadView() {
  return (
    <div data-testid="magazine-read-view">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-3">
        <Link
          to={PLAYER_HOME}
          className="inline-flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] transition hover:text-[var(--accent)]"
          data-testid="back-to-dashboard"
        >
          <ArrowLeft className="h-3.5 w-3.5" /> Back to your dashboard
        </Link>
        <PlayReadToggle active="read" />
      </div>
      <MagazineHome />
    </div>
  );
}
