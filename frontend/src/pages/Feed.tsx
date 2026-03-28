import { useEffect, useState, useCallback, useRef } from "react";
import { Link } from "react-router-dom";
import { fetchFeed, fetchTrendingTopics, type FeedParams } from "@/api/client";
import type { DebateSummary } from "@/api/types";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { AgentAvatar } from "@/components/agent-avatar";
import { getAgentColor } from "@/lib/agent-colors";
import { TrendingUp, Flame, PlusCircle, Swords, Search, X, Clock, Trophy, MessageSquarePlus, Lightbulb, ThumbsUp, ThumbsDown } from "lucide-react";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

export default function Feed() {
  const [debates, setDebates] = useState<DebateSummary[]>([]);
  const [trending, setTrending] = useState<{ topic: string; score: number }[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [searchQuery, setSearchQuery] = useState("");
  const [activeTag, setActiveTag] = useState<string | null>(null);
  const [sortBy, setSortBy] = useState<"hot" | "new" | "top">("hot");
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const loadFeed = useCallback(async (params: FeedParams = {}) => {
    const data = await fetchFeed({ sort: sortBy, ...params });
    setDebates(data.items);
    setTotalCount(data.totalCount);
    setLoading(false);
  }, [sortBy]);

  // Initial load
  useEffect(() => {
    setLoading(true);
    const params: FeedParams = { sort: sortBy };
    if (searchQuery) params.q = searchQuery;
    if (activeTag) params.tag = activeTag;
    loadFeed(params);
  }, [sortBy, activeTag, loadFeed, searchQuery]);

  // Load trending on mount
  useEffect(() => {
    fetchTrendingTopics().then(setTrending);
  }, []);

  const handleSearchChange = (value: string) => {
    setSearchQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      // searchQuery state change triggers useEffect above
    }, 300);
  };

  const clearFilters = () => {
    setSearchQuery("");
    setActiveTag(null);
    setSortBy("hot");
  };

  const hasFilters = searchQuery || activeTag || sortBy !== "hot";

  const liveDebate = debates.find((d) => d.status === "Active" || d.status === "Compromising");

  if (loading && debates.length === 0) {
    return (
      <main className="mx-auto max-w-6xl px-4 py-8">
        <div className="flex flex-col gap-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-5 h-32 animate-pulse" />
          ))}
        </div>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-6xl px-4 py-8">
      <div className="flex flex-col lg:flex-row gap-8">
        <section className="flex-1 min-w-0">
          {liveDebate && !searchQuery && !activeTag && (
            <div className="mb-5 rounded-xl border border-primary/30 bg-primary/5 p-4 flex items-center justify-between gap-3 flex-wrap">
              <div className="flex items-center gap-2">
                <span className="flex h-2 w-2 rounded-full bg-red-500 animate-pulse" />
                <span className="text-xs font-semibold text-foreground uppercase tracking-wide">
                  Live Debate
                </span>
                <span className="text-xs text-muted-foreground truncate max-w-xs">
                  {liveDebate.topic}
                </span>
              </div>
              <Link to={`/debates/${liveDebate.id}`}>
                <Button size="sm" className="gap-1.5 text-xs h-7">
                  <Swords size={12} />
                  Watch Now
                </Button>
              </Link>
            </div>
          )}

          {/* Search bar */}
          <div className="relative mb-4">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => handleSearchChange(e.target.value)}
              placeholder="Search debates..."
              className="w-full rounded-lg border border-border bg-card pl-9 pr-9 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery("")}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              >
                <X size={14} />
              </button>
            )}
          </div>

          {/* Sort + filter bar */}
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <TrendingUp size={16} className="text-primary" />
              <h1 className="text-sm font-semibold text-foreground">
                {activeTag ? `Tag: ${activeTag}` : "Latest from the Arena"}
              </h1>
              {activeTag && (
                <button
                  onClick={() => setActiveTag(null)}
                  className="ml-1 rounded-full bg-secondary p-0.5 hover:bg-destructive/20"
                >
                  <X size={10} />
                </button>
              )}
            </div>
            <div className="flex items-center gap-1">
              {([
                { key: "hot" as const, label: "Hot", icon: Flame },
                { key: "new" as const, label: "New", icon: Clock },
                { key: "top" as const, label: "Top", icon: Trophy },
              ]).map(({ key, label, icon: Icon }) => (
                <Button
                  key={key}
                  variant="ghost"
                  size="sm"
                  onClick={() => setSortBy(key)}
                  className={cn(
                    "h-7 text-xs gap-1",
                    sortBy === key ? "text-primary font-medium" : "text-muted-foreground"
                  )}
                >
                  <Icon size={12} /> {label}
                </Button>
              ))}
            </div>
          </div>

          {hasFilters && (
            <div className="flex items-center gap-2 mb-3 text-xs text-muted-foreground">
              <span>{totalCount} result{totalCount !== 1 ? "s" : ""}</span>
              <button onClick={clearFilters} className="text-primary hover:underline">
                Clear filters
              </button>
            </div>
          )}

          <div className="flex flex-col gap-4">
            {debates.map((d) => (
              <Link key={d.id} to={`/debates/${d.id}`} className="no-underline">
                <article className="rounded-xl border border-border bg-card p-5 flex flex-col gap-3 hover:border-primary/30 transition-colors">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="flex -space-x-2">
                        <AgentAvatar
                          agent={{ name: d.proponent.name, color: getAgentColor(d.proponent.persona ?? "") }}
                          size="md"
                        />
                        <AgentAvatar
                          agent={{ name: d.opponent.name, color: getAgentColor(d.opponent.persona ?? "") }}
                          size="md"
                        />
                      </div>
                      <div>
                        <p className="text-sm font-semibold text-card-foreground">{d.topic}</p>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          {d.proponent.name} vs {d.opponent.name}
                        </p>
                      </div>
                    </div>
                    {d.status === "Active" || d.status === "Compromising" ? (
                      <span className={cn(
                        "shrink-0 flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase",
                        d.status === "Compromising"
                          ? "bg-amber-500/10 text-amber-600"
                          : "bg-red-500/10 text-red-500"
                      )}>
                        <span className={cn(
                          "h-1.5 w-1.5 rounded-full animate-pulse",
                          d.status === "Compromising" ? "bg-amber-500" : "bg-red-500"
                        )} />
                        {d.status === "Compromising" ? "Compromise" : "Live"}
                      </span>
                    ) : (
                      <span className="shrink-0 text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                        {d.status}
                      </span>
                    )}
                  </div>

                  {/* Rivalry banner */}
                  {d.rivalry && d.rivalry.matchups >= 2 && (
                    <div className="flex items-center gap-2 rounded-lg bg-orange-500/5 border border-orange-500/20 px-3 py-1.5">
                      <Swords size={12} className="text-orange-500 shrink-0" />
                      <span className="text-[11px] text-orange-600 dark:text-orange-400 font-medium">
                        {d.rivalry.matchups === 2 ? "Rematch" : `${d.rivalry.matchups}th matchup`}
                        {" \u2014 "}
                        Series {d.rivalry.proponentWins}-{d.rivalry.opponentWins}
                        {d.rivalry.proponentWins === d.rivalry.opponentWins && " (tied)"}
                      </span>
                    </div>
                  )}

                  {/* Reaction distribution bar */}
                  {d.reactionCount > 0 && (
                    <div className="flex items-center gap-2">
                      <div className="flex-1 flex h-1.5 rounded-full overflow-hidden bg-secondary">
                        {(d.reactions?.like ?? 0) > 0 && (
                          <div
                            className="bg-primary h-full"
                            style={{ width: `${((d.reactions.like ?? 0) / d.reactionCount) * 100}%` }}
                          />
                        )}
                        {(d.reactions?.insightful ?? 0) > 0 && (
                          <div
                            className="bg-amber-500 h-full"
                            style={{ width: `${((d.reactions.insightful ?? 0) / d.reactionCount) * 100}%` }}
                          />
                        )}
                        {(d.reactions?.disagree ?? 0) > 0 && (
                          <div
                            className="bg-destructive h-full"
                            style={{ width: `${((d.reactions.disagree ?? 0) / d.reactionCount) * 100}%` }}
                          />
                        )}
                      </div>
                      <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground shrink-0">
                        {(d.reactions?.like ?? 0) > 0 && (
                          <span className="flex items-center gap-0.5"><ThumbsUp size={9} />{d.reactions.like}</span>
                        )}
                        {(d.reactions?.insightful ?? 0) > 0 && (
                          <span className="flex items-center gap-0.5 text-amber-500"><Lightbulb size={9} />{d.reactions.insightful}</span>
                        )}
                        {(d.reactions?.disagree ?? 0) > 0 && (
                          <span className="flex items-center gap-0.5 text-destructive"><ThumbsDown size={9} />{d.reactions.disagree}</span>
                        )}
                      </div>
                    </div>
                  )}

                  <div className="flex items-center gap-3 text-xs text-muted-foreground pt-2 border-t border-border">
                    <span>{d.turnCount} turns</span>
                    <span>{d.voteCount} votes</span>
                    <span>{timeAgo(d.createdAt)}</span>
                    {d.label && (
                      <span className={cn(
                        "rounded-full px-2 py-0.5 text-[10px] font-semibold",
                        d.label === "Controversial" && "bg-orange-500/10 text-orange-600",
                        d.label === "Insightful" && "bg-amber-500/10 text-amber-600",
                        d.label === "Heated" && "bg-red-500/10 text-red-500",
                      )}>
                        {d.label === "Controversial" && <>{"\u26A1"} </>}
                        {d.label === "Insightful" && <>{"\uD83E\uDDE0"} </>}
                        {d.label === "Heated" && <>{"\uD83D\uDD25"} </>}
                        {d.label}
                      </span>
                    )}
                    {d.totalScore !== undefined && d.totalScore > 0 && (
                      <span className="ml-auto rounded-full bg-primary/10 text-primary px-2 py-0.5 text-[10px] font-semibold">
                        {d.totalScore.toFixed(1)} pts
                      </span>
                    )}
                  </div>
                </article>
              </Link>
            ))}
            {debates.length === 0 && (
              <p className="text-sm text-muted-foreground text-center py-8">
                {searchQuery || activeTag ? "No debates match your search." : "No debates yet."}
              </p>
            )}
          </div>
        </section>

        <aside className="w-full lg:w-72 shrink-0 flex flex-col gap-4">
          <div className="rounded-xl border border-border bg-card p-5">
            <h2 className="text-sm font-semibold text-card-foreground mb-1">Start a Debate</h2>
            <p className="text-xs text-muted-foreground leading-relaxed mb-4">
              Pick a topic, choose two agents with opposing views, and let them argue it out.
            </p>
            <Link to="/start">
              <Button size="sm" className="w-full gap-2 text-xs">
                <PlusCircle size={13} />
                New Debate
              </Button>
            </Link>
          </div>

          <div className="rounded-xl border border-border bg-card p-5">
            <h2 className="text-sm font-semibold text-card-foreground mb-1">Shape the Debate</h2>
            <p className="text-xs text-muted-foreground leading-relaxed mb-4">
              Vote on which topics the AI agents should debate next.
            </p>
            <Link to="/topics">
              <Button variant="outline" size="sm" className="w-full gap-2 text-xs">
                <MessageSquarePlus size={13} />
                Vote for Topics
              </Button>
            </Link>
          </div>

          {trending.length > 0 && (
            <div className="rounded-xl border border-border bg-card p-5">
              <h2 className="text-sm font-semibold text-card-foreground mb-3">Trending Topics</h2>
              <div className="flex flex-wrap gap-2">
                {trending.map((t) => (
                  <button
                    key={t.topic}
                    onClick={() => setActiveTag(t.topic)}
                    className={cn(
                      "rounded-full px-3 py-1 text-xs font-medium transition-colors",
                      activeTag === t.topic
                        ? "bg-primary text-primary-foreground"
                        : "bg-secondary text-secondary-foreground hover:bg-primary hover:text-primary-foreground"
                    )}
                  >
                    {t.topic}
                  </button>
                ))}
              </div>
            </div>
          )}
        </aside>
      </div>
    </main>
  );
}
