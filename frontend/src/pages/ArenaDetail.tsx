import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { fetchArenaFeed } from "@/api/client";
import type { ArenaFeedItem, ArenaFeedResponse } from "@/api/types";
import { Button } from "@/components/ui/button";
import { AgentAvatar } from "@/components/agent-avatar";
import { getAgentColor, FORMAT_LABELS } from "@/lib/agent-colors";
import { cn } from "@/lib/utils";
import { ChevronLeft, GitFork, MessageCircle, ThumbsUp, Plus, Flame, Clock, Trophy, Zap } from "lucide-react";

const SORTS: { key: "hot" | "new" | "top" | "controversial"; label: string; icon: typeof Flame }[] = [
  { key: "hot", label: "Hot", icon: Flame },
  { key: "new", label: "New", icon: Clock },
  { key: "top", label: "Top", icon: Trophy },
  { key: "controversial", label: "Controversial", icon: Zap },
];

export default function ArenaDetail() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<ArenaFeedResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sort, setSort] = useState<"hot" | "new" | "top" | "controversial">("hot");

  useEffect(() => {
    if (!slug) return;
    setData(null);
    fetchArenaFeed(slug, { sort })
      .then(setData)
      .catch(() => setError("Couldn't load this arena."));
  }, [slug, sort]);

  if (!slug) return null;

  if (error) {
    return (
      <main className="mx-auto max-w-5xl px-4 py-8">
        <p className="text-sm text-destructive">{error}</p>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-5xl px-4 py-8">
      <Link to="/arenas">
        <Button variant="ghost" size="sm" className="mb-4 gap-1.5 text-xs text-muted-foreground -ml-2">
          <ChevronLeft size={14} /> All Arenas
        </Button>
      </Link>

      {!data ? (
        <div className="space-y-3">
          <div className="h-32 rounded-xl border border-border bg-card animate-pulse" />
          <div className="h-24 rounded-xl border border-border bg-card animate-pulse" />
          <div className="h-24 rounded-xl border border-border bg-card animate-pulse" />
        </div>
      ) : (
        <>
          <ArenaHeader arena={data.arena} onStart={() => navigate(`/start?arena=${data.arena.slug}`)} />

          <div className="mt-6 mb-3 flex items-center gap-1 overflow-x-auto pb-1">
            {SORTS.map(({ key, label, icon: Icon }) => (
              <button
                key={key}
                onClick={() => setSort(key)}
                className={cn(
                  "flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs transition-colors whitespace-nowrap",
                  sort === key
                    ? "border-primary bg-primary/10 text-primary font-semibold"
                    : "border-border bg-card text-muted-foreground hover:text-foreground hover:border-primary/40",
                )}
              >
                <Icon size={12} />
                {label}
              </button>
            ))}
            <span className="text-[10px] text-muted-foreground ml-auto">
              {data.totalCount} debate{data.totalCount === 1 ? "" : "s"}
            </span>
          </div>

          {data.items.length === 0 ? (
            <div className="rounded-xl border border-dashed border-border bg-card/50 p-8 text-center">
              <p className="text-sm text-muted-foreground mb-3">
                No debates here yet. Be the first to start one.
              </p>
              <Link to={`/start?arena=${data.arena.slug}`}>
                <Button size="sm" className="gap-1.5">
                  <Plus size={14} /> Start the first debate
                </Button>
              </Link>
            </div>
          ) : (
            <div className="space-y-2">
              {data.items.map((d) => (
                <ArenaDebateRow key={d.id} debate={d} />
              ))}
            </div>
          )}
        </>
      )}
    </main>
  );
}

function ArenaHeader({
  arena,
  onStart,
}: {
  arena: ArenaFeedResponse["arena"];
  onStart: () => void;
}) {
  return (
    <div
      className="relative overflow-hidden rounded-xl border border-border p-5"
      style={{
        background: `linear-gradient(135deg, ${arena.accentColor}1a 0%, transparent 70%)`,
      }}
    >
      <div
        className="absolute inset-x-0 top-0 h-1"
        style={{ backgroundColor: arena.accentColor }}
      />
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
        <div className="flex items-start gap-3">
          <span className="text-4xl leading-none">{arena.iconEmoji}</span>
          <div>
            <p className="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
              {arena.topic} · {arena.tone}
            </p>
            <h1 className="text-xl font-bold text-foreground tracking-tight">
              {arena.name}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground leading-relaxed max-w-xl">
              {arena.description}
            </p>
          </div>
        </div>
        <Button onClick={onStart} size="sm" className="gap-1.5 shrink-0">
          <Plus size={14} /> Start Debate
        </Button>
      </div>

      {arena.rules && (
        <details className="mt-4 group">
          <summary className="cursor-pointer text-[11px] font-semibold uppercase tracking-wider text-muted-foreground hover:text-foreground select-none">
            House Rules
          </summary>
          <pre className="mt-2 whitespace-pre-wrap text-xs text-foreground/80 leading-relaxed font-sans">
            {arena.rules}
          </pre>
          <p className="mt-2 text-[10px] text-muted-foreground italic">
            These rules are passed into the AI debaters' system prompt — they shape every turn.
          </p>
        </details>
      )}

      <div className="mt-4 flex items-center gap-1.5">
        <span className="text-[10px] text-muted-foreground">Default format:</span>
        <span
          className={cn(
            "text-[10px] font-bold rounded-full px-2 py-0.5",
            FORMAT_LABELS[arena.defaultFormat]?.color ?? "bg-secondary text-secondary-foreground",
          )}
        >
          {FORMAT_LABELS[arena.defaultFormat]?.label ?? arena.defaultFormat}
        </span>
      </div>
    </div>
  );
}

function ArenaDebateRow({ debate }: { debate: ArenaFeedItem }) {
  const isLive = debate.status === "Active" || debate.status === "Compromising";
  const fl = FORMAT_LABELS[debate.format];
  return (
    <Link
      to={`/debates/${debate.id}`}
      className="group block rounded-xl border border-border bg-card p-4 transition-colors hover:border-primary/40 no-underline"
    >
      <div className="flex items-start gap-4">
        <div className="flex flex-col items-center gap-1 shrink-0 pt-1">
          <ThumbsUp size={14} className="text-muted-foreground" />
          <span className="text-xs font-bold text-foreground">{debate.voteCount}</span>
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1.5 flex-wrap">
            {fl && (
              <span className={cn("text-[10px] font-bold rounded-full px-2 py-0.5", fl.color)}>
                {fl.label}
              </span>
            )}
            {isLive && (
              <span className="flex items-center gap-1 text-[10px] font-semibold text-emerald-600">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
                Live
              </span>
            )}
            {debate.isForked && (
              <span className="flex items-center gap-1 text-[10px] text-muted-foreground">
                <GitFork size={10} /> forked
              </span>
            )}
            {debate.forkCount > 0 && (
              <span className="flex items-center gap-1 text-[10px] text-purple-600 font-medium">
                <GitFork size={10} /> {debate.forkCount} fork{debate.forkCount === 1 ? "" : "s"}
              </span>
            )}
          </div>

          <h3 className="font-semibold text-sm text-card-foreground group-hover:text-primary transition-colors text-balance leading-snug">
            {debate.topic}
          </h3>

          <div className="mt-2 flex items-center gap-3 text-[11px] text-muted-foreground">
            <div className="flex items-center gap-1.5">
              <AgentAvatar
                agent={{ name: debate.proponent.name, color: getAgentColor(debate.proponent.persona ?? "") }}
                size="sm"
              />
              <span className="truncate max-w-[10ch]">{debate.proponent.name}</span>
            </div>
            <span>vs.</span>
            <div className="flex items-center gap-1.5">
              <AgentAvatar
                agent={{ name: debate.opponent.name, color: getAgentColor(debate.opponent.persona ?? "") }}
                size="sm"
              />
              <span className="truncate max-w-[10ch]">{debate.opponent.name}</span>
            </div>
            <span className="ml-auto flex items-center gap-1">
              <MessageCircle size={11} />
              {debate.turnCount}
            </span>
          </div>
        </div>
      </div>
    </Link>
  );
}
