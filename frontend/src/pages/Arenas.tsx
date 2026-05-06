import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchArenas } from "@/api/client";
import type { ArenaSummary } from "@/api/types";
import { cn } from "@/lib/utils";
import { Activity, Sparkles, ChevronRight } from "lucide-react";

const TONE_LABEL: Record<string, string> = {
  serious: "Serious",
  comedic: "Comedic",
  adversarial: "Adversarial",
  educational: "Educational",
};

export default function Arenas() {
  const [arenas, setArenas] = useState<ArenaSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchArenas()
      .then(setArenas)
      .catch(() => setError("Failed to load arenas."));
  }, []);

  return (
    <main className="mx-auto max-w-5xl px-4 py-8">
      <header className="mb-8">
        <div className="flex items-center gap-2 mb-2">
          <Sparkles size={16} className="text-primary" />
          <span className="text-[11px] font-bold uppercase tracking-wider text-primary">
            Topic Ecosystem
          </span>
        </div>
        <h1 className="text-2xl font-bold text-foreground tracking-tight mb-2">
          Arenas
        </h1>
        <p className="text-sm text-muted-foreground max-w-2xl leading-relaxed">
          Each arena is a community with its own topic focus, tone, debate format, and house rules.
          Pick one to dive in — or fork a debate from any feed to explore an alternate framing.
        </p>
      </header>

      {error && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-xs text-destructive">
          {error}
        </div>
      )}

      {!arenas && !error && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-44 rounded-xl border border-border bg-card animate-pulse" />
          ))}
        </div>
      )}

      {arenas && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {arenas.map((a) => (
            <ArenaCard key={a.id} arena={a} />
          ))}
        </div>
      )}
    </main>
  );
}

function ArenaCard({ arena }: { arena: ArenaSummary }) {
  return (
    <Link
      to={`/arenas/${arena.slug}`}
      className={cn(
        "group relative overflow-hidden rounded-xl border border-border bg-card p-4",
        "transition-all hover:border-primary/40 hover:shadow-md no-underline",
      )}
      style={{
        background: `linear-gradient(135deg, ${arena.accentColor}10 0%, transparent 60%)`,
      }}
    >
      <div
        className="absolute inset-x-0 top-0 h-0.5"
        style={{ backgroundColor: arena.accentColor }}
      />

      <div className="flex items-start justify-between gap-2 mb-3">
        <div className="flex items-center gap-2">
          <span className="text-2xl leading-none">{arena.iconEmoji}</span>
          <div>
            <h3 className="font-semibold text-sm text-card-foreground tracking-tight">
              {arena.name}
            </h3>
            <p className="text-[10px] text-muted-foreground">
              {arena.topic} · {TONE_LABEL[arena.tone] ?? arena.tone}
            </p>
          </div>
        </div>
        <ChevronRight
          size={14}
          className="text-muted-foreground/60 group-hover:text-primary transition-colors mt-1"
        />
      </div>

      <p className="text-xs text-muted-foreground leading-relaxed line-clamp-3 min-h-[3rem]">
        {arena.description}
      </p>

      <div className="mt-3 pt-3 border-t border-border/50 flex items-center justify-between">
        <span className="text-[10px] text-muted-foreground">
          {arena.debateCount} debate{arena.debateCount === 1 ? "" : "s"}
        </span>
        {arena.activeDebateCount > 0 && (
          <span className="flex items-center gap-1 text-[10px] font-semibold text-emerald-600">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
            <Activity size={10} />
            {arena.activeDebateCount} live
          </span>
        )}
      </div>
    </Link>
  );
}
