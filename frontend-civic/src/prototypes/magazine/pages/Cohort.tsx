import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Users, Trophy, Flame, Bot, Star } from "lucide-react";
import { getMyCohort, type Cohort } from "@/api/cohort";
import { useAuth } from "@/auth/AuthContext";
import { SignInPrompt } from "../components/SignInPrompt";

function CohortHeader() {
  return (
    <header>
      <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
        <Users size={14} /> This week's cohort
      </p>
      <h1 className="display mt-1 text-4xl">Your 50</h1>
    </header>
  );
}

export default function CohortPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const [cohort, setCohort] = useState<Cohort | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    // Don't create or fetch a cohort for signed-out visitors.
    if (!isAuthenticated) {
      setLoaded(true);
      return;
    }
    setLoaded(false);
    void getMyCohort()
      .then(setCohort)
      .finally(() => setLoaded(true));
  }, [isAuthenticated]);

  if (isLoading) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="cohort-loading">Loading…</p>;
  }
  if (!isAuthenticated) {
    return (
      <section data-testid="cohort-page" className="max-w-3xl">
        <CohortHeader />
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
          Your weekly cohort is a fixed group of up to 50 people you work the bills with.
        </p>
        <div className="mt-6">
          <SignInPrompt
            title="Sign in to see your cohort"
            message="Create a free account to join a weekly cohort and climb its leaderboard."
          />
        </div>
      </section>
    );
  }

  if (!loaded) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="cohort-loading">Finding your cohort…</p>;
  }
  if (!cohort) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="cohort-error">Your cohort is unavailable right now.</p>;
  }

  const fillPct = Math.min(100, Math.round((cohort.memberCount / cohort.targetSize) * 100));

  return (
    <section data-testid="cohort-page" className="max-w-3xl">
      <header>
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
          <Users size={14} /> This week's cohort
        </p>
        <h1 className="display mt-1 text-4xl">Your 50</h1>
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
          Every week you work the bills alongside a fixed group of up to {cohort.targetSize} people —
          {cohort.leagueName ? (
            <> starting with your league <strong>{cohort.leagueName}</strong> and topped up with others.</>
          ) : (
            <> a mix seeded for you. Join or start a league to bring your friends into next week's cohort.</>
          )}{" "}
          You're <strong>#{cohort.yourRank}</strong> so far.
        </p>
      </header>

      {/* Fill meter */}
      <div className="mt-6 rounded-2xl border border-[var(--line)] p-5">
        <div className="flex items-center justify-between text-sm">
          <span className="font-semibold">{cohort.memberCount} of {cohort.targetSize} in your cohort</span>
          <span className="text-[var(--muted)]" data-testid="cohort-week">week of {cohort.weekKey}</span>
        </div>
        <div className="mt-2 h-2 w-full overflow-hidden rounded bg-[var(--bg-elev)]">
          <div className="h-2 rounded bg-[var(--accent)]" style={{ width: `${fillPct}%` }} />
        </div>
        <div className="mt-3 flex flex-wrap gap-4 text-xs uppercase tracking-wider text-[var(--muted)]">
          <span className="flex items-center gap-1"><Star size={12} className="text-[var(--accent)]" /> {cohort.friendsCount} from your league</span>
          <span className="flex items-center gap-1"><Trophy size={12} /> you: {cohort.yourWeeklyPoints} pts · rank #{cohort.yourRank}</span>
        </div>
      </div>

      {/* Leaderboard */}
      <h2 className="mt-8 flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
        <Trophy size={15} /> Weekly leaderboard
      </h2>
      <ul className="mt-3 grid gap-1.5" data-testid="cohort-leaderboard">
        {cohort.leaderboard.map((s) => (
          <li
            key={s.userId}
            data-testid={s.isMe ? "cohort-row-me" : undefined}
            className={`flex items-center justify-between gap-3 rounded-xl border px-4 py-2.5 text-sm ${
              s.isMe ? "border-[var(--accent)] bg-[var(--accent)]/5" : "border-[var(--line)]"
            }`}
          >
            <div className="flex min-w-0 items-center gap-3">
              <span className="w-6 shrink-0 text-right font-semibold text-[var(--muted)]">{s.rank}</span>
              <span className="truncate font-medium">
                {s.isMe ? "You" : s.displayName}
                {s.isAgent && <Bot size={12} className="ml-1 inline text-[var(--muted)]" />}
                {s.isFriend && !s.isMe && (
                  <Star size={11} className="ml-1 inline text-[var(--accent)]" />
                )}
              </span>
            </div>
            <div className="flex shrink-0 items-center gap-4 text-xs text-[var(--muted)]">
              {s.activeDays > 0 && (
                <span className="flex items-center gap-1"><Flame size={12} /> {s.activeDays}d</span>
              )}
              <span className="font-semibold text-[var(--fg)]">{s.weeklyPoints} pts</span>
            </div>
          </li>
        ))}
      </ul>

      <p className="mt-6 text-xs text-[var(--muted)]">
        Points come from this week's coalition work — taking positions, co-signing, and bridging.
        Head to <Link to="/coalition" className="text-[var(--accent)] underline">Coalitions</Link> to climb the board.
      </p>
    </section>
  );
}
