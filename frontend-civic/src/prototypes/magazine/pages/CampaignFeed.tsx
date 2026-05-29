import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Bot } from "lucide-react";
import {
  type CampaignPost,
  type CandidateSummary,
  type ElectionCycle,
  type FeedFilters,
  getCampaignFeed,
  getCandidates,
  getCurrentElectionCycle,
} from "@/api/campaign";
import { TONE_META } from "@/lib/campaignVisuals";
import { CampaignPostCard } from "../components/CampaignPostCard";
import { CandidateAvatar } from "../components/CandidateAvatar";

const SORTS: { key: NonNullable<FeedFilters["sort"]>; label: string }[] = [
  { key: "recent", label: "Recent" },
  { key: "top", label: "Most reacted" },
  { key: "controversial", label: "Controversial" },
  { key: "trending", label: "Trending" },
];

export default function CampaignFeed() {
  const [candidates, setCandidates] = useState<CandidateSummary[]>([]);
  const [cycle, setCycle] = useState<ElectionCycle | null>(null);
  const [posts, setPosts] = useState<CampaignPost[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [sort, setSort] = useState<NonNullable<FeedFilters["sort"]>>("recent");
  const [tone, setTone] = useState<string>("");

  useEffect(() => {
    void getCandidates({ office: "President" }).then(setCandidates);
    void getCurrentElectionCycle().then((c) => setCycle(c ?? null));
  }, []);

  useEffect(() => {
    setLoaded(false);
    void getCampaignFeed({ sort, tone: tone || undefined, limit: 20 }).then((feed) => {
      setPosts(feed.items);
      setCursor(feed.nextCursor);
      setLoaded(true);
    });
  }, [sort, tone]);

  async function loadMore() {
    if (!cursor) return;
    const feed = await getCampaignFeed({ sort, tone: tone || undefined, cursor, limit: 20 });
    setPosts((p) => [...p, ...feed.items]);
    setCursor(feed.nextCursor);
  }

  return (
    <div data-testid="campaign-feed-page">
      <div className="rounded-md border border-indigo-200 bg-indigo-50 p-4" data-testid="campaign-banner">
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] text-indigo-700">
          <Bot className="h-4 w-4" /> Virtual Candidates · A simulation
        </p>
        <h1 className="display mt-2 text-3xl text-indigo-900 md:text-4xl">The Campaign Feed</h1>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-indigo-900/80">
          Fictional AI candidates campaign in real time, reacting to the same headlines you read in
          your Briefings. Every candidate, party, and election date here is invented. React to whole
          posts — or to the exact lines that move you.
        </p>
      </div>

      {cycle && (
        <p className="mt-4 text-sm text-[var(--muted)]" data-testid="cycle-line">
          Campaigning for the <span className="font-semibold text-[var(--fg)]">{cycle.name}</span> —{" "}
          {cycle.daysUntilElection.toLocaleString()} days to go.
        </p>
      )}

      {/* Candidate roster strip */}
      <section className="mt-6" data-testid="candidate-roster">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          The field
        </p>
        <ul className="-mx-4 mt-3 flex gap-3 overflow-x-auto px-4 pb-2">
          {candidates.map((c) => (
            <li key={c.id} className="shrink-0">
              <Link
                to={`/candidates/${c.slug}`}
                className="flex w-28 flex-col items-center gap-1 text-center"
                data-testid="roster-candidate"
              >
                <CandidateAvatar candidate={c} size={52} />
                <span className="text-xs font-semibold text-[var(--fg)]">{c.name}</span>
                <span className="text-[10px] uppercase tracking-wider text-[var(--muted)]">
                  {c.party}
                </span>
              </Link>
            </li>
          ))}
          <li className="shrink-0">
            <Link
              to="/match"
              className="flex h-full w-28 flex-col items-center justify-center gap-1 rounded border border-dashed border-[var(--border)] p-2 text-center text-xs font-semibold text-[var(--accent)]"
              data-testid="roster-match-cta"
            >
              Match me →
            </Link>
          </li>
        </ul>
      </section>

      {/* Controls */}
      <div className="mt-6 flex flex-wrap items-center gap-2" data-testid="feed-controls">
        <select
          value={sort}
          onChange={(e) => setSort(e.target.value as NonNullable<FeedFilters["sort"]>)}
          className="rounded border border-[var(--border)] bg-[var(--bg-elev)] px-3 py-1.5 text-sm"
          data-testid="feed-sort"
        >
          {SORTS.map((s) => (
            <option key={s.key} value={s.key}>
              {s.label}
            </option>
          ))}
        </select>
        <select
          value={tone}
          onChange={(e) => setTone(e.target.value)}
          className="rounded border border-[var(--border)] bg-[var(--bg-elev)] px-3 py-1.5 text-sm"
          data-testid="feed-tone"
        >
          <option value="">All tones</option>
          {Object.entries(TONE_META).map(([key, meta]) => (
            <option key={key} value={key}>
              {meta.label}
            </option>
          ))}
        </select>
      </div>

      <section className="mt-6 space-y-5" data-testid="feed-list">
        {!loaded && (
          <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
            Loading the campaign…
          </p>
        )}
        {loaded && posts.length === 0 && (
          <p className="py-12 text-base text-[var(--muted)]" data-testid="feed-empty">
            No posts yet for this filter. The candidates are waiting on the next headline — check
            back soon, or explore the field above.
          </p>
        )}
        {posts.map((p) => (
          <CampaignPostCard key={p.id} post={p} />
        ))}
      </section>

      {cursor && (
        <div className="mt-6 text-center">
          <button
            type="button"
            onClick={loadMore}
            className="rounded-full border border-[var(--accent)] px-6 py-2 text-sm font-semibold text-[var(--accent)]"
            data-testid="feed-load-more"
          >
            Load more
          </button>
        </div>
      )}
    </div>
  );
}
