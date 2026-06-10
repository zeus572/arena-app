import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Users, Trophy, Plus, ArrowRight } from "lucide-react";
import { listMyLeagues, createLeague, type LeagueSummary } from "@/api/leagues";
import { useAuth } from "@/auth/AuthContext";
import { SignInPrompt } from "../components/SignInPrompt";

export default function Leagues() {
  const { user, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const [leagues, setLeagues] = useState<LeagueSummary[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated) {
      setLoaded(true);
      return;
    }
    void listMyLeagues()
      .then(setLeagues)
      .finally(() => setLoaded(true));
  }, [isAuthenticated]);

  async function onCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim() || creating) return;
    setCreating(true);
    setError(null);
    try {
      const league = await createLeague({
        name: name.trim(),
        displayName: user?.displayName ?? undefined,
        email: user?.email ?? undefined,
        avatarUrl: user?.avatarUrl ?? undefined,
      });
      navigate(`/leagues/${league.id}`);
    } catch (err) {
      setError(
        (err as { response?: { data?: { error?: string } } }).response?.data?.error ??
          "Couldn't create the league. Try again.",
      );
      setCreating(false);
    }
  }

  return (
    <section data-testid="leagues-page">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
          Leagues
        </p>
        <h1 className="display mt-1 text-4xl">Compete with friends</h1>
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
          Start a league, invite your friends, and put your campaigns head-to-head. Climb the season
          standings through your candidate's support — and win shared rounds where everyone responds
          to the same news and votes on each other's takes.
        </p>
      </header>

      {!isLoading && !isAuthenticated ? (
        <div className="mt-8">
          <SignInPrompt
            title="Sign in to start a league"
            message="Leagues need an account so your friends can find you on the leaderboard. Create one — it's free."
          />
        </div>
      ) : !loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Loading…
        </p>
      ) : (
        <div className="mt-8 grid gap-8 md:grid-cols-[1fr_320px]">
          {/* My leagues */}
          <div>
            {leagues.length === 0 ? (
              <div
                className="border border-dashed border-[var(--border)] bg-[var(--bg-elev)] p-10 text-center"
                data-testid="empty-state"
              >
                <Users className="mx-auto h-8 w-8 text-[var(--muted)]" />
                <p className="mt-3 text-lg font-semibold text-[var(--fg)]">No leagues yet</p>
                <p className="mt-1 text-sm text-[var(--fg-soft)]">
                  Create your first league and invite a few friends to get going.
                </p>
              </div>
            ) : (
              <ul className="grid gap-4" data-testid="league-list">
                {leagues.map((l) => (
                  <li key={l.id}>
                    <Link
                      to={`/leagues/${l.id}`}
                      data-testid="league-card"
                      className="block border border-[var(--border)] bg-[var(--bg-elev)] p-5 transition hover:border-[var(--accent)]"
                    >
                      <div className="flex items-center justify-between gap-3">
                        <p className="display text-2xl">{l.name}</p>
                        {l.myRole === "Owner" && (
                          <span className="rounded-full bg-[var(--accent)] px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider text-white">
                            Owner
                          </span>
                        )}
                      </div>
                      {l.description && (
                        <p className="mt-1 text-sm text-[var(--fg-soft)]">{l.description}</p>
                      )}
                      <div className="mt-4 flex items-center gap-4 text-sm text-[var(--fg-soft)]">
                        <span className="inline-flex items-center gap-1">
                          <Users className="h-4 w-4" /> {l.memberCount}/{l.maxMembers}
                        </span>
                        {l.activeRoundId ? (
                          <span className="inline-flex items-center gap-1 font-semibold text-[var(--accent)]">
                            <Trophy className="h-4 w-4" /> Round in progress
                          </span>
                        ) : (
                          <span>Season {l.seasonNumber}</span>
                        )}
                        {!l.hasLinkedCampaign && (
                          <span className="text-[var(--muted)]">· Link a campaign</span>
                        )}
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Create */}
          <aside>
            <form
              onSubmit={onCreate}
              className="border border-[var(--border)] bg-[var(--bg-elev)] p-5"
              data-testid="create-league-form"
            >
              <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">
                Start a league
              </h2>
              <label className="mt-3 block text-sm font-semibold text-[var(--fg-soft)]" htmlFor="league-name">
                League name
              </label>
              <input
                id="league-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={120}
                placeholder="The Group Chat Caucus"
                data-testid="league-name-input"
                className="mt-1 w-full border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-[var(--fg)] outline-none focus:border-[var(--accent)]"
              />
              {error && (
                <p className="mt-2 text-sm text-red-600" data-testid="create-error">
                  {error}
                </p>
              )}
              <button
                type="submit"
                disabled={!name.trim() || creating}
                data-testid="create-league-submit"
                className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white disabled:opacity-50"
              >
                <Plus className="h-4 w-4" />
                {creating ? "Creating…" : "Create league"}
              </button>
              <p className="mt-3 text-xs text-[var(--muted)]">
                You'll get a shareable invite link to send your friends right after.
              </p>
            </form>

            <JoinByCode />
          </aside>
        </div>
      )}
    </section>
  );
}

// ---------------------------------------------------------------- Join by code

function JoinByCode() {
  const navigate = useNavigate();
  const [code, setCode] = useState("");

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    // Accept either a raw code or a full join URL the friend pasted in.
    const raw = code.trim();
    if (!raw) return;
    const match = raw.match(/join\/([A-Za-z0-9]+)/);
    const value = (match ? match[1] : raw).toUpperCase();
    navigate(`/leagues/join/${value}`);
  }

  return (
    <form
      onSubmit={onSubmit}
      className="mt-4 border border-[var(--border)] bg-[var(--bg-elev)] p-5"
      data-testid="join-by-code-form"
    >
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">
        Have an invite code?
      </h2>
      <p className="mt-1 text-xs text-[var(--muted)]">
        Paste the code (or link) a friend sent you to join their league.
      </p>
      <div className="mt-3 flex items-center gap-2">
        <input
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder="ABCD2345"
          data-testid="join-code-input"
          className="w-full border border-[var(--border)] bg-[var(--bg)] px-3 py-2 font-mono uppercase tracking-wider text-[var(--fg)] outline-none placeholder:font-sans placeholder:normal-case placeholder:tracking-normal focus:border-[var(--accent)]"
        />
        <button
          type="submit"
          disabled={!code.trim()}
          data-testid="join-code-submit"
          className="inline-flex shrink-0 items-center gap-1 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
        >
          Go <ArrowRight className="h-4 w-4" />
        </button>
      </div>
    </form>
  );
}
