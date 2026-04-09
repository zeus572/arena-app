import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, Link } from "react-router-dom";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { fetchDebate, castVote, addReaction, fetchPredictions, makePrediction, fetchInterventions, submitIntervention, upvoteIntervention } from "@/api/client";
import type { DebateDetail, TurnDetail, TurnCitation, PredictionData, InterventionData } from "@/api/types";
import { cn } from "@/lib/utils";
import { getAgentColor, getAgentLabel, BUBBLE_BG, FORMAT_LABELS, type AgentColor } from "@/lib/agent-colors";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { MatchupIntro } from "@/components/matchup-intro";
import { DebateBackdrop, BACKDROP_ACCENTS } from "@/components/debate-backdrop";
import {
  LoveMeter,
  HotSeatHeader,
  computeFireLevel,
  LaughterMeter,
  TweetHeader,
  RapidFireBanner,
  LongformHeader,
  CommonGroundHeader,
  RoastStageHeader,
  getFormatBubbleStyles,
} from "@/components/format-layouts";
import { Button } from "@/components/ui/button";
import { ThumbsUp, ThumbsDown, Lightbulb, ChevronLeft, Trophy, Scale, Target, Check, X, Activity, ChevronDown, ChevronUp, Crosshair, BookOpen, HelpCircle, AlertTriangle, MessageCircleQuestion, ArrowUp, Send, Sparkles, Share2, Copy, Link2, Crown, Mic, Laugh, Flame, TrendingUp, Play, SkipForward, Zap, Heart } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { useSettings, resolveMatchupTheme, type ResolvedMatchupTheme } from "@/lib/use-settings";

/* ─────────────────── StreamingText ─────────────────── */

function StreamingText({ content, onComplete }: { content: string; onComplete: () => void }) {
  const [visibleChars, setVisibleChars] = useState(0);
  const [done, setDone] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval>>(undefined);

  useEffect(() => {
    // Reveal ~15 chars at a time, every 20ms → ~750 chars/sec
    const charsPerTick = 15;
    const tickMs = 20;
    intervalRef.current = setInterval(() => {
      setVisibleChars((prev) => {
        const next = prev + charsPerTick;
        if (next >= content.length) {
          clearInterval(intervalRef.current);
          setDone(true);
          onComplete();
          return content.length;
        }
        return next;
      });
    }, tickMs);
    return () => clearInterval(intervalRef.current);
  }, [content, onComplete]);

  if (done) {
    return <Markdown remarkPlugins={[remarkGfm]}>{content}</Markdown>;
  }

  // Show partial content — find a clean break point (space/newline)
  let end = visibleChars;
  while (end > 0 && end < content.length && content[end] !== ' ' && content[end] !== '\n') {
    end--;
  }
  if (end === 0) end = visibleChars;

  const partial = content.slice(0, end);

  return (
    <>
      <Markdown remarkPlugins={[remarkGfm]}>{partial}</Markdown>
      <span className="inline-block w-2 h-4 bg-foreground/60 animate-pulse rounded-sm ml-0.5 align-text-bottom" />
    </>
  );
}

/* ─────────────────── ReactionRow ─────────────────── */

function ReactionRow({
  turnId,
  debateId,
  targetType,
  counts,
  format,
  show = true,
}: {
  turnId?: string;
  debateId?: string;
  targetType: "debate" | "turn";
  counts: Record<string, number>;
  format?: string;
  show?: boolean;
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

  const buttons: { key: string; label: string; icon: typeof ThumbsUp; activeClass: string }[] = [
    { key: "agree", label: "like", icon: ThumbsUp, activeClass: "bg-primary text-primary-foreground" },
    { key: "disagree", label: "disagree", icon: ThumbsDown, activeClass: "bg-destructive text-white" },
    { key: "insightful", label: "insightful", icon: Lightbulb, activeClass: "bg-accent-foreground text-accent" },
  ];

  if (format === "roast") {
    buttons.push(
      { key: "funny", label: "funny", icon: Laugh, activeClass: "bg-amber-500 text-white" },
      { key: "savage", label: "savage", icon: Flame, activeClass: "bg-red-500 text-white" },
    );
  }
  if (format === "common_ground") {
    buttons.push(
      { key: "surprising", label: "surprising", icon: Sparkles, activeClass: "bg-emerald-500 text-white" },
    );
  }
  if (format === "tweet") {
    buttons.push(
      { key: "ratio", label: "ratio", icon: TrendingUp, activeClass: "bg-sky-500 text-white" },
    );
  }

  return (
    <div
      className={cn(
        "flex items-center gap-1 mt-2 flex-wrap transition-all duration-700",
        show ? "opacity-100 translate-y-0" : "opacity-0 translate-y-4"
      )}
    >
      {buttons.map(({ key, label, icon: Icon, activeClass }) => (
        <button
          key={key}
          onClick={() => vote(label)}
          className={cn(
            "flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-medium transition-all duration-200",
            "hover:scale-110 active:scale-90",
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

/* ─────────────────── TypingIndicator ─────────────────── */

function TypingIndicator({ agentName, agentColor, isLeft }: { agentName: string; agentColor: AgentColor; isLeft: boolean }) {
  return (
    <div className={cn(
      "flex gap-3",
      isLeft ? "flex-row" : "flex-row-reverse",
      "animate-in fade-in zoom-in-95 slide-in-from-bottom-8 duration-500"
    )}>
      <div className="shrink-0 mt-1 animate-pulse">
        <AgentAvatar agent={{ name: agentName, color: agentColor }} size="md" />
      </div>
      <div
        className={cn(
          "rounded-2xl px-5 py-4 relative overflow-hidden",
          isLeft ? "rounded-tl-sm" : "rounded-tr-sm",
          BUBBLE_BG[agentColor]
        )}
      >
        {/* Shimmer effect */}
        <div className="absolute inset-0 -translate-x-full animate-[shimmer_1.5s_infinite] bg-gradient-to-r from-transparent via-white/10 to-transparent" />
        <div className="flex items-center gap-1.5 relative">
          <span className="w-2.5 h-2.5 rounded-full bg-muted-foreground/60 animate-bounce [animation-delay:0ms]" />
          <span className="w-2.5 h-2.5 rounded-full bg-muted-foreground/60 animate-bounce [animation-delay:200ms]" />
          <span className="w-2.5 h-2.5 rounded-full bg-muted-foreground/60 animate-bounce [animation-delay:400ms]" />
        </div>
      </div>
    </div>
  );
}

/* ─────────────────── ContinueButton ─────────────────── */

function ContinueButton({ onClick, turnsLeft }: { onClick: () => void; turnsLeft: number }) {
  return (
    <div className="flex justify-center py-6 animate-in fade-in zoom-in-95 slide-in-from-bottom-8 duration-500">
      <div className="relative group">
        <div className="absolute inset-0 bg-primary/30 rounded-full blur-xl group-hover:blur-2xl transition-all group-hover:scale-110" />
        <Button
          onClick={onClick}
          size="lg"
          className={cn(
            "relative gap-3 px-8 py-6 rounded-full text-base font-semibold",
            "shadow-lg hover:shadow-2xl transition-all duration-300",
            "hover:scale-105 active:scale-95",
            "bg-gradient-to-r from-primary to-primary/80"
          )}
        >
          <Zap size={18} className="animate-pulse" />
          Continue
          <span className="text-xs opacity-80 bg-primary-foreground/20 px-2 py-0.5 rounded-full">
            {turnsLeft} left
          </span>
        </Button>
      </div>
    </div>
  );
}

/* ─────────────────── ArbiterCard ─────────────────── */

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

/* ─────────────────── CommentaryBoothCard ─────────────────── */

function CommentaryBoothCard({ turns }: { turns: TurnDetail[] }) {
  const [open, setOpen] = useState(false);
  if (turns.length === 0) return null;
  return (
    <div className="mb-6">
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 rounded-full border border-sky-500/20 bg-sky-500/5 px-4 py-2 text-[11px] font-semibold text-sky-600 dark:text-sky-400 hover:bg-sky-500/10 transition-colors"
      >
        <Mic size={12} />
        TLDR — Commentary Booth
        {open ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
      </button>
      {open && (
      <div className="rounded-xl border border-sky-500/20 bg-sky-500/5 px-5 py-4 mt-2 animate-in fade-in slide-in-from-top-2 duration-300">
      <div className="space-y-3">
        {turns.map((turn) => (
          <div key={turn.id} className="flex items-start gap-2.5">
            <AgentAvatar
              agent={{ name: turn.agent.name, color: "commentator" as AgentColor }}
              size="sm"
            />
            <div className="min-w-0">
              <span className="text-[10px] font-semibold text-sky-700 dark:text-sky-300">
                {turn.agent.name}
              </span>
              <p className="text-xs text-muted-foreground leading-relaxed mt-0.5">
                {turn.content}
              </p>
            </div>
          </div>
        ))}
      </div>
      </div>
      )}
    </div>
  );
}

/* ─────────────────── ClipButton ─────────────────── */

function ClipButton({ turn, debateTopic }: { turn: TurnDetail; debateTopic: string }) {
  const [showCard, setShowCard] = useState(false);
  const [copied, setCopied] = useState(false);

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
            <div className="flex gap-2 mt-3">
              <button onClick={handleCopyText} className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-card border border-border px-3 py-2 text-xs font-medium text-foreground hover:bg-secondary transition-colors">
                <Copy size={12} />
                {copied ? "Copied!" : "Copy Quote"}
              </button>
              <button onClick={handleCopyLink} className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-card border border-border px-3 py-2 text-xs font-medium text-foreground hover:bg-secondary transition-colors">
                <Link2 size={12} />
                Copy Link
              </button>
              <button onClick={() => setShowCard(false)} className="rounded-lg bg-card border border-border px-3 py-2 text-xs text-muted-foreground hover:bg-secondary transition-colors">
                <X size={12} />
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

/* ─────────────────── ArgumentBreakdown ─────────────────── */

interface ArgumentAnalysis {
  claims: string[];
  evidence: string[];
  assumptions: string[];
  weaknesses: string[];
}

function analyzeArgument(content: string, analysisJson?: string | null): ArgumentAnalysis {
  if (analysisJson) {
    try {
      return JSON.parse(analysisJson) as ArgumentAnalysis;
    } catch { /* fall through to heuristic */ }
  }

  const lines = content.split("\n").filter((l) => l.trim());
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

  const evidence = lines
    .filter((l) => /\[\d+\]/.test(l))
    .map((l) => l.replace(/^[-*•]\s*/, "").trim())
    .filter((l) => l.length > 20)
    .slice(0, 5);

  const assumptionKeywords = ["assuming", "if we", "given that", "presumes", "relies on", "would mean", "should lead", "likely", "presumably"];
  const assumptions = lines
    .filter((l) => { const lower = l.toLowerCase(); return assumptionKeywords.some((kw) => lower.includes(kw)); })
    .map((l) => l.replace(/^[-*•]\s*/, "").replace(/\*\*/g, "").trim())
    .slice(0, 3);

  const hedgeKeywords = ["however", "although", "despite", "admittedly", "granted", "to be fair", "challenge", "limitation", "risk"];
  const weaknesses = lines
    .filter((l) => { const lower = l.toLowerCase(); return hedgeKeywords.some((kw) => lower.includes(kw)); })
    .map((l) => l.replace(/^[-*•]\s*/, "").replace(/\*\*/g, "").trim())
    .slice(0, 3);

  return { claims: claims.slice(0, 5), evidence, assumptions, weaknesses };
}

function ArgumentBreakdown({ turn }: { turn: TurnDetail }) {
  const [open, setOpen] = useState(false);
  const analysis = analyzeArgument(turn.content, turn.analysisJson);
  const hasContent = analysis.claims.length > 0 || analysis.evidence.length > 0 || analysis.assumptions.length > 0 || analysis.weaknesses.length > 0;
  if (!hasContent) return null;

  const sections = [
    { key: "claims", label: "Key Claims", icon: Crosshair, items: analysis.claims, color: "text-blue-500" },
    { key: "evidence", label: "Evidence", icon: BookOpen, items: analysis.evidence, color: "text-emerald-500" },
    { key: "assumptions", label: "Assumptions", icon: HelpCircle, items: analysis.assumptions, color: "text-amber-500" },
    { key: "weaknesses", label: "Potential Weaknesses", icon: AlertTriangle, items: analysis.weaknesses, color: "text-red-400" },
  ].filter((s) => s.items.length > 0);

  return (
    <div className="mt-2">
      <button onClick={() => setOpen(!open)} className="flex items-center gap-1 text-[10px] font-medium text-primary/70 hover:text-primary transition-colors">
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

/* ─────────────────── CollapsibleSources ─────────────────── */

function CollapsibleSources({ citationsJson }: { citationsJson: string }) {
  const [open, setOpen] = useState(false);

  let citations: TurnCitation[];
  try {
    const raw = JSON.parse(citationsJson) as Record<string, string>[];
    citations = raw.map((c) => ({
      source: c.source || c.Source || "",
      title: c.title || c.Title || "",
      url: c.url || c.Url || "",
    }));
  } catch {
    return null;
  }

  if (citations.length === 0) return null;

  return (
    <div className="mt-3 pt-2 border-t border-border/30">
      <button
        onClick={(e) => { e.stopPropagation(); setOpen(!open); }}
        className="flex items-center gap-1 text-[10px] font-semibold text-muted-foreground uppercase tracking-wide hover:text-foreground transition-colors"
      >
        {open ? <ChevronUp size={10} /> : <ChevronDown size={10} />}
        {citations.length} source{citations.length !== 1 ? "s" : ""}
      </button>
      {open && (
        <div className="flex flex-col gap-1 mt-1.5">
          {citations.map((c, i) => (
            <a key={i} href={c.url} target="_blank" rel="noopener noreferrer" className="text-[11px] text-primary hover:underline truncate block" onClick={(e) => e.stopPropagation()}>
              [{c.source}] {c.title}
            </a>
          ))}
        </div>
      )}
    </div>
  );
}

/* ─────────────────── TurnBubble (with animations) ─────────────────── */

function TurnBubble({
  turn,
  isLeft,
  debateId,
  debateTopic,
  agentColor,
  format,
  isNew = false,
  showReactions = true,
}: {
  turn: TurnDetail;
  isLeft: boolean;
  debateId: string;
  debateTopic: string;
  agentColor: AgentColor;
  format?: string;
  isNew?: boolean;
  showReactions?: boolean;
}) {
  const isCompromise = turn.type === "Compromise";
  const isWildcard = turn.type === "Wildcard";
  const bubbleRef = useRef<HTMLDivElement>(null);
  const [hasAnimated, setHasAnimated] = useState(!isNew);
  const [streamingDone, setStreamingDone] = useState(!isNew);
  const handleStreamComplete = useCallback(() => setStreamingDone(true), []);

  useEffect(() => {
    if (isNew && bubbleRef.current) {
      // Only scroll if the new bubble isn't already visible — avoids
      // jerking the page when the user has the bubble in view already.
      bubbleRef.current.scrollIntoView({ behavior: "smooth", block: "nearest" });
      setTimeout(() => setHasAnimated(true), 800);
    }
  }, [isNew]);

  // Stagger a per-bubble float delay so they don't all bob in unison.
  const floatDelay = `${(turn.id.charCodeAt(0) % 5) * 0.4}s`;

  const formatStyles = getFormatBubbleStyles(format);
  // Force alignment for formats that don't alternate
  const effectiveIsLeft =
    formatStyles.alignment === "left" ? true
    : formatStyles.alignment === "right" ? false
    : formatStyles.alignment === "center" ? true
    : isLeft;

  return (
    <div
      ref={bubbleRef}
      className={cn(
        "flex gap-3 transition-all",
        isWildcard ? "flex-row justify-center" : effectiveIsLeft ? "flex-row" : "flex-row-reverse",
        isNew && !hasAnimated ? "animate-in fade-in zoom-in-95 slide-in-from-bottom-12 duration-700" : "",
        formatStyles.rowClass
      )}
      style={{ animationDelay: isNew ? "100ms" : "0ms" }}
    >
      {/* Avatar with glow effect when new */}
      <div className={cn(
        "shrink-0 mt-1 transition-all duration-500 relative",
        isNew && !hasAnimated ? "scale-110" : ""
      )}>
        {isNew && !hasAnimated && (
          <div className={cn(
            "absolute inset-0 rounded-full blur-md animate-pulse",
            "bg-primary/40"
          )} />
        )}
        <AgentAvatar
          agent={{ name: turn.agent.name, color: isWildcard ? "wildcard" as AgentColor : agentColor }}
          size={format === "tweet" || format === "rapid_fire" ? "sm" : "md"}
        />
      </div>
      <div className={cn("min-w-0 flex-1 flex flex-col overflow-hidden", isWildcard ? "items-center" : effectiveIsLeft ? "items-start" : "items-end")}>
        {/* Dramatic entrance label for new turns */}
        {isNew && !hasAnimated && (
          <div className="flex items-center gap-1.5 mb-1.5 text-[10px] font-bold uppercase tracking-wider animate-in fade-in slide-in-from-bottom-2 duration-300">
            <Zap size={10} className="text-primary" />
            <span className="text-primary">{turn.agent.name} responds</span>
          </div>
        )}
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
        {/* Tweet-style header: name + @handle + timestamp */}
        {format === "tweet" && (
          <div className="flex items-center gap-1.5 text-xs mb-1 px-1">
            <span className="font-bold text-foreground">{turn.agent.name}</span>
            <span className="text-muted-foreground">@{turn.agent.name.toLowerCase().replace(/\s+/g, "_")}</span>
            <span className="text-muted-foreground">·</span>
            <span className="text-muted-foreground">
              {new Date(turn.createdAt).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}
            </span>
          </div>
        )}
        {format === "town_hall" && turn.type === "Question" && (
          <span className="flex items-center gap-1 text-[10px] font-bold text-orange-500 uppercase tracking-wider mb-1 px-1">
            <Flame size={10} className="fill-orange-500" /> Question
          </span>
        )}
        {format === "town_hall" && turn.type !== "Question" && turn.type !== "Commentary" && turn.type !== "Wildcard" && turn.type !== "Compromise" && (
          <span className="flex items-center gap-1 text-[10px] font-bold text-amber-500 uppercase tracking-wider mb-1 px-1">
            🪑 Hot Seat Response
          </span>
        )}
        {format === "common_ground" && turn.type === "Agreement" && (
          <span className="flex items-center gap-1 text-[10px] font-bold text-rose-500 uppercase tracking-wider mb-1 px-1">
            <Heart size={10} className="fill-rose-500" /> Agreement
          </span>
        )}
        {format === "roast" && turn.type === "Roast" && (
          <span className="flex items-center gap-1 text-[10px] font-bold text-amber-500 uppercase tracking-wider mb-1 px-1">
            <Mic size={10} /> Roast
          </span>
        )}
        <div
          style={{ animationDelay: floatDelay }}
          className={cn(
            "rounded-2xl px-4 py-3 text-sm leading-relaxed prose prose-sm max-w-full transition-transform duration-300 ease-out relative overflow-hidden break-words [overflow-wrap:anywhere]",
            "prose-p:my-2 prose-ul:my-2 prose-ol:my-2 prose-li:my-0.5",
            "prose-strong:text-inherit prose-em:text-inherit",
            "prose-table:my-3 prose-table:text-xs prose-th:px-3 prose-th:py-1.5 prose-th:text-left prose-th:font-semibold prose-th:border-b prose-th:border-border",
            "prose-td:px-3 prose-td:py-1.5 prose-td:border-b prose-td:border-border/50",
            "prose-thead:bg-secondary/50 prose-tr:border-0",
            "hover:shadow-md hover:brightness-105",
            "animate-[bubble-float_5s_ease-in-out_infinite]",
            isWildcard
              ? "rounded-sm border border-amber-500/30 bg-amber-500/5 text-foreground"
              : isCompromise
                ? "rounded-tl-sm border border-purple-500/20 bg-purple-500/5 text-foreground"
                : effectiveIsLeft
                  ? cn("rounded-tl-sm text-foreground", BUBBLE_BG[agentColor])
                  : cn("rounded-tr-sm text-foreground", BUBBLE_BG[agentColor]),
            // Format-specific override classes (border, bg, padding) — applied last so they win
            formatStyles.bubbleClass,
            isNew && !hasAnimated ? "shadow-xl ring-2 ring-primary/30 ring-offset-2 ring-offset-background" : "shadow-sm",
          )}
        >
          {/* Sparkle effect on new messages */}
          {isNew && !hasAnimated && (
            <Sparkles
              size={14}
              className="absolute top-2 right-2 animate-pulse text-primary/60"
            />
          )}
          {/* Counter-animated wrapper: applies the inverse of the bubble's
              float so the text visually stays still while the shell bobs.
              Same duration / delay / easing → the two animations cancel out. */}
          <div
            style={{ animationDelay: floatDelay }}
            className="animate-[bubble-float-counter_5s_ease-in-out_infinite]"
          >
            {isNew && !streamingDone ? (
              <StreamingText content={turn.content} onComplete={handleStreamComplete} />
            ) : (
              <Markdown remarkPlugins={[remarkGfm]}>{turn.content}</Markdown>
            )}
            {streamingDone && turn.citationsJson && <CollapsibleSources citationsJson={turn.citationsJson} />}
          </div>
        </div>
        <div className={cn("mt-1 px-1 flex items-center gap-2", isLeft ? "" : "flex-row-reverse")}>
          <ReactionRow
            targetType="turn"
            turnId={turn.id}
            debateId={debateId}
            counts={turn.reactions}
            format={format}
            show={showReactions}
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

/* ─────────────────── MomentumMeter ─────────────────── */

function computeMomentum(turns: TurnDetail[], proponentId: string, proponentVotes: number, opponentVotes: number) {
  let momentum = 50;
  const history: { turnNumber: number; momentum: number; agentId: string }[] = [];
  for (const turn of turns) {
    if (turn.type === "Arbiter" || turn.type === "Commentary") continue;
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
  const totalVotes = proponentVotes + opponentVotes;
  if (totalVotes > 0) {
    const voteShift = ((proponentVotes / totalVotes) - 0.5) * 10;
    momentum = Math.max(5, Math.min(95, momentum + voteShift));
  }
  return { current: Math.round(momentum), history };
}

function MomentumMeter({ debate, proponentColor, opponentColor }: { debate: DebateDetail; proponentColor: AgentColor; opponentColor: AgentColor }) {
  const argumentTurns = debate.turns.filter((t) => t.type !== "Arbiter" && t.type !== "Commentary");
  if (argumentTurns.length < 2) return null;
  const { current } = computeMomentum(debate.turns, debate.proponent.id, debate.proponentVotes, debate.opponentVotes);
  const proLeading = current > 55;
  const oppLeading = current < 45;
  const label = proLeading ? `${debate.proponent.name} has momentum` : oppLeading ? `${debate.opponent.name} has momentum` : "Evenly matched";

  return (
    <div className="rounded-xl border border-border bg-card p-4 mb-6">
      <div className="flex items-center gap-2 mb-3">
        <Activity size={14} className="text-blue-500" />
        <h3 className="text-sm font-semibold text-card-foreground">Momentum</h3>
        <span className="text-[10px] text-muted-foreground ml-auto">{label}</span>
      </div>
      <div className="relative">
        <div className="h-3 w-full rounded-full bg-secondary overflow-hidden flex">
          <div className={cn("h-full transition-all duration-700 ease-out", `bg-${proponentColor}`)} style={{ width: `${current}%` }} />
          <div className={cn("h-full transition-all duration-700 ease-out", `bg-${opponentColor}`)} style={{ width: `${100 - current}%` }} />
        </div>
        <div className="absolute top-0 left-1/2 -translate-x-px h-3 w-0.5 bg-foreground/30" />
        <div className="absolute -top-0.5 h-4 w-4 rounded-full border-2 border-background bg-foreground shadow transition-all duration-700 ease-out" style={{ left: `${current}%`, transform: "translateX(-50%)" }} />
      </div>
      <div className="flex justify-between mt-2">
        <span className="text-[10px] text-muted-foreground">{debate.proponent.name}</span>
        <span className="text-[10px] font-mono text-muted-foreground">{current}–{100 - current}</span>
        <span className="text-[10px] text-muted-foreground">{debate.opponent.name}</span>
      </div>
    </div>
  );
}

/* ─────────────────── PredictionWidget ─────────────────── */

function PredictionWidget({ debateId, debate, proponentColor, opponentColor }: { debateId: string; debate: DebateDetail; proponentColor: AgentColor; opponentColor: AgentColor }) {
  const [prediction, setPrediction] = useState<PredictionData | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => { fetchPredictions(debateId).then(setPrediction).catch(() => {}); }, [debateId]);

  const handlePredict = async (agentId: string) => {
    if (submitting) return;
    setSubmitting(true);
    try { await makePrediction(debateId, agentId); const updated = await fetchPredictions(debateId); setPrediction(updated); } catch {}
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
        <h3 className="text-sm font-semibold text-card-foreground">{isCompleted ? "Prediction Results" : "Who Will Win?"}</h3>
        {total > 0 && <span className="text-[10px] text-muted-foreground ml-auto">{total} prediction{total !== 1 ? "s" : ""}</span>}
      </div>
      {total > 0 && (
        <div className="mb-3">
          <div className="h-2.5 w-full rounded-full bg-secondary overflow-hidden flex">
            <div className={cn("h-full transition-all duration-500", `bg-${proponentColor}`)} style={{ width: `${proOdds}%` }} />
            <div className={cn("h-full transition-all duration-500", `bg-${opponentColor}`)} style={{ width: `${oppOdds}%` }} />
          </div>
          <div className="flex justify-between mt-1">
            <span className="text-[11px] text-muted-foreground">{debate.proponent.name} {proOdds.toFixed(0)}%</span>
            <span className="text-[11px] text-muted-foreground">{oppOdds.toFixed(0)}% {debate.opponent.name}</span>
          </div>
        </div>
      )}
      {isCompleted && hasPredicted && (
        <div className={cn("rounded-lg px-3 py-2 text-xs font-medium flex items-center gap-2", prediction.userIsCorrect ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" : "bg-red-500/10 text-red-500")}>
          {prediction.userIsCorrect ? (<><Check size={14} />You predicted correctly!</>) : (<><X size={14} />Your prediction was wrong.</>)}
        </div>
      )}
      {!isCompleted && !hasPredicted && (
        <div className="flex gap-2">
          <button onClick={() => handlePredict(debate.proponent.id)} disabled={submitting} className="flex-1 flex items-center justify-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium transition-colors hover:border-primary/40 hover:bg-primary/5 disabled:opacity-50">
            <AgentAvatar agent={{ name: debate.proponent.name, color: proponentColor }} size="sm" /> {debate.proponent.name}
          </button>
          <button onClick={() => handlePredict(debate.opponent.id)} disabled={submitting} className="flex-1 flex items-center justify-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium transition-colors hover:border-primary/40 hover:bg-primary/5 disabled:opacity-50">
            <AgentAvatar agent={{ name: debate.opponent.name, color: opponentColor }} size="sm" /> {debate.opponent.name}
          </button>
        </div>
      )}
      {!isCompleted && hasPredicted && (
        <p className="text-xs text-muted-foreground">You predicted <span className="font-semibold text-foreground">{prediction.userPredictedAgentId === debate.proponent.id ? debate.proponent.name : debate.opponent.name}</span> will win.</p>
      )}
    </div>
  );
}

/* ─────────────────── InterventionPanel ─────────────────── */

function InterventionPanel({ debateId, isLive }: { debateId: string; isLive: boolean }) {
  const { isAuthenticated, isPremium } = useAuth();
  const [interventions, setInterventions] = useState<InterventionData[]>([]);
  const [question, setQuestion] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [voted, setVoted] = useState<Set<string>>(new Set());

  useEffect(() => { fetchInterventions(debateId).then(setInterventions).catch(() => {}); }, [debateId]);

  const handleSubmit = async () => {
    if (!question.trim() || submitting) return;
    setSubmitting(true);
    try { await submitIntervention(debateId, question.trim()); setQuestion(""); const updated = await fetchInterventions(debateId); setInterventions(updated); } catch {}
    setSubmitting(false);
  };

  const handleUpvote = async (id: string) => {
    if (voted.has(id)) return;
    setVoted((prev) => new Set(prev).add(id));
    setInterventions((prev) => prev.map((i) => (i.id === id ? { ...i, upvotes: i.upvotes + 1 } : i)));
    try { await upvoteIntervention(debateId, id); } catch {
      setVoted((prev) => { const s = new Set(prev); s.delete(id); return s; });
      setInterventions((prev) => prev.map((i) => (i.id === id ? { ...i, upvotes: i.upvotes - 1 } : i)));
    }
  };

  const pending = interventions.filter((i) => !i.used);
  const used = interventions.filter((i) => i.used);

  return (
    <div className="rounded-xl border border-border bg-card p-4 mb-6">
      <div className="flex items-center gap-2 mb-3">
        <MessageCircleQuestion size={14} className="text-indigo-500" />
        <h3 className="text-sm font-semibold text-card-foreground">Crowd Questions</h3>
        <span className="text-[10px] text-muted-foreground ml-auto">Top-voted questions get injected into the next turn</span>
      </div>
      {isLive && isPremium && (
        <div className="flex gap-2 mb-3">
          <input type="text" value={question} onChange={(e) => setQuestion(e.target.value)} placeholder="Ask the agents a question..." maxLength={280}
            className="flex-1 rounded-lg border border-border bg-background px-3 py-1.5 text-xs placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary"
            onKeyDown={(e) => e.key === "Enter" && handleSubmit()} />
          <button onClick={handleSubmit} disabled={submitting || question.trim().length < 10} className="rounded-lg bg-primary px-3 py-1.5 text-xs font-medium text-primary-foreground disabled:opacity-50 flex items-center gap-1">
            <Send size={11} /> Ask
          </button>
        </div>
      )}
      {isLive && !isPremium && (
        <div className="flex items-center gap-2 rounded-lg bg-amber-500/5 border border-amber-500/20 px-3 py-2 mb-3">
          <Crown size={12} className="text-amber-500 shrink-0" />
          <p className="text-[11px] text-muted-foreground"><span className="font-semibold text-amber-600 dark:text-amber-400">Premium</span> required to submit crowd questions.</p>
        </div>
      )}
      {pending.length > 0 && (
        <div className="space-y-1.5 mb-3">
          {pending.map((i) => (
            <div key={i.id} className="flex items-start gap-2 rounded-lg bg-secondary/50 px-3 py-2">
              <button onClick={() => handleUpvote(i.id)} disabled={!isAuthenticated} title={!isAuthenticated ? "Log in to upvote" : undefined}
                className={cn("flex flex-col items-center gap-0.5 shrink-0 mt-0.5 transition-colors", !isAuthenticated ? "text-muted-foreground/40 cursor-not-allowed" : voted.has(i.id) ? "text-primary" : "text-muted-foreground hover:text-foreground")}>
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
      {used.length > 0 && (
        <div className="space-y-1">
          <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wide">Answered</p>
          {used.map((i) => (
            <div key={i.id} className="flex items-center gap-2 text-[11px] text-muted-foreground">
              <Check size={10} className="text-emerald-500 shrink-0" />
              <span className="truncate">{i.content}</span>
              {i.usedInTurnNumber && <span className="text-[9px] shrink-0">(Turn {i.usedInTurnNumber})</span>}
            </div>
          ))}
        </div>
      )}
      {interventions.length === 0 && !isLive && <p className="text-xs text-muted-foreground text-center py-2">No crowd questions for this debate.</p>}
    </div>
  );
}

/* ─────────────────── Main DebateViewPage ─────────────────── */

export default function DebateViewPage() {
  const { id } = useParams<{ id: string }>();
  const [debate, setDebate] = useState<DebateDetail | null>(null);
  const [userVote, setUserVote] = useState<string | null>(null);
  const [settings] = useSettings();
  // Resolve the theme once per debate visit so "random" picks one and stays stable
  // until the user navigates to another debate.
  const [effectiveTheme, setEffectiveTheme] = useState<ResolvedMatchupTheme>(() =>
    resolveMatchupTheme(settings.matchupTheme)
  );
  const [introDone, setIntroDone] = useState(() => effectiveTheme === "off");

  // Reset (and re-roll random) whenever the debate id or saved theme changes
  useEffect(() => {
    const next = resolveMatchupTheme(settings.matchupTheme);
    setEffectiveTheme(next);
    setIntroDone(next === "off");
  }, [id, settings.matchupTheme]);

  // Progressive reveal state
  const [visibleTurns, setVisibleTurns] = useState(0);
  const [isTyping, setIsTyping] = useState(false);
  const [reactionsVisible, setReactionsVisible] = useState<boolean[]>([]);
  const [revealPhase, setRevealPhase] = useState<"loading" | "idle" | "revealing" | "done">("loading");
  const prevTurnCountRef = useRef(0);

  const loadDebate = useCallback(async () => {
    if (!id) return;
    const data = await fetchDebate(id);
    setDebate(data);
  }, [id]);

  useEffect(() => { loadDebate(); }, [loadDebate]);

  useEffect(() => {
    if (id && localStorage.getItem(`vote-${id}`)) {
      setUserVote(localStorage.getItem(`vote-${id}`));
    }
  }, [id]);

  // Determine reveal mode once debate loads — always progressive reveal
  useEffect(() => {
    if (!debate || !id || revealPhase !== "loading") return;
    setVisibleTurns(0);
    setReactionsVisible([]);
    setRevealPhase("idle");
  }, [debate, id, revealPhase]);

  // Live debate: when new turns arrive AFTER reveal is done, animate them in
  useEffect(() => {
    if (!debate || revealPhase !== "done") return;
    const isLive = debate.status === "Active" || debate.status === "Compromising";
    if (!isLive) return;

    const nonCommentary = debate.turns.filter((t) => t.type !== "Commentary");
    const newCount = nonCommentary.length;
    if (newCount > prevTurnCountRef.current) {
      // New turns arrived via polling — animate them
      setVisibleTurns(newCount);
      setReactionsVisible((prev) => {
        const next = [...prev];
        for (let i = prevTurnCountRef.current; i < newCount; i++) {
          next[i] = false;
        }
        return next;
      });
      setTimeout(() => {
        setReactionsVisible(nonCommentary.map(() => true));
      }, 800);
    }
    prevTurnCountRef.current = newCount;
  }, [debate, revealPhase]);

  // Real-time polling for live debates
  useEffect(() => {
    if (!debate || (debate.status !== "Active" && debate.status !== "Compromising")) return;
    const interval = setInterval(loadDebate, 10_000);
    return () => clearInterval(interval);
  }, [debate?.status, loadDebate]);

  const startReveal = () => {
    setRevealPhase("revealing");
    showNextTurn();
  };

  const showNextTurn = () => {
    if (isTyping || !debate) return;
    const nonCommentary = debate.turns.filter((t) => t.type !== "Commentary");
    if (visibleTurns >= nonCommentary.length) {
      finishReveal();
      return;
    }

    setIsTyping(true);

    setTimeout(() => {
      setIsTyping(false);
      setVisibleTurns((v) => v + 1);
      // Show reactions after bubble appears
      setTimeout(() => {
        setReactionsVisible((prev) => {
          const next = [...prev];
          next[visibleTurns] = true;
          return next;
        });
      }, 600);
    }, 1200);
  };

  const skipToEnd = () => {
    if (!debate || !id) return;
    const nonCommentary = debate.turns.filter((t) => t.type !== "Commentary");
    setIsTyping(false);
    setVisibleTurns(nonCommentary.length);
    setReactionsVisible(nonCommentary.map(() => true));
    finishReveal();
  };

  const finishReveal = () => {
    setRevealPhase("done");
  };

  const handleContinue = () => {
    if (!debate) return;
    const nonCommentary = debate.turns.filter((t) => t.type !== "Commentary");
    if (visibleTurns >= nonCommentary.length) {
      finishReveal();
    } else {
      showNextTurn();
    }
  };

  if (!debate) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8">
        <div className="rounded-xl border border-border bg-card p-6 h-64 animate-pulse" />
      </main>
    );
  }

  const proponentColor = getAgentColor(debate.proponent.persona ?? "", debate.proponent.agentType);
  const opponentColor = getAgentColor(debate.opponent.persona ?? "", debate.opponent.agentType);
  const proponentLabel = getAgentLabel(debate.proponent.persona ?? "", debate.proponent.agentType);
  const opponentLabel = getAgentLabel(debate.opponent.persona ?? "", debate.opponent.agentType);

  const totalVotes = debate.proponentVotes + debate.opponentVotes;
  const pctA = totalVotes > 0 ? (debate.proponentVotes / totalVotes) * 100 : 50;
  const winner = debate.status === "Completed"
    ? debate.proponentVotes > debate.opponentVotes ? debate.proponent : debate.opponent
    : null;

  const handleVote = async (agentId: string) => {
    if (!id || userVote) return;
    try {
      await castVote(id, agentId);
      localStorage.setItem(`vote-${id}`, agentId);
      setUserVote(agentId);
      await loadDebate();
    } catch {}
  };

  const isLive = debate.status === "Active" || debate.status === "Compromising";
  const nonCommentaryTurns = debate.turns.filter((t) => t.type !== "Commentary");
  const isRevealing = revealPhase === "revealing";
  const isComplete = visibleTurns >= nonCommentaryTurns.length;

  // Determine which agent is currently "active" for header highlight
  const currentTurnData = !isComplete && nonCommentaryTurns[visibleTurns];
  const activeAgentId = isRevealing && isTyping && currentTurnData ? currentTurnData.agentId : null;

  const showIntro = !introDone && effectiveTheme !== "off";
  const themed = effectiveTheme !== "off";
  // After Watch Debate is clicked, the top card collapses + fades into the
  // background so the debate text visually overlays on top.
  const isWatching = revealPhase === "revealing" || revealPhase === "done";

  return (
    <>
      <DebateBackdrop theme={effectiveTheme} />
      {showIntro && (
        <MatchupIntro
          theme={effectiveTheme}
          proponent={{
            id: debate.proponent.id,
            name: debate.proponent.name,
            label: proponentLabel,
            color: proponentColor,
          }}
          opponent={{
            id: debate.opponent.id,
            name: debate.opponent.name,
            label: opponentLabel,
            color: opponentColor,
          }}
          topic={debate.topic}
          onComplete={() => setIntroDone(true)}
        />
      )}
    <main className={cn("mx-auto max-w-3xl px-4 py-8", themed && "relative")}>
      <Link to="/">
        <Button variant="ghost" size="sm" className="mb-5 gap-1.5 text-xs text-muted-foreground -ml-2">
          <ChevronLeft size={14} />
          Back to Feed
        </Button>
      </Link>

      {/* Header card — wrapped in a collapsing container so it can fade
          into the background once Watch Debate has been clicked. The wrapper
          shrinks its in-flow height while the inner card content overflows
          visually with reduced opacity, letting the debate text overlay on top. */}
      <div
        className={cn(
          "relative overflow-visible transition-[max-height,margin-bottom] duration-700 ease-out",
          isWatching ? "max-h-[72px] mb-2" : "max-h-[800px] mb-5"
        )}
        aria-hidden={isWatching || undefined}
      >
      <div className={cn(
        "rounded-xl border border-border bg-card p-4 shadow-sm transition-all duration-700 ease-out",
        themed && effectiveTheme !== "off" && `ring-1 ${BACKDROP_ACCENTS[effectiveTheme]}`,
        isRevealing && !themed ? "ring-1 ring-primary/20" : "",
        isWatching && "opacity-[0.18] scale-[0.97] blur-[1.5px] pointer-events-none origin-top"
      )}>
        <div className="flex items-center justify-between mb-3 flex-wrap gap-2">
          <span className={cn(
            "flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold",
            debate.status === "Active" ? "bg-red-500/10 text-red-500"
              : debate.status === "Compromising" ? "bg-amber-500/10 text-amber-600"
              : "bg-secondary text-muted-foreground"
          )}>
            {debate.status === "Active" ? (
              <><span className="h-1.5 w-1.5 rounded-full bg-red-500 animate-pulse" />LIVE</>
            ) : debate.status === "Compromising" ? (
              <><span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" /><Scale size={12} />FINDING COMPROMISE</>
            ) : (debate.status)}
          </span>
          {debate.format && debate.format !== "standard" && (() => {
            const fl = FORMAT_LABELS[debate.format];
            return fl ? <span className={cn("flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-bold", fl.color)}>{fl.label}</span> : null;
          })()}
          {debate.source === "breaking" && (
            <span className="flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-bold bg-red-600/10 text-red-600">BREAKING NEWS</span>
          )}
          <span className="text-[11px] text-muted-foreground">
            {new Date(debate.createdAt).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" })}
          </span>
        </div>

        {debate.newsInfo && (
          <div className="flex items-start gap-2.5 rounded-lg bg-blue-500/5 border border-blue-500/20 px-3 py-2.5 mb-4">
            <BookOpen size={14} className="text-blue-500 shrink-0 mt-0.5" />
            <div className="min-w-0">
              <p className="text-xs font-medium text-blue-600 dark:text-blue-400">{debate.newsInfo.headline}</p>
              <p className="text-[10px] text-muted-foreground mt-0.5">{debate.newsInfo.source} &middot; {new Date(debate.newsInfo.publishedAt).toLocaleDateString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}</p>
            </div>
          </div>
        )}

        <h1 className="text-base font-bold text-card-foreground leading-snug mb-3 text-balance">{debate.topic}</h1>
        {debate.description && <p className="text-xs text-muted-foreground mb-3">{debate.description}</p>}

        {/* Agent cards with active speaker highlight */}
        <div className="flex items-center justify-between gap-2">
          <div className={cn(
            "flex items-center gap-2.5 flex-1 min-w-0 p-2 rounded-lg transition-all duration-500",
            activeAgentId === debate.proponent.id ? "bg-primary/5 shadow-md ring-1 ring-primary/30" : ""
          )}>
            <div className="relative shrink-0">
              <AgentAvatar agent={{ name: debate.proponent.name, color: proponentColor }} size="md" />
              {activeAgentId === debate.proponent.id && (
                <span className="absolute -bottom-0.5 -right-0.5 w-3 h-3 rounded-full bg-primary flex items-center justify-center shadow">
                  <span className="w-1.5 h-1.5 rounded-full bg-white animate-ping" />
                </span>
              )}
            </div>
            <div className="min-w-0 flex-1">
              <p className="font-semibold text-xs text-card-foreground truncate">{debate.proponent.name}</p>
              <IdeologyBadge label={proponentLabel} color={proponentColor} />
              {(debate.proponent.agentType === "celebrity" || debate.proponent.agentType === "historical") && (
                <p className="text-[9px] text-muted-foreground/70 italic mt-0.5 truncate" title="AI simulation based on public record">AI simulation</p>
              )}
            </div>
          </div>
          <span className={cn("text-sm font-black tracking-wider transition-all duration-500 shrink-0 px-1", isRevealing ? "text-primary" : "text-muted-foreground/40")}>VS</span>
          <div className={cn(
            "flex items-center gap-2.5 flex-1 min-w-0 p-2 rounded-lg transition-all duration-500 flex-row-reverse text-right",
            activeAgentId === debate.opponent.id ? "bg-primary/5 shadow-md ring-1 ring-primary/30" : ""
          )}>
            <div className="relative shrink-0">
              <AgentAvatar agent={{ name: debate.opponent.name, color: opponentColor }} size="md" />
              {activeAgentId === debate.opponent.id && (
                <span className="absolute -bottom-0.5 -right-0.5 w-3 h-3 rounded-full bg-primary flex items-center justify-center shadow">
                  <span className="w-1.5 h-1.5 rounded-full bg-white animate-ping" />
                </span>
              )}
            </div>
            <div className="min-w-0 flex-1">
              <p className="font-semibold text-xs text-card-foreground truncate">{debate.opponent.name}</p>
              <IdeologyBadge label={opponentLabel} color={opponentColor} />
              {(debate.opponent.agentType === "celebrity" || debate.opponent.agentType === "historical") && (
                <p className="text-[9px] text-muted-foreground/70 italic mt-0.5 truncate" title="AI simulation based on public record">AI simulation</p>
              )}
            </div>
          </div>
        </div>

        {/* Progress bar (during reveal or live) */}
        {(isRevealing || (isLive && nonCommentaryTurns.length > 0)) && (
          <div className="mt-6 pt-4 border-t border-border animate-in fade-in slide-in-from-bottom-4 duration-500">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs text-muted-foreground font-medium">
                Round {Math.min(visibleTurns + 1, nonCommentaryTurns.length)} of {nonCommentaryTurns.length}
              </span>
              {isRevealing && !isComplete && (
                <Button variant="ghost" size="sm" onClick={skipToEnd} className="text-xs text-muted-foreground hover:text-foreground gap-1">
                  <SkipForward size={12} /> Skip to end
                </Button>
              )}
            </div>
            <div className="h-2 w-full rounded-full bg-secondary overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-primary/80 via-primary to-primary/60 transition-all duration-700 ease-out"
                style={{ width: `${(visibleTurns / nonCommentaryTurns.length) * 100}%` }}
              />
            </div>
          </div>
        )}
      </div>
      </div>

      <div className="relative z-10">
      {/* Format-specific banners / meters — show as soon as the user has
          clicked Watch Debate so each format reads visually distinct.
          Standard format gets none. */}
      {isWatching && debate.format && debate.format !== "standard" && (
        <div className="animate-in fade-in slide-in-from-top-4 duration-500">
          {debate.format === "town_hall" && (
            <HotSeatHeader
              respondent={debate.proponent}
              questioner={debate.opponent}
              respondentColor={proponentColor}
              questionerColor={opponentColor}
              fireLevel={computeFireLevel(nonCommentaryTurns.slice(0, visibleTurns))}
            />
          )}
          {debate.format === "common_ground" && (
            <>
              <CommonGroundHeader
                proponent={debate.proponent}
                opponent={debate.opponent}
                proponentColor={proponentColor}
                opponentColor={opponentColor}
              />
              <LoveMeter turns={nonCommentaryTurns.slice(0, visibleTurns)} totalTurns={nonCommentaryTurns.length} />
            </>
          )}
          {debate.format === "roast" && (
            <>
              <RoastStageHeader proponent={debate.proponent} opponent={debate.opponent} />
              <LaughterMeter turns={nonCommentaryTurns.slice(0, visibleTurns)} />
            </>
          )}
          {debate.format === "tweet" && (
            <TweetHeader currentTurn={visibleTurns} totalTurns={nonCommentaryTurns.length} />
          )}
          {debate.format === "rapid_fire" && (
            <RapidFireBanner currentTurn={visibleTurns} totalTurns={nonCommentaryTurns.length} />
          )}
          {debate.format === "longform" && (
            <LongformHeader proponentName={debate.proponent.name} opponentName={debate.opponent.name} />
          )}
        </div>
      )}

      {/* Widgets shown after reveal is done or in live mode */}
      {(revealPhase === "done" || isLive) && (
        <>
          <MomentumMeter debate={debate} proponentColor={proponentColor} opponentColor={opponentColor} />
          <PredictionWidget debateId={debate.id} debate={debate} proponentColor={proponentColor} opponentColor={opponentColor} />
        </>
      )}

      <CommentaryBoothCard turns={debate.turns.filter((t) => t.type === "Commentary")} />

      {/* Start button for progressive reveal */}
      {revealPhase === "idle" && (
        <div className="flex flex-col items-center justify-center py-20 animate-in fade-in zoom-in-95 duration-700">
          <div className="relative group cursor-pointer" onClick={startReveal}>
            <div className="absolute inset-0 bg-primary/20 rounded-full blur-3xl group-hover:blur-[40px] transition-all group-hover:scale-125 animate-pulse" />
            <Button
              size="lg"
              className={cn(
                "relative gap-4 text-lg px-10 py-8 rounded-full",
                "shadow-2xl hover:shadow-primary/30 transition-all duration-300",
                "hover:scale-110 active:scale-95",
                "bg-gradient-to-r from-primary to-primary/80"
              )}
            >
              <Play size={24} className="fill-current" />
              Watch Debate
            </Button>
          </div>
          <p className="text-sm text-muted-foreground mt-6 animate-pulse">Click to begin the showdown</p>
        </div>
      )}

      {/* Debate turns (progressive reveal or full) */}
      {(revealPhase === "revealing" || revealPhase === "done") && (
        <section
          className={cn(
            "flex flex-col mb-8 overflow-x-hidden",
            // Per-format gap & padding so the layouts feel different
            debate.format === "tweet" ? "gap-2" :
            debate.format === "rapid_fire" ? "gap-3" :
            debate.format === "longform" ? "gap-10" :
            "gap-6"
          )}
          aria-label="Debate turns"
        >
          {nonCommentaryTurns.slice(0, visibleTurns).map((turn, i) => {
            if (turn.type === "Arbiter") {
              return <ArbiterCard key={turn.id} turn={turn} />;
            }
            const isA = turn.agentId === debate.proponent.id;
            const isNewTurn = isRevealing ? i === visibleTurns - 1 : i >= prevTurnCountRef.current - 1 && i === nonCommentaryTurns.length - 1;
            return (
              <TurnBubble
                key={turn.id}
                turn={turn}
                isLeft={isA}
                debateId={debate.id}
                debateTopic={debate.topic}
                agentColor={isA ? proponentColor : opponentColor}
                format={debate.format}
                isNew={isNewTurn}
                showReactions={reactionsVisible[i] ?? true}
              />
            );
          })}

          {/* Typing indicator */}
          {isTyping && currentTurnData && (
            <TypingIndicator
              agentName={currentTurnData.agentId === debate.proponent.id ? debate.proponent.name : debate.opponent.name}
              agentColor={currentTurnData.agentId === debate.proponent.id ? proponentColor : opponentColor}
              isLeft={currentTurnData.agentId === debate.proponent.id}
            />
          )}

          {/* Continue button */}
          {isRevealing && !isTyping && !isComplete && visibleTurns > 0 && (
            <ContinueButton onClick={handleContinue} turnsLeft={nonCommentaryTurns.length - visibleTurns} />
          )}

          {isLive && isComplete && (
            <p className="text-center text-xs text-muted-foreground italic py-4">
              {debate.status === "Compromising" ? "Agents are negotiating a compromise..."
                : debate.turns.length === 0 ? "Waiting for the first argument..."
                : "Waiting for next turn..."}
            </p>
          )}
        </section>
      )}

      {/* Intervention panel */}
      {(revealPhase === "done" || isLive) && (
        <InterventionPanel debateId={debate.id} isLive={isLive} />
      )}

      {/* Vote / Winner section — enhanced with v0 styling */}
      {(revealPhase === "done" || (isRevealing && isComplete)) && (
        <div className={cn(
          "rounded-xl border border-border bg-card p-6",
          isRevealing && isComplete ? "animate-in fade-in zoom-in-95 slide-in-from-bottom-8 duration-700 shadow-lg ring-1 ring-primary/10" : ""
        )}>
          <h2 className="text-base font-bold text-card-foreground mb-5 flex items-center gap-2">
            {winner ? (
              <>
                <div className="relative">
                  <Trophy size={20} className="text-amber-500" />
                  <Sparkles size={12} className="absolute -top-1 -right-1 text-amber-400 animate-pulse" />
                </div>
                <span className="bg-gradient-to-r from-amber-500 to-amber-600 bg-clip-text text-transparent">
                  Winner: {winner.name}
                </span>
              </>
            ) : (
              <><Zap size={18} className="text-primary" /> Cast Your Vote</>
            )}
          </h2>

          <div className="mb-5">
            <div className="h-4 w-full rounded-full bg-secondary overflow-hidden flex shadow-inner">
              <div className={cn("h-full transition-all duration-1000 ease-out relative", `bg-${proponentColor}`)} style={{ width: `${pctA}%` }}>
                <div className="absolute inset-0 bg-gradient-to-t from-black/10 to-white/10" />
              </div>
              <div className={cn("h-full transition-all duration-1000 ease-out relative", `bg-${opponentColor}`)} style={{ width: `${100 - pctA}%` }}>
                <div className="absolute inset-0 bg-gradient-to-t from-black/10 to-white/10" />
              </div>
            </div>
            <div className="flex justify-between mt-3">
              <span className="text-xs font-semibold flex items-center gap-2">
                <span className={cn("w-2 h-2 rounded-full", `bg-${proponentColor}`)} />
                {debate.proponent.name} — {pctA.toFixed(0)}%
              </span>
              <span className="text-xs font-semibold flex items-center gap-2">
                {(100 - pctA).toFixed(0)}% — {debate.opponent.name}
                <span className={cn("w-2 h-2 rounded-full", `bg-${opponentColor}`)} />
              </span>
            </div>
          </div>

          {!userVote ? (
            <div className="flex gap-4 flex-wrap">
              <Button
                size="lg"
                variant="outline"
                className={cn(
                  "flex-1 gap-3 text-sm py-6 transition-all duration-300",
                  "border-2 hover:scale-105 active:scale-95",
                  "hover:shadow-lg"
                )}
                onClick={() => handleVote(debate.proponent.id)}
              >
                <AgentAvatar agent={{ name: debate.proponent.name, color: proponentColor }} size="sm" />
                <span>{debate.proponent.name} Won</span>
              </Button>
              <Button
                size="lg"
                variant="outline"
                className={cn(
                  "flex-1 gap-3 text-sm py-6 transition-all duration-300",
                  "border-2 hover:scale-105 active:scale-95",
                  "hover:shadow-lg"
                )}
                onClick={() => handleVote(debate.opponent.id)}
              >
                <AgentAvatar agent={{ name: debate.opponent.name, color: opponentColor }} size="sm" />
                <span>{debate.opponent.name} Won</span>
              </Button>
            </div>
          ) : (
            <div className="flex items-center gap-3 p-4 rounded-lg bg-secondary/50 animate-in fade-in zoom-in-95 duration-500">
              <Sparkles size={16} className="text-primary" />
              <p className="text-sm text-muted-foreground">
                You voted for{" "}
                <span className="font-bold text-foreground">
                  {userVote === debate.proponent.id ? debate.proponent.name : debate.opponent.name}
                </span>
                . Thanks for participating!
              </p>
            </div>
          )}
        </div>
      )}
      </div>
    </main>
    </>
  );
}
