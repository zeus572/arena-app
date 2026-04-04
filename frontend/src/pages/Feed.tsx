import { useEffect, useState, useCallback, useRef } from "react";
import { Link } from "react-router-dom";
import { fetchFeed, fetchTrendingTopics, type FeedParams } from "@/api/client";
import type { DebateSummary } from "@/api/types";
import { cn } from "@/lib/utils";
import { AgentAvatar } from "@/components/agent-avatar";
import { getAgentColor, FORMAT_LABELS } from "@/lib/agent-colors";
import { getTopicImageUrl } from "@/lib/topic-images";
import { TrendingUp, Flame, Swords, Search, X, Clock, Trophy, MessageSquarePlus, ThumbsUp, Play, MessageCircle, ChevronRight } from "lucide-react";

function strHue(s: string): number {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = s.charCodeAt(i) + ((h << 5) - h);
  return Math.abs(h) % 360;
}

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h`;
  const days = Math.floor(hours / 24);
  return `${days}d`;
}

/* ─────────────────── Hero Live Banner ─────────────────── */

function LiveHeroBanner({ debate }: { debate: DebateSummary }) {
  const proColor = getAgentColor(debate.proponent.persona ?? "", debate.proponent.agentType);
  const oppColor = getAgentColor(debate.opponent.persona ?? "", debate.opponent.agentType);
  const imgUrl = getTopicImageUrl(debate.topic, 1200, 500);

  return (
    <Link to={`/debates/${debate.id}`} className="no-underline block group">
      <div className="relative rounded-2xl overflow-hidden mb-6 min-h-[220px]">
        {/* Background image */}
        <img src={imgUrl} alt="" className="absolute inset-0 w-full h-full object-cover transition-transform duration-700 group-hover:scale-105" loading="eager" />
        <div className="absolute inset-0 bg-gradient-to-t from-black/90 via-black/50 to-black/30" />
        <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/[0.03] to-transparent animate-[shimmer_3s_infinite]" />

        <div className="relative px-6 py-6 sm:px-8 sm:py-7 flex flex-col justify-end min-h-[220px]">
          <div className="flex items-center gap-2 mb-3">
            <span className="flex items-center gap-1.5 rounded-full bg-red-500 px-2.5 py-0.5 text-[10px] font-bold text-white uppercase tracking-wider shadow-lg shadow-red-500/30">
              <span className="h-1.5 w-1.5 rounded-full bg-white animate-pulse" />
              Live
            </span>
            {debate.format && debate.format !== "standard" && (() => {
              const fl = FORMAT_LABELS[debate.format];
              return fl ? <span className={cn("rounded-full px-2 py-0.5 text-[10px] font-bold", fl.color)}>{fl.label}</span> : null;
            })()}
          </div>

          <h2 className="text-lg sm:text-2xl font-black text-white leading-tight mb-5 max-w-xl drop-shadow-lg">
            {debate.topic}
          </h2>

          <div className="flex items-center gap-6">
            <div className="flex items-center gap-2.5">
              <AgentAvatar agent={{ name: debate.proponent.name, color: proColor }} size="lg" />
              <span className="text-xs font-bold text-white/90 hidden sm:inline">{debate.proponent.name}</span>
            </div>
            <span className="text-xl font-black text-white/30">VS</span>
            <div className="flex items-center gap-2.5">
              <span className="text-xs font-bold text-white/90 hidden sm:inline">{debate.opponent.name}</span>
              <AgentAvatar agent={{ name: debate.opponent.name, color: oppColor }} size="lg" />
            </div>
            <div className="ml-auto flex items-center gap-1.5 rounded-full bg-white/15 backdrop-blur-sm px-4 py-2 text-xs font-semibold text-white group-hover:bg-white/25 transition-colors">
              <Play size={12} className="fill-current" /> Watch
            </div>
          </div>
        </div>
      </div>
    </Link>
  );
}

/* ─────────────────── Debate Card ─────────────────── */

function DebateCard({ debate, variant }: { debate: DebateSummary; variant: "hero" | "image" | "compact" }) {
  const proColor = getAgentColor(debate.proponent.persona ?? "", debate.proponent.agentType);
  const oppColor = getAgentColor(debate.opponent.persona ?? "", debate.opponent.agentType);
  const isLive = debate.status === "Active" || debate.status === "Compromising";
  const totalVotes = debate.proponentVotes + debate.opponentVotes;
  const pctA = totalVotes > 0 ? (debate.proponentVotes / totalVotes) * 100 : 50;

  // Hero: large card with full background image, spans 2 columns
  if (variant === "hero") {
    const imgUrl = getTopicImageUrl(debate.topic, 800, 400);
    return (
      <Link to={`/debates/${debate.id}`} className="no-underline block group sm:col-span-2">
        <article className="relative rounded-xl overflow-hidden transition-all duration-200 hover:shadow-lg hover:-translate-y-0.5 min-h-[200px]">
          <img src={imgUrl} alt="" className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" loading="lazy" />
          <div className="absolute inset-0 bg-gradient-to-t from-black/90 via-black/40 to-black/10" />

          <div className="relative p-5 min-h-[200px] flex flex-col justify-end">
            <div className="flex items-center gap-1.5 mb-2">
              {isLive && (
                <span className="flex items-center gap-1 rounded-full bg-red-500 px-2 py-0.5 text-[9px] font-bold text-white uppercase">
                  <span className="h-1 w-1 rounded-full bg-white animate-pulse" /> Live
                </span>
              )}
              {debate.format && debate.format !== "standard" && (() => {
                const fl = FORMAT_LABELS[debate.format];
                return fl ? <span className={cn("rounded-full px-1.5 py-0.5 text-[9px] font-bold", fl.color)}>{fl.label}</span> : null;
              })()}
              {debate.label && <span className="rounded-full px-1.5 py-0.5 text-[9px] font-semibold bg-white/15 text-white/80">{debate.label}</span>}
              <span className="ml-auto text-[10px] text-white/40">{timeAgo(debate.createdAt)}</span>
            </div>

            <p className="text-[15px] font-bold text-white leading-snug mb-3 drop-shadow-lg line-clamp-2">
              {debate.topic}
            </p>

            <div className="flex items-center gap-3">
              <div className="flex items-center gap-1.5">
                <AgentAvatar agent={{ name: debate.proponent.name, color: proColor }} size="sm" />
                <span className="text-[10px] text-white/70 font-medium truncate max-w-[80px]">{debate.proponent.name}</span>
              </div>
              <span className="text-[8px] font-black text-white/25">VS</span>
              <div className="flex items-center gap-1.5">
                <span className="text-[10px] text-white/70 font-medium truncate max-w-[80px]">{debate.opponent.name}</span>
                <AgentAvatar agent={{ name: debate.opponent.name, color: oppColor }} size="sm" />
              </div>
              <div className="ml-auto flex items-center gap-2 text-[10px] text-white/40">
                <span><MessageCircle size={9} className="inline" /> {debate.turnCount}</span>
                <span><ThumbsUp size={9} className="inline" /> {debate.voteCount}</span>
              </div>
            </div>
          </div>
        </article>
      </Link>
    );
  }

  // Image: card with small image thumbnail on top
  if (variant === "image") {
    const imgUrl = getTopicImageUrl(debate.topic, 400, 200);
    return (
      <Link to={`/debates/${debate.id}`} className="no-underline block group">
        <article className={cn(
          "rounded-xl border bg-card overflow-hidden transition-all duration-200 hover:shadow-md hover:-translate-y-0.5",
          isLive ? "border-primary/20" : "border-border hover:border-primary/20",
        )}>
          {/* Thumbnail */}
          <div className="relative h-28 overflow-hidden">
            <img src={imgUrl} alt="" className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" loading="lazy" />
            <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
            <div className="absolute bottom-2 left-3 flex items-center gap-1.5">
              {isLive && (
                <span className="flex items-center gap-1 rounded-full bg-red-500 px-1.5 py-0.5 text-[9px] font-bold text-white uppercase">
                  <span className="h-1 w-1 rounded-full bg-white animate-pulse" /> Live
                </span>
              )}
              {debate.format && debate.format !== "standard" && (() => {
                const fl = FORMAT_LABELS[debate.format];
                return fl ? <span className={cn("rounded-full px-1.5 py-0.5 text-[9px] font-bold", fl.color)}>{fl.label}</span> : null;
              })()}
            </div>
            <span className="absolute bottom-2 right-3 text-[10px] text-white/50">{timeAgo(debate.createdAt)}</span>
          </div>

          <div className="p-3.5">
            <p className="text-[13px] font-semibold text-card-foreground leading-snug mb-2.5 group-hover:text-primary transition-colors line-clamp-2">
              {debate.topic}
            </p>

            <div className="flex items-center gap-1.5 mb-2">
              <AgentAvatar agent={{ name: debate.proponent.name, color: proColor }} size="sm" />
              <span className="text-[10px] text-muted-foreground truncate flex-1">{debate.proponent.name}</span>
              <span className="text-[8px] font-black text-muted-foreground/25 px-1">VS</span>
              <span className="text-[10px] text-muted-foreground truncate flex-1 text-right">{debate.opponent.name}</span>
              <AgentAvatar agent={{ name: debate.opponent.name, color: oppColor }} size="sm" />
            </div>

            {totalVotes > 0 && (
              <div className="h-1 w-full rounded-full bg-secondary overflow-hidden flex mb-2">
                <div className={cn("h-full", `bg-${proColor}`)} style={{ width: `${pctA}%` }} />
                <div className={cn("h-full", `bg-${oppColor}`)} style={{ width: `${100 - pctA}%` }} />
              </div>
            )}

            <div className="flex items-center gap-2 text-[10px] text-muted-foreground/50">
              <span><MessageCircle size={9} className="inline" /> {debate.turnCount}</span>
              <span><ThumbsUp size={9} className="inline" /> {debate.voteCount}</span>
              <ChevronRight size={10} className="ml-auto opacity-0 group-hover:opacity-100 transition-all text-primary" />
            </div>
          </div>
        </article>
      </Link>
    );
  }

  // Compact: gradient background card, no photo
  const hue = strHue(debate.topic);
  const hue2 = (hue + 45 + (strHue(debate.id) % 30)) % 360;
  return (
    <Link to={`/debates/${debate.id}`} className="no-underline block group">
      <article
        className="rounded-xl overflow-hidden transition-all duration-200 hover:shadow-md hover:-translate-y-0.5"
        style={{ background: `linear-gradient(135deg, oklch(0.30 0.10 ${hue}), oklch(0.22 0.08 ${hue2}))` }}
      >
        <div className="p-4 min-h-[140px] flex flex-col justify-end">
          <div className="flex items-center gap-1.5 mb-2">
            {isLive && (
              <span className="flex items-center gap-1 rounded-full bg-red-500 px-1.5 py-0.5 text-[9px] font-bold text-white uppercase">
                <span className="h-1 w-1 rounded-full bg-white animate-pulse" /> Live
              </span>
            )}
            {debate.format && debate.format !== "standard" && (() => {
              const fl = FORMAT_LABELS[debate.format];
              return fl ? <span className={cn("rounded-full px-1.5 py-0.5 text-[9px] font-bold", fl.color)}>{fl.label}</span> : null;
            })()}
            {debate.label && (
              <span className="rounded-full px-1.5 py-0.5 text-[9px] font-semibold bg-white/10 text-white/80">{debate.label}</span>
            )}
            <span className="ml-auto text-[10px] text-white/40">{timeAgo(debate.createdAt)}</span>
          </div>

          <p className="text-[13px] font-semibold text-white leading-snug mb-2.5 drop-shadow line-clamp-2">
            {debate.topic}
          </p>

          <div className="flex items-center gap-1.5 mb-2">
            <AgentAvatar agent={{ name: debate.proponent.name, color: proColor }} size="sm" />
            <span className="text-[10px] text-white/60 truncate flex-1">{debate.proponent.name}</span>
            <span className="text-[8px] font-black text-white/20 px-1">VS</span>
            <span className="text-[10px] text-white/60 truncate flex-1 text-right">{debate.opponent.name}</span>
            <AgentAvatar agent={{ name: debate.opponent.name, color: oppColor }} size="sm" />
          </div>

          <div className="flex items-center gap-2 text-[10px] text-white/35">
            <span><MessageCircle size={9} className="inline" /> {debate.turnCount}</span>
            <span><ThumbsUp size={9} className="inline" /> {debate.voteCount}</span>
            <ChevronRight size={10} className="ml-auto opacity-0 group-hover:opacity-100 transition-all text-white/60" />
          </div>
        </div>
      </article>
    </Link>
  );
}

/* ─────────────────── Main Feed ─────────────────── */

// Cycle card variants for visual variety: hero (2-col), image (with photo), compact (text-only)
const VARIANT_CYCLE: ("hero" | "image" | "compact")[] = [
  "hero", "image", "compact", "image", "compact", "image",
  "compact", "hero", "image", "compact", "image", "compact",
  "image", "compact", "image", "compact", "hero", "image",
];

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

  useEffect(() => {
    setLoading(true);
    const params: FeedParams = { sort: sortBy };
    if (searchQuery) params.q = searchQuery;
    if (activeTag) params.tag = activeTag;
    loadFeed(params);
  }, [sortBy, activeTag, loadFeed, searchQuery]);

  useEffect(() => { fetchTrendingTopics().then(setTrending); }, []);

  const handleSearchChange = (value: string) => {
    setSearchQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {}, 300);
  };

  const clearFilters = () => { setSearchQuery(""); setActiveTag(null); setSortBy("hot"); };
  const hasFilters = searchQuery || activeTag || sortBy !== "hot";
  const liveDebate = debates.find((d) => d.status === "Active" || d.status === "Compromising");
  const otherDebates = liveDebate ? debates.filter((d) => d.id !== liveDebate.id) : debates;
  const isFiltering = !!(searchQuery || activeTag);

  if (loading && debates.length === 0) {
    return (
      <main className="mx-auto max-w-6xl px-4 py-8">
        <div className="rounded-2xl bg-secondary/50 h-[220px] animate-pulse mb-6" />
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className={cn("rounded-xl bg-secondary/50 animate-pulse", i === 1 ? "h-[200px] sm:col-span-2" : "h-44")} />
          ))}
        </div>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-6xl px-4 py-6">
      {liveDebate && !isFiltering && <LiveHeroBanner debate={liveDebate} />}

      <div className="flex flex-col lg:flex-row gap-6">
        <section className="flex-1 min-w-0">
          {/* Search + Sort */}
          <div className="flex items-center gap-2 mb-5">
            <div className="relative flex-1">
              <Search size={13} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground/40" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => handleSearchChange(e.target.value)}
                placeholder="Search..."
                className="w-full rounded-full border border-border bg-card pl-8 pr-8 py-2 text-xs text-foreground placeholder:text-muted-foreground/40 outline-none focus:border-primary focus:ring-1 focus:ring-primary/20"
              />
              {searchQuery && (
                <button onClick={() => setSearchQuery("")} className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground">
                  <X size={12} />
                </button>
              )}
            </div>
            <div className="flex items-center rounded-full border border-border bg-card p-0.5">
              {([
                { key: "hot" as const, icon: Flame },
                { key: "new" as const, icon: Clock },
                { key: "top" as const, icon: Trophy },
              ]).map(({ key, icon: Icon }) => (
                <button
                  key={key}
                  onClick={() => setSortBy(key)}
                  className={cn(
                    "rounded-full p-1.5 transition-all",
                    sortBy === key ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground/40 hover:text-foreground"
                  )}
                  title={key}
                >
                  <Icon size={12} />
                </button>
              ))}
            </div>
          </div>

          {(activeTag || hasFilters) && (
            <div className="flex items-center gap-2 mb-4 text-[11px]">
              {activeTag && (
                <span className="flex items-center gap-1 rounded-full bg-primary/10 text-primary px-2.5 py-0.5 font-medium">
                  {activeTag} <button onClick={() => setActiveTag(null)}><X size={9} /></button>
                </span>
              )}
              <span className="text-muted-foreground">{totalCount} results</span>
              <button onClick={clearFilters} className="text-primary hover:underline">Clear</button>
            </div>
          )}

          {/* Card grid with mixed variants */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {otherDebates.map((d, i) => (
              <DebateCard
                key={d.id}
                debate={d}
                variant={isFiltering ? "image" : VARIANT_CYCLE[i % VARIANT_CYCLE.length]}
              />
            ))}
          </div>

          {debates.length === 0 && (
            <div className="text-center py-16">
              <Swords size={32} className="mx-auto text-muted-foreground/20 mb-3" />
              <p className="text-xs text-muted-foreground">
                {isFiltering ? "No matches." : "No debates yet."}
              </p>
            </div>
          )}
        </section>

        {/* Sidebar */}
        <aside className="w-full lg:w-64 shrink-0 flex flex-col gap-3">
          <Link to="/start" className="no-underline">
            <div className="rounded-xl border border-primary/20 bg-gradient-to-br from-primary/5 to-card p-4 hover:border-primary/40 transition-colors">
              <div className="flex items-center gap-2 mb-1">
                <Swords size={13} className="text-primary" />
                <span className="text-xs font-bold text-card-foreground">Start a Debate</span>
              </div>
              <p className="text-[10px] text-muted-foreground">Pick a topic and two agents.</p>
            </div>
          </Link>

          <Link to="/topics" className="no-underline">
            <div className="rounded-xl border border-border bg-card p-4 hover:border-primary/20 transition-colors">
              <div className="flex items-center gap-2 mb-1">
                <MessageSquarePlus size={13} className="text-amber-500" />
                <span className="text-xs font-bold text-card-foreground">Vote on Topics</span>
              </div>
              <p className="text-[10px] text-muted-foreground">Shape what gets debated next.</p>
            </div>
          </Link>

          {trending.length > 0 && (
            <div className="rounded-xl border border-border bg-card p-4">
              <div className="flex items-center gap-1.5 mb-2">
                <TrendingUp size={12} className="text-primary" />
                <span className="text-xs font-bold text-card-foreground">Trending</span>
              </div>
              <div className="flex flex-wrap gap-1">
                {trending.map((t) => (
                  <button
                    key={t.topic}
                    onClick={() => setActiveTag(t.topic)}
                    className={cn(
                      "rounded-full px-2.5 py-1 text-[10px] font-medium transition-all",
                      activeTag === t.topic
                        ? "bg-primary text-primary-foreground"
                        : "bg-secondary text-muted-foreground hover:text-foreground"
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
