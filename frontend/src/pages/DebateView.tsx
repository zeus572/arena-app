import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router-dom";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { fetchDebate, castVote, addReaction, fetchPredictions, makePrediction } from "@/api/client";
import type { DebateDetail, TurnDetail, TurnCitation, PredictionData } from "@/api/types";
import { cn } from "@/lib/utils";
import { getAgentColor, getAgentLabel, BUBBLE_BG, type AgentColor } from "@/lib/agent-colors";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { Button } from "@/components/ui/button";
import { ThumbsUp, ThumbsDown, Lightbulb, ChevronLeft, Trophy, Scale, Target, Check, X, Activity } from "lucide-react";

function ReactionRow({
  turnId,
  debateId,
  targetType,
  counts,
}: {
  turnId?: string;
  debateId?: string;
  targetType: "debate" | "turn";
  counts: Record<string, number>;
}) {
  const [state, setState] = useState(counts);
  const [voted, setVoted] = useState<string | null>(null);

  const targetId = targetType === "turn" ? turnId! : debateId!;

  const vote = async (key: string) => {
    if (voted === key) return;
    setState((p) => ({ ...p, [key]: (p[key] ?? 0) + 1 }));
    setVoted(key);
    try {
      await addReaction(targetType, targetId, key);
    } catch {
      setState((p) => ({ ...p, [key]: (p[key] ?? 1) - 1 }));
      setVoted(null);
    }
  };

  const fmt = (n: number) => (n >= 1000 ? `${(n / 1000).toFixed(1)}k` : String(n));

  const buttons = [
    { key: "agree", label: "like", icon: ThumbsUp, activeClass: "bg-primary text-primary-foreground" },
    { key: "disagree", label: "disagree", icon: ThumbsDown, activeClass: "bg-destructive text-white" },
    { key: "insightful", label: "insightful", icon: Lightbulb, activeClass: "bg-accent-foreground text-accent" },
  ];

  return (
    <div className="flex items-center gap-1 mt-2">
      {buttons.map(({ key, label, icon: Icon, activeClass }) => (
        <button
          key={key}
          onClick={() => vote(label)}
          className={cn(
            "flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-medium transition-colors",
            voted === label
              ? activeClass
              : "bg-secondary text-muted-foreground hover:text-foreground"
          )}
        >
          <Icon size={11} />
          {fmt(state[label] ?? 0)}
        </button>
      ))}
    </div>
  );
}

function ArbiterCard({ turn }: { turn: TurnDetail }) {
  const isClosing = turn.content.toLowerCase().includes("closed");
  return (
    <div className="flex justify-center py-4">
      <div className="rounded-xl border-2 border-amber-500/30 bg-amber-500/5 px-6 py-4 text-center max-w-md">
        <Scale size={20} className="mx-auto mb-2 text-amber-500" />
        <p className="text-sm font-semibold text-amber-600 dark:text-amber-400">
          {isClosing ? "Debate Closed" : "The Arbiter Has Intervened"}
        </p>
        <p className="text-xs text-muted-foreground mt-1">
          {isClosing
            ? "Both sides have presented their compromise proposals. Cast your vote for the most compelling argument."
            : "Both agents must now find common ground and propose a compromise budget."}
        </p>
      </div>
    </div>
  );
}

function TurnBubble({
  turn,
  isLeft,
  debateId,
  agentColor,
}: {
  turn: TurnDetail;
  isLeft: boolean;
  debateId: string;
  agentColor: AgentColor;
}) {
  const isCompromise = turn.type === "Compromise";

  return (
    <div className={cn("flex gap-3", isLeft ? "flex-row" : "flex-row-reverse")}>
      <div className="shrink-0 mt-1">
        <AgentAvatar
          agent={{ name: turn.agent.name, color: agentColor }}
          size="md"
        />
      </div>
      <div className={cn("max-w-[92%] flex flex-col", isLeft ? "items-start" : "items-end")}>
        {isCompromise && (
          <span className="text-[10px] font-semibold text-purple-500 uppercase tracking-wide mb-1 px-1">
            Compromise Proposal
          </span>
        )}
        <div
          className={cn(
            "rounded-2xl px-4 py-3 text-sm leading-relaxed prose prose-sm max-w-none",
            "prose-p:my-2 prose-ul:my-2 prose-ol:my-2 prose-li:my-0.5",
            "prose-strong:text-inherit prose-em:text-inherit",
            "prose-table:my-3 prose-table:text-xs prose-th:px-3 prose-th:py-1.5 prose-th:text-left prose-th:font-semibold prose-th:border-b prose-th:border-border",
            "prose-td:px-3 prose-td:py-1.5 prose-td:border-b prose-td:border-border/50",
            "prose-thead:bg-secondary/50 prose-tr:border-0",
            isCompromise
              ? "rounded-tl-sm border border-purple-500/20 bg-purple-500/5 text-foreground"
              : isLeft
                ? cn("rounded-tl-sm text-foreground", BUBBLE_BG[agentColor])
                : cn("rounded-tr-sm text-foreground", BUBBLE_BG[agentColor])
          )}
        >
          <Markdown remarkPlugins={[remarkGfm]}>{turn.content}</Markdown>
          {turn.citationsJson && (() => {
            try {
              const raw = JSON.parse(turn.citationsJson) as Record<string, string>[];
              const citations: TurnCitation[] = raw.map((c) => ({
                source: c.source || c.Source || "",
                title: c.title || c.Title || "",
                url: c.url || c.Url || "",
              }));
              if (citations.length === 0) return null;
              return (
                <div className="mt-3 pt-2 border-t border-border/30">
                  <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wide mb-1">Sources</p>
                  <div className="flex flex-col gap-1">
                    {citations.map((c, i) => (
                      <a
                        key={i}
                        href={c.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-[11px] text-primary hover:underline truncate block"
                        onClick={(e) => e.stopPropagation()}
                      >
                        [{c.source}] {c.title}
                      </a>
                    ))}
                  </div>
                </div>
              );
            } catch {
              return null;
            }
          })()}
        </div>
        <div className={cn("mt-1 px-1 flex items-center gap-2", isLeft ? "" : "flex-row-reverse")}>
          <ReactionRow
            targetType="turn"
            turnId={turn.id}
            debateId={debateId}
            counts={turn.reactions}
          />
          <span className="text-[10px] text-muted-foreground/60">
            {new Date(turn.createdAt).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}
          </span>
        </div>
      </div>
    </div>
  );
}

function computeMomentum(
  turns: TurnDetail[],
  proponentId: string,
  proponentVotes: number,
  opponentVotes: number,
) {
  // Start at 50 (neutral). Each turn shifts momentum based on reactions.
  // positive reactions (like, insightful, fire) boost, disagree hurts
  let momentum = 50;
  const history: { turnNumber: number; momentum: number; agentId: string }[] = [];

  for (const turn of turns) {
    if (turn.type === "Arbiter") continue;
    const r = turn.reactions;
    const positive = (r["like"] ?? 0) + (r["insightful"] ?? 0) + (r["fire"] ?? 0);
    const negative = r["disagree"] ?? 0;
    const net = positive - negative;
    const shift = Math.min(15, Math.max(-15, net * 3));
    const isProponent = turn.agentId === proponentId;
    momentum += isProponent ? shift : -shift;
    momentum = Math.max(5, Math.min(95, momentum));
    history.push({ turnNumber: turn.turnNumber, momentum, agentId: turn.agentId });
  }

  // Factor in votes slightly
  const totalVotes = proponentVotes + opponentVotes;
  if (totalVotes > 0) {
    const voteShift = ((proponentVotes / totalVotes) - 0.5) * 10;
    momentum = Math.max(5, Math.min(95, momentum + voteShift));
  }

  return { current: Math.round(momentum), history };
}

function MomentumMeter({
  debate,
  proponentColor,
  opponentColor,
}: {
  debate: DebateDetail;
  proponentColor: AgentColor;
  opponentColor: AgentColor;
}) {
  const argumentTurns = debate.turns.filter((t) => t.type !== "Arbiter");
  if (argumentTurns.length < 2) return null;

  const { current } = computeMomentum(
    debate.turns,
    debate.proponent.id,
    debate.proponentVotes,
    debate.opponentVotes,
  );

  const proLeading = current > 55;
  const oppLeading = current < 45;
  const label = proLeading
    ? `${debate.proponent.name} has momentum`
    : oppLeading
      ? `${debate.opponent.name} has momentum`
      : "Evenly matched";

  return (
    <div className="rounded-xl border border-border bg-card p-4 mb-6">
      <div className="flex items-center gap-2 mb-3">
        <Activity size={14} className="text-blue-500" />
        <h3 className="text-sm font-semibold text-card-foreground">Momentum</h3>
        <span className="text-[10px] text-muted-foreground ml-auto">{label}</span>
      </div>

      <div className="relative">
        {/* Track */}
        <div className="h-3 w-full rounded-full bg-secondary overflow-hidden flex">
          <div
            className={cn("h-full transition-all duration-700 ease-out", `bg-${proponentColor}`)}
            style={{ width: `${current}%` }}
          />
          <div
            className={cn("h-full transition-all duration-700 ease-out", `bg-${opponentColor}`)}
            style={{ width: `${100 - current}%` }}
          />
        </div>

        {/* Center marker */}
        <div className="absolute top-0 left-1/2 -translate-x-px h-3 w-0.5 bg-foreground/30" />

        {/* Indicator */}
        <div
          className="absolute -top-0.5 h-4 w-4 rounded-full border-2 border-background bg-foreground shadow transition-all duration-700 ease-out"
          style={{ left: `${current}%`, transform: "translateX(-50%)" }}
        />
      </div>

      <div className="flex justify-between mt-2">
        <span className="text-[10px] text-muted-foreground">{debate.proponent.name}</span>
        <span className="text-[10px] font-mono text-muted-foreground">{current}–{100 - current}</span>
        <span className="text-[10px] text-muted-foreground">{debate.opponent.name}</span>
      </div>
    </div>
  );
}

function PredictionWidget({
  debateId,
  debate,
  proponentColor,
  opponentColor,
}: {
  debateId: string;
  debate: DebateDetail;
  proponentColor: AgentColor;
  opponentColor: AgentColor;
}) {
  const [prediction, setPrediction] = useState<PredictionData | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    fetchPredictions(debateId).then(setPrediction).catch(() => {});
  }, [debateId]);

  const handlePredict = async (agentId: string) => {
    if (submitting) return;
    setSubmitting(true);
    try {
      await makePrediction(debateId, agentId);
      const updated = await fetchPredictions(debateId);
      setPrediction(updated);
    } catch {
      // ignore
    }
    setSubmitting(false);
  };

  if (!prediction) return null;

  const isCompleted = debate.status === "Completed";
  const hasPredicted = !!prediction.userPredictedAgentId;
  const total = prediction.totalPredictions;
  const proOdds = prediction.proponentOdds;
  const oppOdds = prediction.opponentOdds;

  return (
    <div className="rounded-xl border border-border bg-card p-4 mb-6">
      <div className="flex items-center gap-2 mb-3">
        <Target size={14} className="text-purple-500" />
        <h3 className="text-sm font-semibold text-card-foreground">
          {isCompleted ? "Prediction Results" : "Who Will Win?"}
        </h3>
        {total > 0 && (
          <span className="text-[10px] text-muted-foreground ml-auto">
            {total} prediction{total !== 1 ? "s" : ""}
          </span>
        )}
      </div>

      {/* Odds Bar */}
      {total > 0 && (
        <div className="mb-3">
          <div className="h-2.5 w-full rounded-full bg-secondary overflow-hidden flex">
            <div
              className={cn("h-full transition-all duration-500", `bg-${proponentColor}`)}
              style={{ width: `${proOdds}%` }}
            />
            <div
              className={cn("h-full transition-all duration-500", `bg-${opponentColor}`)}
              style={{ width: `${oppOdds}%` }}
            />
          </div>
          <div className="flex justify-between mt-1">
            <span className="text-[11px] text-muted-foreground">
              {debate.proponent.name} {proOdds.toFixed(0)}%
            </span>
            <span className="text-[11px] text-muted-foreground">
              {oppOdds.toFixed(0)}% {debate.opponent.name}
            </span>
          </div>
        </div>
      )}

      {/* Post-debate reveal */}
      {isCompleted && hasPredicted && (
        <div
          className={cn(
            "rounded-lg px-3 py-2 text-xs font-medium flex items-center gap-2",
            prediction.userIsCorrect
              ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
              : "bg-red-500/10 text-red-500"
          )}
        >
          {prediction.userIsCorrect ? (
            <>
              <Check size={14} />
              You predicted correctly!
            </>
          ) : (
            <>
              <X size={14} />
              Your prediction was wrong.
            </>
          )}
        </div>
      )}

      {/* Pre/during debate prediction buttons */}
      {!isCompleted && !hasPredicted && (
        <div className="flex gap-2">
          <button
            onClick={() => handlePredict(debate.proponent.id)}
            disabled={submitting}
            className={cn(
              "flex-1 flex items-center justify-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium transition-colors",
              "hover:border-primary/40 hover:bg-primary/5 disabled:opacity-50"
            )}
          >
            <AgentAvatar agent={{ name: debate.proponent.name, color: proponentColor }} size="sm" />
            {debate.proponent.name}
          </button>
          <button
            onClick={() => handlePredict(debate.opponent.id)}
            disabled={submitting}
            className={cn(
              "flex-1 flex items-center justify-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium transition-colors",
              "hover:border-primary/40 hover:bg-primary/5 disabled:opacity-50"
            )}
          >
            <AgentAvatar agent={{ name: debate.opponent.name, color: opponentColor }} size="sm" />
            {debate.opponent.name}
          </button>
        </div>
      )}

      {/* Already predicted, not completed */}
      {!isCompleted && hasPredicted && (
        <p className="text-xs text-muted-foreground">
          You predicted{" "}
          <span className="font-semibold text-foreground">
            {prediction.userPredictedAgentId === debate.proponent.id
              ? debate.proponent.name
              : debate.opponent.name}
          </span>{" "}
          will win.
        </p>
      )}
    </div>
  );
}

export default function DebateViewPage() {
  const { id } = useParams<{ id: string }>();
  const [debate, setDebate] = useState<DebateDetail | null>(null);
  const [userVote, setUserVote] = useState<string | null>(null);

  const loadDebate = useCallback(async () => {
    if (!id) return;
    const data = await fetchDebate(id);
    setDebate(data);
  }, [id]);

  useEffect(() => {
    loadDebate();
  }, [loadDebate]);

  useEffect(() => {
    if (id && localStorage.getItem(`vote-${id}`)) {
      setUserVote(localStorage.getItem(`vote-${id}`));
    }
  }, [id]);

  useEffect(() => {
    if (!debate || (debate.status !== "Active" && debate.status !== "Compromising")) return;
    const interval = setInterval(loadDebate, 10_000);
    return () => clearInterval(interval);
  }, [debate?.status, loadDebate]);

  if (!debate) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8">
        <div className="rounded-xl border border-border bg-card p-6 h-64 animate-pulse" />
      </main>
    );
  }

  const proponentColor = getAgentColor(debate.proponent.persona ?? "");
  const opponentColor = getAgentColor(debate.opponent.persona ?? "");
  const proponentLabel = getAgentLabel(debate.proponent.persona ?? "");
  const opponentLabel = getAgentLabel(debate.opponent.persona ?? "");

  const totalVotes = debate.proponentVotes + debate.opponentVotes;
  const pctA = totalVotes > 0 ? (debate.proponentVotes / totalVotes) * 100 : 50;
  const winner =
    debate.status === "Completed"
      ? debate.proponentVotes > debate.opponentVotes
        ? debate.proponent
        : debate.opponent
      : null;

  const handleVote = async (agentId: string) => {
    if (!id || userVote) return;
    try {
      await castVote(id, agentId);
      localStorage.setItem(`vote-${id}`, agentId);
      setUserVote(agentId);
      await loadDebate();
    } catch {
      // ignore
    }
  };

  const isLive = debate.status === "Active" || debate.status === "Compromising";

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <Link to="/">
        <Button variant="ghost" size="sm" className="mb-5 gap-1.5 text-xs text-muted-foreground -ml-2">
          <ChevronLeft size={14} />
          Back to Feed
        </Button>
      </Link>

      <div className="rounded-xl border border-border bg-card p-6 mb-6">
        <div className="flex items-center justify-between mb-4 flex-wrap gap-3">
          <span
            className={cn(
              "flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold",
              debate.status === "Active"
                ? "bg-red-500/10 text-red-500"
                : debate.status === "Compromising"
                  ? "bg-amber-500/10 text-amber-600"
                  : "bg-secondary text-muted-foreground"
            )}
          >
            {debate.status === "Active" ? (
              <>
                <span className="h-1.5 w-1.5 rounded-full bg-red-500 animate-pulse" />
                LIVE
              </>
            ) : debate.status === "Compromising" ? (
              <>
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" />
                <Scale size={12} />
                FINDING COMPROMISE
              </>
            ) : (
              debate.status
            )}
          </span>
          <span className="text-[11px] text-muted-foreground">
            {new Date(debate.createdAt).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" })}
          </span>
        </div>

        <h1 className="text-lg font-bold text-card-foreground leading-snug mb-6 text-balance">
          {debate.topic}
        </h1>
        {debate.description && (
          <p className="text-sm text-muted-foreground mb-4">{debate.description}</p>
        )}

        <div className="flex items-center justify-between gap-4">
          <div className="flex flex-col items-center gap-2 text-center flex-1">
            <AgentAvatar agent={{ name: debate.proponent.name, color: proponentColor }} size="xl" />
            <div>
              <p className="font-semibold text-sm text-card-foreground">{debate.proponent.name}</p>
              <IdeologyBadge label={proponentLabel} color={proponentColor} />
            </div>
          </div>
          <div className="flex flex-col items-center gap-1 shrink-0">
            <span className="text-xl font-black text-muted-foreground/30">VS</span>
          </div>
          <div className="flex flex-col items-center gap-2 text-center flex-1">
            <AgentAvatar agent={{ name: debate.opponent.name, color: opponentColor }} size="xl" />
            <div>
              <p className="font-semibold text-sm text-card-foreground">{debate.opponent.name}</p>
              <IdeologyBadge label={opponentLabel} color={opponentColor} />
            </div>
          </div>
        </div>
      </div>

      <MomentumMeter
        debate={debate}
        proponentColor={proponentColor}
        opponentColor={opponentColor}
      />

      <PredictionWidget
        debateId={debate.id}
        debate={debate}
        proponentColor={proponentColor}
        opponentColor={opponentColor}
      />

      <section className="flex flex-col gap-6 mb-8" aria-label="Debate turns">
        {debate.turns.map((turn) => {
          if (turn.type === "Arbiter") {
            return <ArbiterCard key={turn.id} turn={turn} />;
          }

          const isA = turn.agentId === debate.proponent.id;
          return (
            <TurnBubble
              key={turn.id}
              turn={turn}
              isLeft={isA}
              debateId={debate.id}
              agentColor={isA ? proponentColor : opponentColor}
            />
          );
        })}
        {isLive && (
          <p className="text-center text-xs text-muted-foreground italic py-4">
            {debate.status === "Compromising"
              ? "Agents are negotiating a compromise..."
              : debate.turns.length === 0
                ? "Waiting for the first argument..."
                : "Waiting for next turn..."}
          </p>
        )}
      </section>

      <div className="rounded-xl border border-border bg-card p-6">
        <h2 className="text-sm font-semibold text-card-foreground mb-4 flex items-center gap-2">
          {winner ? (
            <>
              <Trophy size={15} className="text-amber-500" />
              Winner: {winner.name}
            </>
          ) : (
            "Vote for the winner"
          )}
        </h2>

        <div className="mb-4">
          <div className="h-3 w-full rounded-full bg-secondary overflow-hidden flex">
            <div className={cn("h-full transition-all duration-500", `bg-${proponentColor}`)} style={{ width: `${pctA}%` }} />
            <div className={cn("h-full transition-all duration-500", `bg-${opponentColor}`)} style={{ width: `${100 - pctA}%` }} />
          </div>
          <div className="flex justify-between mt-2">
            <span className="text-xs font-semibold">
              {debate.proponent.name} — {debate.proponentVotes} votes ({pctA.toFixed(0)}%)
            </span>
            <span className="text-xs font-semibold">
              {(100 - pctA).toFixed(0)}% — {debate.opponentVotes} votes — {debate.opponent.name}
            </span>
          </div>
        </div>

        {!userVote ? (
          <div className="flex gap-3 flex-wrap">
            <Button
              size="sm"
              variant="outline"
              className="gap-2 text-xs"
              onClick={() => handleVote(debate.proponent.id)}
            >
              <AgentAvatar agent={{ name: debate.proponent.name, color: proponentColor }} size="sm" />
              {debate.proponent.name} Won
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="gap-2 text-xs"
              onClick={() => handleVote(debate.opponent.id)}
            >
              <AgentAvatar agent={{ name: debate.opponent.name, color: opponentColor }} size="sm" />
              {debate.opponent.name} Won
            </Button>
          </div>
        ) : (
          <p className="text-xs text-muted-foreground">
            You voted for{" "}
            <span className="font-semibold text-foreground">
              {userVote === debate.proponent.id ? debate.proponent.name : debate.opponent.name}
            </span>
            . Thanks for participating.
          </p>
        )}
      </div>
    </main>
  );
}
