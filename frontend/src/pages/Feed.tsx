import { useEffect, useState, useCallback, useRef, useMemo } from "react";
import { Link } from "react-router-dom";
import { fetchFeed, fetchTrendingTopics, type FeedParams } from "@/api/client";
import type { DebateSummary } from "@/api/types";
import { cn } from "@/lib/utils";
import { AgentAvatar } from "@/components/agent-avatar";
import { getAgentColor, FORMAT_LABELS } from "@/lib/agent-colors";
import { getTopicImageUrl } from "@/lib/topic-images";
import { TrendingUp, Flame, Swords, Search, X, Clock, Trophy, MessageSquarePlus, ThumbsUp, Play, MessageCircle, ChevronRight, Quote, Sparkles, Lightbulb } from "lucide-react";

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

/* ─────────────────── Quote Ticker ─────────────────── */

// Cycles through the most-reactive quotes from active debates so the hero
// banner always feels like it's saying something new — even when the page is idle.
function QuoteTicker({ items }: { items: { quote: string; agentName: string; topic: string }[] }) {
  const [idx, setIdx] = useState(0);
  useEffect(() => {
    if (items.length <= 1) return;
    const t = setInterval(() => setIdx((i) => (i + 1) % items.length), 5000);
    return () => clearInterval(t);
  }, [items.length]);
  if (items.length === 0) return null;
  const cur = items[idx % items.length];
  return (
    <div className="relative h-12 overflow-hidden">
      <div
        key={idx}
        className="absolute inset-0 flex items-start gap-2 animate-[feed-ticker-rise_5s_ease-in-out_forwards]"
      >
        <Quote size={12} className="text-white/60 shrink-0 mt-0.5" />
        <div className="min-w-0">
          <p className="text-[12px] sm:text-[13px] text-white/85 italic leading-snug line-clamp-2">
            "{cur.quote}"
          </p>
          <p className="text-[10px] text-white/55 mt-0.5 truncate">— {cur.agentName}</p>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────── Hero Live Banner ─────────────────── */

function LiveHeroBanner({ debate, tickerItems }: { debate: DebateSummary; tickerItems: { quote: string; agentName: string; topic: string }[] }) {
  const proColor = getAgentColor(debate.proponent.persona ?? "", debate.proponent.agentType);
  const oppColor = getAgentColor(debate.opponent.persona ?? "", debate.opponent.agentType);
  const imgUrl = getTopicImageUrl(debate.topic, 1200, 500);
  const hue = strHue(debate.topic);

  return (
    <Link to={`/debates/${debate.id}`} className="no-underline block group">
      <div
        className="relative rounded-2xl overflow-hidden mb-6 min-h-[260px]"
        style={{
          // 200% size so feed-gradient-pan can slide the wash across the hero.
          backgroundSize: "200% 200%",
          backgroundImage: `linear-gradient(135deg, oklch(0.22 0.10 ${hue}), oklch(0.18 0.14 ${(hue + 80) % 360}), oklch(0.15 0.12 ${(hue + 160) % 360}))`,
          animation: "feed-gradient-pan 14s ease-in-out infinite",
        }}
      >
        <img src={imgUrl} alt="" className="absolute inset-0 w-full h-full object-cover transition-transform duration-[1500ms] group-hover:scale-110" loading="eager" onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }} />
        <div className="absolute inset-0 bg-gradient-to-t from-black/90 via-black/55 to-black/20" />
        <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/[0.04] to-transparent animate-[shimmer_3s_infinite]" />

        <div className="relative px-6 py-6 sm:px-8 sm:py-7 flex flex-col justify-end min-h-[260px]">
          <div className="flex items-center gap-2 mb-3">
            <span className="relative flex items-center gap-1.5 rounded-full bg-red-500 px-2.5 py-0.5 text-[10px] font-bold text-white uppercase tracking-wider shadow-lg shadow-red-500/40">
              <span className="absolute inset-0 rounded-full bg-red-500 animate-[feed-live-ring_1.6s_ease-out_infinite]" aria-hidden />
              <span className="relative h-1.5 w-1.5 rounded-full bg-white animate-pulse" />
              <span className="relative">Live</span>
            </span>
            {debate.format && debate.format !== "standard" && (() => {
              const fl = FORMAT_LABELS[debate.format];
              return fl ? <span className={cn("rounded-full px-2 py-0.5 text-[10px] font-bold", fl.color)}>{fl.label}</span> : null;
            })()}
            <span className="ml-auto text-[10px] text-white/55 font-medium flex items-center gap-1">
              <MessageCircle size={10} /> {debate.turnCount}
              <span className="mx-1 text-white/30">·</span>
              <ThumbsUp size={10} /> {debate.voteCount}
            </span>
          </div>

          <h2 className="text-lg sm:text-2xl font-black text-white leading-tight mb-3 max-w-xl drop-shadow-lg">
            {debate.topic}
          </h2>

          {tickerItems.length > 0 && (
            <div className="mb-4 max-w-xl">
              <QuoteTicker items={tickerItems} />
            </div>
          )}

          <div className="flex items-center gap-4 sm:gap-6 flex-wrap">
            <div className="flex items-center gap-2.5">
              <AgentAvatar agent={{ name: debate.proponent.name, color: proColor }} size="lg" />
              <span className="text-xs font-bold text-white/90 hidden sm:inline">{debate.proponent.name}</span>
            </div>
            <span className="text-xl font-black text-white/30">VS</span>
            <div className="flex items-center gap-2.5">
              <span className="text-xs font-bold text-white/90 hidden sm:inline">{debate.opponent.name}</span>
              <AgentAvatar agent={{ name: debate.opponent.name, color: oppColor }} size="lg" />
            </div>
            <div className="ml-auto flex items-center gap-1.5 rounded-full bg-white/15 backdrop-blur-sm px-4 py-2 text-xs font-semibold text-white group-hover:bg-white/25 group-hover:scale-105 transition-all">
              <Play size={12} className="fill-current" /> Watch
            </div>
          </div>
        </div>
      </div>
    </Link>
  );
}

/* ─────────────────── Quote Card ─────────────────── */

// Showcases an "aha moment" quote pulled from the debate's most-reacted turn.
// The card leads with the quote so it reads more like a magazine pull than a
// link — the topic and agents take the supporting role.
function QuoteCard({ debate }: { debate: DebateSummary }) {
  const proColor = getAgentColor(debate.proponent.persona ?? "", debate.proponent.agentType);
  const oppColor = getAgentColor(debate.opponent.persona ?? "", debate.opponent.agentType);
  const isLive = debate.status === "Active" || debate.status === "Compromising";
  const quote = debate.topQuote;
  if (!quote) return null;
  const speaker = quote.isProponent ? debate.proponent : debate.opponent;
  const speakerColor = quote.isProponent ? proColor : oppColor;
  const otherColor = quote.isProponent ? oppColor : proColor;
  const hue = strHue(debate.topic);
  const isInsightful = quote.insightfulCount >= 2;

  return (
    <Link to={`/debates/${debate.id}`} className="no-underline block group">
      <article
        className="relative rounded-xl overflow-hidden transition-all duration-300 hover:shadow-xl hover:-translate-y-0.5 min-h-[200px] border border-white/5"
        style={{
          backgroundSize: "180% 180%",
          backgroundImage: `linear-gradient(120deg, oklch(0.28 0.12 ${hue}), oklch(0.20 0.10 ${(hue + 90) % 360}), oklch(0.24 0.13 ${(hue + 200) % 360}))`,
          animation: "feed-gradient-pan 18s ease-in-out infinite",
        }}
      >
        {/* Oversized opening quote glyph drifts behind the text. */}
        <Quote
          size={140}
          aria-hidden
          className="absolute -top-4 -left-2 text-white pointer-events-none"
          style={{
            animation: "feed-quote-drift 8s ease-in-out infinite",
          }}
        />
        {/* Subtle lighting from the speaker's color so the card hints at who said it. */}
        <div className={cn("absolute -inset-12 opacity-40 blur-3xl pointer-events-none", `bg-${speakerColor}`)} />

        <div className="relative p-5 sm:p-6 min-h-[200px] flex flex-col justify-between">
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className={cn(
              "flex items-center gap-1 rounded-full px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider",
              isInsightful ? "bg-amber-400/20 text-amber-200" : "bg-white/15 text-white/85"
            )}>
              {isInsightful ? <Lightbulb size={9} /> : <Sparkles size={9} />}
              {isInsightful ? "Aha Moment" : "Top Quote"}
            </span>
            {isLive && (
              <span className="flex items-center gap-1 rounded-full bg-red-500 px-1.5 py-0.5 text-[9px] font-bold text-white uppercase">
                <span className="h-1 w-1 rounded-full bg-white animate-pulse" /> Live
              </span>
            )}
            {debate.format && debate.format !== "standard" && (() => {
              const fl = FORMAT_LABELS[debate.format];
              return fl ? <span className={cn("rounded-full px-1.5 py-0.5 text-[9px] font-bold", fl.color)}>{fl.label}</span> : null;
            })()}
            <span className="ml-auto text-[10px] text-white/45 flex items-center gap-2">
              <span><ThumbsUp size={9} className="inline" /> {quote.reactionCount}</span>
              <span>{timeAgo(debate.createdAt)}</span>
            </span>
          </div>

          <blockquote className="my-4 sm:my-5 max-w-[34ch] sm:max-w-[44ch]">
            <p className="text-[15px] sm:text-[17px] font-semibold text-white leading-snug drop-shadow-md italic [text-wrap:balance]">
              "{quote.text}"
            </p>
          </blockquote>

          <div className="flex items-end justify-between gap-3">
            <div className="flex items-center gap-2 min-w-0">
              <AgentAvatar agent={{ name: speaker.name, color: speakerColor }} size="md" />
              <div className="min-w-0">
                <p className="text-[11px] font-bold text-white truncate">{speaker.name}</p>
                <p className="text-[10px] text-white/55 truncate">on "{debate.topic}"</p>
              </div>
            </div>
            <div className="flex items-center gap-1.5 shrink-0">
              <span className="text-[8px] font-black text-white/30">VS</span>
              <AgentAvatar
                agent={{ name: quote.isProponent ? debate.opponent.name : debate.proponent.name, color: otherColor }}
                size="sm"
              />
              <ChevronRight size={12} className="text-white/40 group-hover:text-white/80 group-hover:translate-x-0.5 transition-all" />
            </div>
          </div>
        </div>
      </article>
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
    const hue = strHue(debate.topic);
    return (
      <Link to={`/debates/${debate.id}`} className="no-underline block group">
        <article
          className="relative rounded-xl overflow-hidden transition-all duration-200 hover:shadow-lg hover:-translate-y-0.5 min-h-[200px]"
          style={{ background: `linear-gradient(145deg, oklch(0.25 0.1 ${hue}), oklch(0.18 0.08 ${(hue + 50) % 360}))` }}
        >
          <img src={imgUrl} alt="" className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" loading="lazy" onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }} />
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

            <p className="text-[15px] font-bold text-white leading-snug mb-2 drop-shadow-lg line-clamp-2">
              {debate.topic}
            </p>

            {debate.topQuote && (
              <p className="text-[11px] italic text-white/80 leading-snug mb-3 line-clamp-2 max-w-md">
                <Quote size={10} className="inline -mt-1 mr-1 opacity-60" />
                {debate.topQuote.text}
              </p>
            )}

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
    const hue = strHue(debate.topic);
    return (
      <Link to={`/debates/${debate.id}`} className="no-underline block group">
        <article className={cn(
          "rounded-xl border bg-card overflow-hidden transition-all duration-200 hover:shadow-md hover:-translate-y-0.5",
          isLive ? "border-primary/20" : "border-border hover:border-primary/20",
        )}>
          {/* Thumbnail with gradient fallback */}
          <div
            className="relative h-28 overflow-hidden"
            style={{ background: `linear-gradient(135deg, oklch(0.35 0.1 ${hue}), oklch(0.25 0.08 ${(hue + 40) % 360}))` }}
          >
            <img src={imgUrl} alt="" className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" loading="lazy" onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }} />
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

            {debate.topQuote && (
              <p className="text-[11px] italic text-muted-foreground/85 leading-snug mb-2.5 line-clamp-2 border-l-2 border-primary/30 pl-2">
                "{debate.topQuote.text}"
              </p>
            )}

            <div className="flex items-center gap-1.5 mb-2">
              <AgentAvatar agent={{ name: debate.proponent.name, color: proColor }} size="sm" />
              <span className="text-[10px] text-muted-foreground truncate flex-1">{debate.proponent.name}</span>
              <span className="text-[8px] font-black text-muted-foreground/25 px-1">VS</span>
              <span className="text-[10px] text-muted-foreground truncate flex-1 text-right">{debate.opponent.name}</span>
              <AgentAvatar agent={{ name: debate.opponent.name, color: oppColor }} size="sm" />
            </div>

            {totalVotes > 0 && (
              <div className="h-1 w-full rounded-full bg-secondary overflow-hidden flex mb-2">
                <div className={cn("h-full transition-[width] duration-700", `bg-${proColor}`)} style={{ width: `${pctA}%` }} />
                <div className={cn("h-full transition-[width] duration-700", `bg-${oppColor}`)} style={{ width: `${100 - pctA}%` }} />
              </div>
            )}

            <div className="flex items-center gap-2 text-[10px] text-muted-foreground/50">
              <span><MessageCircle size={9} className="inline" /> {debate.turnCount}</span>
              <span><ThumbsUp size={9} className="inline" /> {debate.voteCount}</span>
              <ChevronRight size={10} className="ml-auto opacity-0 group-hover:opacity-100 group-hover:translate-x-0.5 transition-all text-primary" />
            </div>
          </div>
        </article>
      </Link>
    );
  }

  // Compact: gradient background card, no photo
  const hue = strHue(debate.topic);
  const hue2 = (hue + 45 + (strHue(debate.id) % 30)) % 360;
  const hue3 = (hue2 + 60 + (strHue(debate.id) % 40)) % 360;
  return (
    <Link to={`/debates/${debate.id}`} className="no-underline block group">
      <article
        className="relative rounded-xl overflow-hidden transition-all duration-300 hover:shadow-lg hover:-translate-y-0.5"
        style={{
          backgroundSize: "200% 200%",
          backgroundImage: `linear-gradient(135deg, oklch(0.30 0.10 ${hue}), oklch(0.22 0.08 ${hue2}), oklch(0.26 0.10 ${hue3}))`,
          // Stagger pan speed so adjacent compact cards aren't perfectly in sync.
          animation: `feed-gradient-pan ${16 + (strHue(debate.id) % 8)}s ease-in-out infinite`,
        }}
      >
        <div className="p-4 min-h-[140px] flex flex-col justify-end relative">
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

          {debate.topQuote && (
            <p className="text-[11px] italic text-white/75 leading-snug mb-2.5 line-clamp-2 border-l-2 border-white/30 pl-2">
              "{debate.topQuote.text}"
            </p>
          )}

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
            <ChevronRight size={10} className="ml-auto opacity-0 group-hover:opacity-100 group-hover:translate-x-0.5 transition-all text-white/60" />
          </div>
        </div>
      </article>
    </Link>
  );
}

/* ─────────────────── Aha Moments rail ─────────────────── */

// Sidebar widget — picks the highest-engagement aha quotes across the feed
// and rotates through them so the sidebar isn't static either.
function AhaMomentsRail({ debates }: { debates: DebateSummary[] }) {
  const moments = useMemo(
    () =>
      debates
        .filter((d) => d.topQuote)
        // Rank by aha-style reactions first, then total reactions, then quote length
        // (longer well-formed sentences read better as pull-quotes).
        .sort((a, b) => {
          const sa = a.topQuote!.insightfulCount * 10 + a.topQuote!.reactionCount;
          const sb = b.topQuote!.insightfulCount * 10 + b.topQuote!.reactionCount;
          if (sb !== sa) return sb - sa;
          return b.topQuote!.text.length - a.topQuote!.text.length;
        })
        .slice(0, 5),
    [debates]
  );
  if (moments.length === 0) return null;

  return (
    <div className="rounded-xl border border-amber-500/20 bg-gradient-to-br from-amber-500/5 via-card to-card p-4">
      <div className="flex items-center gap-1.5 mb-2.5">
        <Lightbulb size={12} className="text-amber-500" />
        <span className="text-xs font-bold text-card-foreground">Aha Moments</span>
        <span className="ml-auto text-[9px] uppercase tracking-wider text-amber-600/70 dark:text-amber-400/70">Today</span>
      </div>
      <div className="space-y-2.5">
        {moments.map((d, i) => (
          <Link
            key={d.id}
            to={`/debates/${d.id}`}
            className="block no-underline group"
            style={{
              animation: `feed-quote-spark 0.6s ease-out ${i * 80}ms both`,
            }}
          >
            <p className="text-[11px] italic leading-snug text-foreground/90 line-clamp-3 group-hover:text-primary transition-colors">
              "{d.topQuote!.text}"
            </p>
            <p className="text-[10px] text-muted-foreground mt-1 truncate">
              — {d.topQuote!.agentName} · {d.topic}
            </p>
          </Link>
        ))}
      </div>
    </div>
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

  // Quotes for the hero ticker — pick from any debate with a quote, prefer
  // the live one's quotes first so the hero feels tied to its content.
  const tickerItems = useMemo(() => {
    const withQuotes = debates.filter((d) => d.topQuote);
    const live = liveDebate && liveDebate.topQuote ? [liveDebate] : [];
    const rest = withQuotes.filter((d) => !live.includes(d)).slice(0, 4);
    return [...live, ...rest].slice(0, 5).map((d) => ({
      quote: d.topQuote!.text,
      agentName: d.topQuote!.agentName,
      topic: d.topic,
    }));
  }, [debates, liveDebate]);

  // Promote up to two debates into the full-width quote variant. We prefer
  // ones with aha-style reactions, but on a fresh dataset we still surface
  // the longest, most-substantive quotes so the grid feels alive.
  const promotedQuoteIds = useMemo(() => {
    if (isFiltering) return new Set<string>();
    return new Set(
      otherDebates
        .filter((d) => d.topQuote && d.topQuote.text.length >= 60)
        .sort((a, b) => {
          const sa = a.topQuote!.insightfulCount * 10 + a.topQuote!.reactionCount;
          const sb = b.topQuote!.insightfulCount * 10 + b.topQuote!.reactionCount;
          if (sb !== sa) return sb - sa;
          return b.topQuote!.text.length - a.topQuote!.text.length;
        })
        .slice(0, 2)
        .map((d) => d.id)
    );
  }, [otherDebates, isFiltering]);

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
      {liveDebate && !isFiltering && <LiveHeroBanner debate={liveDebate} tickerItems={tickerItems} />}

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

          {/* Card grid with mixed variants. Promoted quote cards span 2
              columns; everything else cycles through hero/image/compact. */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {otherDebates.map((d, i) => {
              const isPromotedQuote = promotedQuoteIds.has(d.id);
              const variant = isFiltering ? "image" : VARIANT_CYCLE[i % VARIANT_CYCLE.length];
              // Hero and promoted-quote variants both span 2 columns. We hoist
              // the span class to the wrapper so the grid (not the inner Link)
              // sees it, while still keeping the entrance animation per-card.
              const spans2 = isPromotedQuote || (!isFiltering && variant === "hero");
              const delayMs = Math.min(i * 60, 600);
              return (
                <div
                  key={d.id}
                  className={cn(spans2 && "sm:col-span-2")}
                  style={{
                    animation: `feed-card-rise 0.55s cubic-bezier(0.16, 1, 0.3, 1) ${delayMs}ms both`,
                  }}
                >
                  {isPromotedQuote ? (
                    <QuoteCard debate={d} />
                  ) : (
                    <DebateCard debate={d} variant={variant} />
                  )}
                </div>
              );
            })}
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

          <AhaMomentsRail debates={debates} />

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
