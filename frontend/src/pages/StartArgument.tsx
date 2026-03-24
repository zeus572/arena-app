import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { fetchAgents, createDebate } from "@/api/client";
import type { Agent } from "@/api/types";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { getAgentColor, getAgentLabel } from "@/lib/agent-colors";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { Swords, ChevronLeft, Sparkles, CheckCircle2 } from "lucide-react";

const SUGGESTED_TOPICS = [
  "Should the minimum wage be raised to $20/hour?",
  "Is nuclear energy essential for meeting climate goals?",
  "Should social media platforms be regulated as public utilities?",
  "Does free trade benefit American workers?",
  "Should the United States abolish the Electoral College?",
  "Is universal basic income a viable economic policy?",
];

export default function StartArgument() {
  const navigate = useNavigate();
  const [agents, setAgents] = useState<Agent[]>([]);
  const [topic, setTopic] = useState("");
  const [selected, setSelected] = useState<string[]>([]);
  const [generating, setGenerating] = useState(false);

  useEffect(() => {
    fetchAgents().then(setAgents);
  }, []);

  const toggleAgent = (id: string) => {
    setSelected((prev) => {
      if (prev.includes(id)) return prev.filter((x) => x !== id);
      if (prev.length >= 2) return [prev[1], id];
      return [...prev, id];
    });
  };

  const canGenerate = topic.trim().length > 5 && selected.length === 2;

  const handleGenerate = async () => {
    if (!canGenerate) return;
    setGenerating(true);
    try {
      const result = await createDebate({
        topic: topic.trim(),
        proponentId: selected[0],
        opponentId: selected[1],
      });
      navigate(`/debates/${result.id}`);
    } catch {
      setGenerating(false);
    }
  };

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <Link to="/">
        <Button variant="ghost" size="sm" className="mb-5 gap-1.5 text-xs text-muted-foreground -ml-2">
          <ChevronLeft size={14} />
          Back to Feed
        </Button>
      </Link>

      <div className="mb-8">
        <h1 className="text-2xl font-bold text-foreground text-balance mb-2">Start a Debate</h1>
        <p className="text-sm text-muted-foreground leading-relaxed">
          Choose a topic and select two AI agents with opposing ideologies. They will argue it out — you decide the winner.
        </p>
      </div>

      {/* Step 1: Topic */}
      <section className="mb-8">
        <div className="flex items-center gap-2 mb-3">
          <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary text-primary-foreground text-[10px] font-bold">
            1
          </span>
          <h2 className="text-sm font-semibold text-foreground">Enter a debate topic</h2>
        </div>

        <textarea
          value={topic}
          onChange={(e) => setTopic(e.target.value)}
          placeholder="e.g. Should the United States adopt a carbon tax?"
          rows={3}
          className={cn(
            "w-full rounded-xl border border-border bg-card px-4 py-3 text-sm text-foreground placeholder:text-muted-foreground resize-none outline-none transition-colors",
            "focus:border-primary focus:ring-2 focus:ring-primary/20"
          )}
        />

        <div className="mt-3">
          <p className="text-xs text-muted-foreground mb-2 flex items-center gap-1">
            <Sparkles size={11} />
            Suggested topics
          </p>
          <div className="flex flex-wrap gap-2">
            {SUGGESTED_TOPICS.map((t) => (
              <button
                key={t}
                onClick={() => setTopic(t)}
                className={cn(
                  "rounded-full border px-3 py-1 text-xs transition-colors",
                  topic === t
                    ? "border-primary bg-primary/10 text-primary font-medium"
                    : "border-border bg-card text-muted-foreground hover:text-foreground hover:border-primary/40"
                )}
              >
                {t}
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* Step 2: Agents */}
      <section className="mb-8">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary text-primary-foreground text-[10px] font-bold">
              2
            </span>
            <h2 className="text-sm font-semibold text-foreground">Select 2 agents</h2>
          </div>
          <span className="text-xs text-muted-foreground">{selected.length}/2 selected</span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {agents.map((agent) => {
            const isSelected = selected.includes(agent.id);
            const selIdx = selected.indexOf(agent.id);
            const color = getAgentColor(agent.persona);
            return (
              <button
                key={agent.id}
                onClick={() => toggleAgent(agent.id)}
                className={cn(
                  "group relative rounded-xl border bg-card p-4 text-left transition-all",
                  isSelected
                    ? "border-primary bg-primary/5 shadow-sm"
                    : "border-border hover:border-primary/40"
                )}
              >
                {isSelected && (
                  <div className="absolute top-3 right-3 flex items-center gap-1">
                    <span className="text-[10px] font-bold text-primary">
                      {selIdx === 0 ? "Agent A" : "Agent B"}
                    </span>
                    <CheckCircle2 size={14} className="text-primary" />
                  </div>
                )}

                <div className="flex items-start gap-3">
                  <AgentAvatar agent={{ name: agent.name, color }} size="lg" />
                  <div className="min-w-0 flex-1">
                    <p className="font-semibold text-sm text-card-foreground truncate">{agent.name}</p>
                    <IdeologyBadge label={getAgentLabel(agent.persona)} color={color} />
                  </div>
                </div>
                <p className="mt-2.5 text-xs text-muted-foreground leading-relaxed line-clamp-2">
                  {agent.description}
                </p>
              </button>
            );
          })}
        </div>
      </section>

      {/* Selected preview */}
      {selected.length === 2 && (() => {
        const a = agents.find((x) => x.id === selected[0]);
        const b = agents.find((x) => x.id === selected[1]);
        if (!a || !b) return null;
        return (
          <div className="mb-6 rounded-xl border border-border bg-card p-4 flex items-center gap-3">
            <div className="flex items-center gap-2 flex-1">
              <AgentAvatar agent={{ name: a.name, color: getAgentColor(a.persona) }} size="sm" />
              <span className="text-xs font-medium text-card-foreground">{a.name}</span>
            </div>
            <Swords size={16} className="text-muted-foreground shrink-0" />
            <div className="flex items-center gap-2 flex-1 justify-end">
              <span className="text-xs font-medium text-card-foreground">{b.name}</span>
              <AgentAvatar agent={{ name: b.name, color: getAgentColor(b.persona) }} size="sm" />
            </div>
          </div>
        );
      })()}

      <Button
        size="lg"
        disabled={!canGenerate || generating}
        onClick={handleGenerate}
        className="w-full gap-2 text-sm"
      >
        {generating ? (
          <>
            <span className="h-3.5 w-3.5 rounded-full border-2 border-primary-foreground/30 border-t-primary-foreground animate-spin" />
            Creating Debate...
          </>
        ) : (
          <>
            <Swords size={15} />
            Generate Debate
          </>
        )}
      </Button>
      {!canGenerate && (
        <p className="text-center text-xs text-muted-foreground mt-2">
          {topic.trim().length <= 5
            ? "Enter a topic with at least 6 characters"
            : "Select exactly 2 agents to continue"}
        </p>
      )}
    </main>
  );
}
