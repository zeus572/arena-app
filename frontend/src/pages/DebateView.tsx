import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router-dom";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { fetchDebate, castVote, addReaction, fetchPredictions, makePrediction, fetchInterventions, submitIntervention, upvoteIntervention } from "@/api/client";
import type { DebateDetail, TurnDetail, TurnCitation, PredictionData, InterventionData } from "@/api/types";
import { cn } from "@/lib/utils";
import { getAgentColor, getAgentLabel, BUBBLE_BG, type AgentColor } from "@/lib/agent-colors";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { Button } from "@/components/ui/button";
import { ThumbsUp, ThumbsDown, Lightbulb, ChevronLeft, Trophy, Scale, Target, Check, X, Activity, ChevronDown, ChevronUp, Crosshair, BookOpen, HelpCircle, AlertTriangle, MessageCircleQuestion, ArrowUp, Send, Sparkles, Share2, Copy, Link2 } from "lucide-react";

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

function ClipButton({ turn, debateTopic }: { turn: TurnDetail; debateTopic: string }) {
  const [showCard, setShowCard] = useState(false);
  const [copied, setCopied] = useState(false);

  // Extract highlight quote from turn content
  const getQuote = () => {
    const boldMatch = turn.content.match(/\*\*([^*]{15,})\*\*/);
    if (boldMatch) return boldMatch[1];
    return turn.content.length > 180 ? turn.content.slice(0, 180) + "..." : turn.content;
  };

  const handleCopyText = () => {
    const quote = getQuote();
    const text = `"${quote}"\n— ${turn.agent.name} on "${debateTopic}"\n\nWatch the full debate: ${window.location.href}`;
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleCopyLink = () => {
    navigator.clipboard.writeText(`${window.location.href}#turn-${turn.turnNumber}`);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <>
      <button
        onClick={() => setShowCard(true)}
        className="text-[10px] text-muted-foreground/50 hover:text-primary flex items-center gap-0.5 transition-colors"
        title="Clip this moment"
      >
        <Share2 size={10} />
      </button>

      {showCard && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
          onClick={(e) => { if (e.target === e.currentTarget) setShowCard(false); }}
        >
          <div className="w-full max-w-md mx-4">
            {/* Shareable Card Preview */}
            <div className="rounded-2xl bg-gradient-to-br from-card to-secondary border border-border p-6 shadow-xl">
              <div className="flex items-center gap-2 mb-3">
                <AgentAvatar agent={{ name: turn.agent.name, color: "citizen" as AgentColor }} size="sm" />
                <span className="text-xs font-semibold text-card-foreground">{turn.agent.name}</span>
                <span className="text-[10px] text-muted-foreground ml-auto">Turn {turn.turnNumber}</span>
              </div>

              <p className="text-sm text-foreground font-medium leading-relaxed italic mb-3">
                "{getQuote()}"
              </p>

              <div className="flex items-center justify-between">
                <p className="text-[10px] text-muted-foreground truncate max-w-[60%]">
                  on: {debateTopic}
                </p>
                <p className="text-[10px] font-semibold text-primary">Debate Arena</p>
              </div>
            </div>

            {/* Action buttons */}
            <div className="flex gap-2 mt-3">
              <button
                onClick={handleCopyText}
                className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-card border border-border px-3 py-2 text-xs font-medium text-foreground hover:bg-secondary transition-colors"
              >
                <Copy size={12} />
                {copied ? "Copied!" : "Copy Quote"}
              </button>
              <button
                onClick={handleCopyLink}
                className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-card border border-border px-3 py-2 text-xs font-medium text-foreground hover:bg-secondary transition-colors"
              >
                <Link2 size={12} />
                Copy Link
              </button>
              <button
                onClick={() => setShowCard(false)}
                className="rounded-lg bg-card border border-border px-3 py-2 text-xs text-muted-foreground hover:bg-secondary transition-colors"
              >
                <X size={12} />
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

interface ArgumentAnalysis {
  claims: string[];
  evidence: string[];
  assumptions: string[];
  weaknesses: string[];
}

function analyzeArgument(content: string, analysisJson?: string | null): ArgumentAnalysis {
  // Use pre-computed analysis if available
  if (analysisJson) {
    try {
      return JSON.parse(analysisJson) as ArgumentAnalysis;
    } catch { /* fall through to heuristic */ }
  }

  // Heuristic extraction from markdown
  const lines = content.split("\n").filter((l) => l.trim());

  // Claims: bold text
  const claims: string[] = [];
  const boldRegex = /\*\*([^*]+)\*\*/g;
  for (const line of lines) {
    let match;
    while ((match = boldRegex.exec(line)) !== null) {
      const claim = match[1].trim();
      if (claim.length > 15 && !claim.toLowerCase().startsWith("total")) {
        claims.push(claim);
      }
    }
  }

  // Evidence: lines with citation markers [1], [2], etc.
  const evidence = lines
    .filter((l) => /\[\d+\]/.test(l))
    .map((l) => l.replace(/^[-*•]\s*/, "").trim())
    .filter((l) => l.length > 20)
    .slice(0, 5);

  // Assumptions: conditional or presuppositional phrases
  const assumptionKeywords = [
    "assuming", "if we", "given that", "presumes", "relies on",
    "would mean", "should lead", "likely", "presumably",
  ];
  const assumptions = lines
    .filter((l) => {
      const lower = l.toLowerCase();
      return assumptionKeywords.some((kw) => lower.includes(kw));
    })
    .map((l) => l.replace(/^[-*•]\s*/, "").replace(/\*\*/g, "").trim())
    .slice(0, 3);

  // Weaknesses: look for hedging language
  const hedgeKeywords = [
    "however", "although", "despite", "admittedly", "granted",
    "to be fair", "challenge", "limitation", "risk",
  ];
  const weaknesses = lines
    .filter((l) => {
      const lower = l.toLowerCase();
      return hedgeKeywords.some((kw) => lower.includes(kw));
    })
    .map((l) => l.replace(/^[-*•]\s*/, "").replace(/\*\*/g, "").trim())
    .slice(0, 3);

  return { claims: claims.slice(0, 5), evidence, assumptions, weaknesses };
}

function ArgumentBreakdown({ turn }: { turn: TurnDetail }) {
  const [open, setOpen] = useState(false);
  const analysis = analyzeArgument(turn.content, turn.analysisJson);
  const hasContent =
    analysis.claims.length > 0 ||
    analysis.evidence.length > 0 ||
    analysis.assumptions.length > 0 ||
    analysis.weaknesses.length > 0;

  if (!hasContent) return null;

  const sections = [
    { key: "claims", label: "Key Claims", icon: Crosshair, items: analysis.claims, color: "text-blue-500" },
    { key: "evidence", label: "Evidence", icon: BookOpen, items: analysis.evidence, color: "text-emerald-500" },
    { key: "assumptions", label: "Assumptions", icon: HelpCircle, items: analysis.assumptions, color: "text-amber-500" },
    { key: "weaknesses", label: "Potential Weaknesses", icon: AlertTriangle, items: analysis.weaknesses, color: "text-red-400" },
  ].filter((s) => s.items.length > 0);

  return (
    <div className="mt-2">
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-1 text-[10px] font-medium text-primary/70 hover:text-primary transition-colors"
      >
        {open ? <ChevronUp size={10} /> : <ChevronDown size={10} />}
        Argument Breakdown
      </button>
      {open && (
        <div className="mt-2 rounded-lg border border-border/50 bg-background/50 p-3 space-y-3">
          {sections.map(({ key, label, icon: Icon, items, color }) => (
            <div key={key}>
              <p className={cn("text-[10px] font-semibold uppercase tracking-wide flex items-center gap-1 mb-1", color)}>
                <Icon size={10} /> {label}
              </p>
              <ul className="space-y-1">
                {items.map((item, i) => (
                  <li key={i} className="text-[11px] text-muted-foreground leading-relaxed pl-3 border-l-2 border-border/50">
                    {item.length > 120 ? item.slice(0, 120) + "..." : item}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function TurnBubble({
  turn,
  isLeft,
  debateId,
  debateTopic,
  agentColor,
}: {
  turn: TurnDetail;
  isLeft: boolean;
  debateId: string;
  debateTopic: string;
  agentColor: AgentColor;
}) {
  const isCompromise = turn.type === "Compromise";
  const isWildcard = turn.type === "Wildcard";

  return (
    <div className={cn(
      "flex gap-3",
      isWildcard ? "flex-row justify-center" : isLeft ? "flex-row" : "flex-row-reverse",
    )}>
      <div className="shrink-0 mt-1">
        <AgentAvatar
          agent={{ name: turn.agent.name, color: isWildcard ? "wildcard" as AgentColor : agentColor }}
          size="md"
        />
      </div>
      <div className={cn("max-w-[92%] flex flex-col", isWildcard ? "items-center" : isLeft ? "items-start" : "items-end")}>
        {isCompromise && (
          <span className="text-[10px] font-semibold text-purple-500 uppercase tracking-wide mb-1 px-1">
            Compromise Proposal
          </span>
        )}
        {isWildcard && (
          <span className="flex items-center gap-1 text-[10px] font-semibold text-amber-500 uppercase tracking-wide mb-1 px-1">
            <Sparkles size={10} /> Wildcard: {turn.agent.name}
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
            isWildcard
              ? "rounded-sm border border-amber-500/30 bg-amber-500/5 text-foreground"
              : isCompromise
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
          {turn.type !== "Arbiter" && <ClipButton turn={turn} debateTopic={debateTopic} />}
        </div>
        {turn.type !== "Arbiter" && <ArgumentBreakdown turn={turn} />}
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

function InterventionPanel({ debateId, isLive }: { debateId: string; isLive: boolean }) {
  const [interventions, setInterventions] = useState<InterventionData[]>([]);
  const [question, setQuestion] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [voted, setVoted] = useState<Set<string>>(new Set());

  useEffect(() => {
    fetchInterventions(debateId).then(setInterventions).catch(() => {});
  }, [debateId]);

  const handleSubmit = async () => {
    if (!question.trim() || submitting) return;
    setSubmitting(true);
    try {
      await submitIntervention(debateId, question.trim());
      setQuestion("");
      const updated = await fetchInterventions(debateId);
      setInterventions(updated);
    } catch {
      // ignore
    }
    setSubmitting(false);
  };

  const handleUpvote = async (id: string) => {
    if (voted.has(id)) return;
    setVoted((prev) => new Set(prev).add(id));
    setInterventions((prev) =>
      prev.map((i) => (i.id === id ? { ...i, upvotes: i.upvotes + 1 } : i))
    );
    try {
      await upvoteIntervention(debateId, id);
    } catch {
      setVoted((prev) => { const s = new Set(prev); s.delete(id); return s; });
      setInterventions((prev) =>
        prev.map((i) => (i.id === id ? { ...i, upvotes: i.upvotes - 1 } : i))
      );
    }
  };

  const pending = interventions.filter((i) => !i.used);
  const used = interventions.filter((i) => i.used);

  return (
    <div className="rounded-xl border border-border bg-card p-4 mb-6">
      <div className="flex items-center gap-2 mb-3">
        <MessageCircleQuestion size={14} className="text-indigo-500" />
        <h3 className="text-sm font-semibold text-card-foreground">Crowd Questions</h3>
        <span className="text-[10px] text-muted-foreground ml-auto">
          Top-voted questions get injected into the next turn
        </span>
      </div>

      {/* Submit form — only for live debates */}
      {isLive && (
        <div className="flex gap-2 mb-3">
          <input
            type="text"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Ask the agents a question..."
            maxLength={280}
            className="flex-1 rounded-lg border border-border bg-background px-3 py-1.5 text-xs placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary"
            onKeyDown={(e) => e.key === "Enter" && handleSubmit()}
          />
          <button
            onClick={handleSubmit}
            disabled={submitting || question.trim().length < 10}
            className="rounded-lg bg-primary px-3 py-1.5 text-xs font-medium text-primary-foreground disabled:opacity-50 flex items-center gap-1"
          >
            <Send size={11} /> Ask
          </button>
        </div>
      )}

      {/* Pending questions */}
      {pending.length > 0 && (
        <div className="space-y-1.5 mb-3">
          {pending.map((i) => (
            <div key={i.id} className="flex items-start gap-2 rounded-lg bg-secondary/50 px-3 py-2">
              <button
                onClick={() => handleUpvote(i.id)}
                className={cn(
                  "flex flex-col items-center gap-0.5 shrink-0 mt-0.5 transition-colors",
                  voted.has(i.id) ? "text-primary" : "text-muted-foreground hover:text-foreground"
                )}
              >
                <ArrowUp size={12} />
                <span className="text-[10px] font-semibold">{i.upvotes}</span>
              </button>
              <div className="min-w-0">
                <p className="text-xs text-foreground">{i.content}</p>
                <p className="text-[10px] text-muted-foreground mt-0.5">{i.authorName}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Used questions */}
      {used.length > 0 && (
        <div className="space-y-1">
          <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wide">Answered</p>
          {used.map((i) => (
            <div key={i.id} className="flex items-center gap-2 text-[11px] text-muted-foreground">
              <Check size={10} className="text-emerald-500 shrink-0" />
              <span className="truncate">{i.content}</span>
              {i.usedInTurnNumber && (
                <span className="text-[9px] shrink-0">(Turn {i.usedInTurnNumber})</span>
              )}
            </div>
          ))}
        </div>
      )}

      {interventions.length === 0 && !isLive && (
        <p className="text-xs text-muted-foreground text-center py-2">No crowd questions for this debate.</p>
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
          {debate.source === "breaking" && (
            <span className="flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-bold bg-red-600/10 text-red-600">
              BREAKING NEWS
            </span>
          )}
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
              debateTopic={debate.topic}
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

      <InterventionPanel debateId={debate.id} isLive={isLive} />

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
