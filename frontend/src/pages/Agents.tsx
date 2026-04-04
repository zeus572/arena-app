import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchAgents } from "@/api/client";
import type { Agent } from "@/api/types";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { getAgentColor, getAgentLabel } from "@/lib/agent-colors";
import { ChevronDown, ChevronUp, Trophy, Flame, Swords } from "lucide-react";

const META_FILTERS = [
  "don't break character", "do not break character",
  "do not reference", "you are a debate", "you are an ai",
  "markdown", "formatting", "bold", "italic",
  "respond as", "numbered reference", "tool",
  "core beliefs:",
];

function extractPoliticalLeanings(persona: string): string[] {
  return persona
    .split(/[.\n]/)
    .map((s) => s.trim())
    .filter((s) => s.length > 15)
    .filter((s) => {
      const lower = s.toLowerCase();
      return !META_FILTERS.some((kw) => lower.includes(kw));
    });
}

export default function Agents() {
  const [agents, setAgents] = useState<Agent[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);

  useEffect(() => {
    fetchAgents().then(setAgents);
  }, []);

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <h1 className="text-2xl font-bold text-foreground mb-6">AI Agents</h1>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {agents.map((agent) => {
          const color = getAgentColor(agent.persona, agent.agentType);
          const label = getAgentLabel(agent.persona, agent.agentType);
          const isExpanded = expanded === agent.id;
          const leanings = extractPoliticalLeanings(agent.persona);

          return (
            <Link
              key={agent.id}
              to={`/agents/${agent.id}`}
              className="rounded-xl border border-border bg-card p-5 flex flex-col gap-3 no-underline hover:border-primary/30 transition-colors"
            >
              <div className="flex items-start gap-3">
                <AgentAvatar agent={{ name: agent.name, color }} size="lg" />
                <div>
                  <p className="font-semibold text-sm text-card-foreground">{agent.name}</p>
                  <IdeologyBadge label={label} color={color} />
                  <p className="text-xs text-muted-foreground mt-1">
                    Reputation: {agent.reputationScore.toFixed(1)}
                  </p>
                  {agent.stats?.title && (
                    <p className="flex items-center gap-1 text-[10px] font-semibold text-amber-600 dark:text-amber-400 mt-1">
                      <Trophy size={10} /> {agent.stats.title}
                    </p>
                  )}
                </div>
              </div>
              <p className="text-xs text-muted-foreground leading-relaxed">{agent.description}</p>

              {agent.stats && agent.stats.totalDebates > 0 && (
                <div className="flex items-center gap-3 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1">
                    <Swords size={11} /> {agent.stats.wins}W-{agent.stats.losses}L-{agent.stats.draws}D
                  </span>
                  {agent.stats.winStreak >= 2 && (
                    <span className="flex items-center gap-1 text-orange-500 font-medium">
                      <Flame size={11} /> {agent.stats.winStreak} streak
                    </span>
                  )}
                  {agent.stats.topTag && (
                    <span className="text-[10px] rounded-full bg-secondary px-2 py-0.5">
                      {agent.stats.topTag}
                    </span>
                  )}
                </div>
              )}

              {leanings.length > 0 && (
                <>
                  <button
                    onClick={() => setExpanded(isExpanded ? null : agent.id)}
                    className="flex items-center gap-1 text-[11px] font-medium text-primary hover:underline self-start"
                  >
                    {isExpanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                    {isExpanded ? "Hide positions" : "View positions"}
                  </button>
                  {isExpanded && (
                    <ul className="text-[12px] text-muted-foreground leading-relaxed space-y-2 pl-3 border-l-2 border-primary/20">
                      {leanings.map((line, i) => (
                        <li key={i}>{line}</li>
                      ))}
                    </ul>
                  )}
                </>
              )}
            </Link>
          );
        })}
      </div>
    </main>
  );
}
