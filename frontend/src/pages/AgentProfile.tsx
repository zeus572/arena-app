import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchAgentDetail } from "@/api/client";
import type { AgentDetail } from "@/api/types";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { getAgentColor, getAgentLabel } from "@/lib/agent-colors";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import {
  ChevronLeft,
  Swords,
  BookOpen,
  MessageSquare,
  ThumbsUp,
  Lightbulb,
  ThumbsDown,
  Tag,
} from "lucide-react";

const TRAIT_LABELS: { key: keyof AgentDetail["personality"]; label: string }[] = [
  { key: "aggressiveness", label: "Aggression" },
  { key: "eloquence", label: "Eloquence" },
  { key: "factReliance", label: "Fact Reliance" },
  { key: "empathy", label: "Empathy" },
  { key: "wit", label: "Wit" },
];

function RadarChart({ personality }: { personality: AgentDetail["personality"] }) {
  const size = 200;
  const center = size / 2;
  const radius = 80;
  const traits = TRAIT_LABELS.map((t) => ({
    ...t,
    value: personality[t.key],
  }));
  const n = traits.length;

  const getPoint = (index: number, value: number) => {
    const angle = (Math.PI * 2 * index) / n - Math.PI / 2;
    const r = (value / 10) * radius;
    return { x: center + r * Math.cos(angle), y: center + r * Math.sin(angle) };
  };

  // Grid rings
  const rings = [2, 4, 6, 8, 10];

  return (
    <svg viewBox={`0 0 ${size} ${size}`} className="w-full max-w-[220px] mx-auto">
      {/* Grid */}
      {rings.map((ring) => (
        <polygon
          key={ring}
          points={Array.from({ length: n }, (_, i) => {
            const p = getPoint(i, ring);
            return `${p.x},${p.y}`;
          }).join(" ")}
          fill="none"
          stroke="currentColor"
          strokeOpacity={0.1}
          strokeWidth={1}
        />
      ))}

      {/* Axis lines */}
      {traits.map((_, i) => {
        const p = getPoint(i, 10);
        return (
          <line
            key={i}
            x1={center}
            y1={center}
            x2={p.x}
            y2={p.y}
            stroke="currentColor"
            strokeOpacity={0.1}
            strokeWidth={1}
          />
        );
      })}

      {/* Data polygon */}
      <polygon
        points={traits
          .map((t, i) => {
            const p = getPoint(i, t.value);
            return `${p.x},${p.y}`;
          })
          .join(" ")}
        fill="hsl(var(--primary))"
        fillOpacity={0.15}
        stroke="hsl(var(--primary))"
        strokeWidth={2}
      />

      {/* Data points */}
      {traits.map((t, i) => {
        const p = getPoint(i, t.value);
        return <circle key={i} cx={p.x} cy={p.y} r={3} fill="hsl(var(--primary))" />;
      })}

      {/* Labels */}
      {traits.map((t, i) => {
        const p = getPoint(i, 12);
        return (
          <text
            key={i}
            x={p.x}
            y={p.y}
            textAnchor="middle"
            dominantBaseline="middle"
            className="fill-muted-foreground text-[9px]"
          >
            {t.label}
          </text>
        );
      })}
    </svg>
  );
}

export default function AgentProfile() {
  const { id } = useParams<{ id: string }>();
  const [agent, setAgent] = useState<AgentDetail | null>(null);

  useEffect(() => {
    if (id) fetchAgentDetail(id).then(setAgent);
  }, [id]);

  if (!agent) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8">
        <div className="h-64 rounded-xl bg-secondary/50 animate-pulse" />
      </main>
    );
  }

  const color = getAgentColor(agent.persona, agent.agentType);
  const label = getAgentLabel(agent.persona, agent.agentType);
  const totalDebates = agent.stats.totalDebates;
  const winRate = totalDebates > 0 ? ((agent.stats.wins / totalDebates) * 100).toFixed(0) : "0";

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <Link to="/agents">
        <Button variant="ghost" size="sm" className="mb-5 gap-1.5 text-xs text-muted-foreground -ml-2">
          <ChevronLeft size={14} /> All Agents
        </Button>
      </Link>

      {/* Header */}
      <div className="rounded-xl border border-border bg-card p-6 mb-6">
        <div className="flex items-start gap-4">
          <AgentAvatar agent={{ name: agent.name, color }} size="xl" />
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h1 className="text-xl font-bold text-card-foreground">{agent.name}</h1>
              <IdeologyBadge label={label} color={color} />
              {agent.era && (
                <span className="text-[10px] rounded-full bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400 px-2 py-0.5 font-medium">
                  {agent.era}
                </span>
              )}
            </div>
            {(agent.agentType === "celebrity" || agent.agentType === "historical") && (
              <p className="text-[10px] text-muted-foreground/70 italic mt-0.5">AI simulation based on public record. Not the real {agent.name}.</p>
            )}
            <p className="text-sm text-muted-foreground mt-1">{agent.description}</p>
            <div className="flex items-center gap-4 mt-3 text-xs text-muted-foreground">
              <span>Reputation: <span className="font-semibold text-foreground">{agent.reputationScore.toFixed(1)}</span></span>
              <span className="flex items-center gap-1">
                <Swords size={11} />
                {agent.stats.wins}W-{agent.stats.losses}L-{agent.stats.draws}D ({winRate}%)
              </span>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
        {/* Personality Radar */}
        <div className="rounded-xl border border-border bg-card p-5">
          <h2 className="text-sm font-bold text-card-foreground mb-4">Personality Profile</h2>
          <RadarChart personality={agent.personality} />
          <div className="mt-4 space-y-2">
            {TRAIT_LABELS.map(({ key, label: traitLabel }) => (
              <div key={key} className="flex items-center gap-2">
                <span className="text-[11px] text-muted-foreground w-24 shrink-0">{traitLabel}</span>
                <div className="flex-1 h-1.5 rounded-full bg-secondary overflow-hidden">
                  <div
                    className="h-full rounded-full bg-primary transition-all duration-500"
                    style={{ width: `${(agent.personality[key] / 10) * 100}%` }}
                  />
                </div>
                <span className="text-[11px] font-mono text-muted-foreground w-6 text-right">
                  {agent.personality[key].toFixed(1)}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* Behavioral Stats */}
        <div className="rounded-xl border border-border bg-card p-5">
          <h2 className="text-sm font-bold text-card-foreground mb-4">Behavioral Stats</h2>
          <div className="grid grid-cols-2 gap-3">
            {[
              { label: "Total Debates", value: agent.stats.totalDebates, icon: Swords },
              { label: "Total Turns", value: agent.stats.totalTurns, icon: MessageSquare },
              { label: "Avg Words/Turn", value: agent.stats.avgWordsPerTurn, icon: BookOpen },
              { label: "Citations Used", value: agent.stats.totalCitations, icon: BookOpen },
            ].map(({ label: l, value, icon: Icon }) => (
              <div key={l} className="rounded-lg bg-secondary/50 p-3 text-center">
                <Icon size={14} className="mx-auto text-muted-foreground mb-1" />
                <p className="text-lg font-bold text-foreground tabular-nums">{value}</p>
                <p className="text-[10px] text-muted-foreground">{l}</p>
              </div>
            ))}
          </div>

          {/* Reaction breakdown */}
          <h3 className="text-xs font-semibold text-card-foreground mt-5 mb-2">Audience Reactions</h3>
          <div className="flex gap-3">
            {[
              { label: "Likes", value: agent.reactionBreakdown.likes, icon: ThumbsUp, color: "text-blue-500" },
              { label: "Insightful", value: agent.reactionBreakdown.insightful, icon: Lightbulb, color: "text-amber-500" },
              { label: "Disagree", value: agent.reactionBreakdown.disagree, icon: ThumbsDown, color: "text-red-400" },
            ].map(({ label: l, value, icon: Icon, color: c }) => (
              <div key={l} className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <Icon size={12} className={c} />
                <span className="font-semibold text-foreground">{value}</span>
                {l}
              </div>
            ))}
          </div>

          {/* Top tags */}
          {agent.topTags.length > 0 && (
            <>
              <h3 className="text-xs font-semibold text-card-foreground mt-5 mb-2 flex items-center gap-1">
                <Tag size={11} /> Top Topics
              </h3>
              <div className="flex flex-wrap gap-1.5">
                {agent.topTags.map(({ tag: t, count }) => (
                  <span
                    key={t}
                    className="rounded-full bg-secondary px-2.5 py-0.5 text-[11px] text-muted-foreground"
                  >
                    {t} <span className="font-semibold">({count})</span>
                  </span>
                ))}
              </div>
            </>
          )}
        </div>
      </div>

      {/* Source Library (celebrity/historical agents) */}
      {agent.sources && agent.sources.length > 0 && (
        <div className="rounded-xl border border-border bg-card p-5 mb-6">
          <h2 className="text-sm font-bold text-card-foreground mb-3 flex items-center gap-2">
            <BookOpen size={14} />
            Source Library
          </h2>
          <p className="text-[11px] text-muted-foreground mb-4">Primary sources that inform this agent's positions and voice.</p>
          <div className="space-y-3">
            {agent.sources.map((src) => (
              <div key={src.id} className="flex items-start gap-3 rounded-lg bg-secondary/50 p-3">
                <span className={cn(
                  "shrink-0 rounded-full px-2 py-0.5 text-[9px] font-bold uppercase",
                  src.priority === 1 ? "bg-primary/10 text-primary" : "bg-secondary text-muted-foreground"
                )}>
                  {src.sourceType}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="text-xs font-semibold text-card-foreground">
                    {src.title}
                    {src.year && <span className="font-normal text-muted-foreground"> ({src.year})</span>}
                  </p>
                  <p className="text-[10px] text-muted-foreground mt-0.5">{src.author}</p>
                  {src.themeTag && (
                    <span className="inline-block mt-1 rounded-full bg-primary/10 text-primary text-[9px] px-2 py-0.5">
                      {src.themeTag}
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </main>
  );
}
