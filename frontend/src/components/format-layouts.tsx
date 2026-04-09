import { Heart, Flame, Mic, Zap, Crown, Feather } from "lucide-react";
import { cn } from "@/lib/utils";
import { AgentAvatar } from "@/components/agent-avatar";
import type { TurnDetail, Agent } from "@/api/types";
import type { AgentColor } from "@/lib/agent-colors";

/* ─────────────────────────────────────────────────────────────
   Format-aware helpers — meters, headers, container modifiers
   ───────────────────────────────────────────────────────────── */

/** Sum a single reaction key across all turns. */
function sumReactionKey(turns: TurnDetail[], key: string): number {
  return turns.reduce((acc, t) => acc + (t.reactions?.[key] ?? 0), 0);
}

/** Count turns of a given type. */
function countTurnsOfType(turns: TurnDetail[], type: string): number {
  return turns.filter((t) => t.type === type).length;
}

/* ─────────────────── Common Ground — Love Meter ─────────────────── */

export function LoveMeter({ turns, totalTurns }: { turns: TurnDetail[]; totalTurns: number }) {
  const agreementTurns = countTurnsOfType(turns, "Agreement");
  const positiveReactions = sumReactionKey(turns, "like") + sumReactionKey(turns, "insightful");
  // Fill: 60% from agreement turns, 40% from positive reactions (capped)
  const turnPct = totalTurns > 0 ? (agreementTurns / Math.max(totalTurns, 1)) * 60 : 0;
  const reactionPct = Math.min(40, positiveReactions * 4);
  const fillPct = Math.min(100, turnPct + reactionPct);

  return (
    <div className="relative rounded-2xl border border-rose-300/40 bg-gradient-to-br from-rose-100/40 via-pink-50/40 to-fuchsia-100/40 dark:from-rose-950/30 dark:via-pink-950/20 dark:to-fuchsia-950/30 p-4 mb-5 overflow-hidden">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <Heart size={16} className="text-rose-500 fill-rose-500" />
          <span className="text-xs font-bold uppercase tracking-wider text-rose-600 dark:text-rose-300">
            Love Meter
          </span>
        </div>
        <span className="text-[10px] font-mono text-rose-600/70 dark:text-rose-300/70">
          {agreementTurns} agreement{agreementTurns === 1 ? "" : "s"} · {positiveReactions} ♥
        </span>
      </div>
      <div className="relative h-3 rounded-full bg-rose-200/40 dark:bg-rose-950/50 overflow-hidden">
        <div
          className="absolute inset-y-0 left-0 bg-gradient-to-r from-rose-400 via-pink-500 to-fuchsia-500 transition-all duration-1000 ease-out"
          style={{ width: `${fillPct}%` }}
        />
        {/* Floating hearts overlay */}
        <div className="absolute inset-0 flex items-center pl-1 gap-1 pointer-events-none">
          {Array.from({ length: Math.min(5, Math.floor(fillPct / 20)) }).map((_, i) => (
            <Heart
              key={i}
              size={10}
              className="fill-white text-white drop-shadow-sm animate-pulse"
              style={{ animationDelay: `${i * 200}ms` }}
            />
          ))}
        </div>
      </div>
      {fillPct >= 95 && (
        <p className="mt-2 text-center text-xs font-bold text-rose-600 dark:text-rose-300 animate-pulse">
          🤝 Common Ground Reached
        </p>
      )}
    </div>
  );
}

/* ─────────────────── Town Hall — Hot Seat + Fire Meter ─────────────────── */

export function HotSeatHeader({
  respondent,
  questioner,
  respondentColor,
  questionerColor,
  fireLevel,
}: {
  respondent: Agent;
  questioner: Agent;
  respondentColor: AgentColor;
  questionerColor: AgentColor;
  /** 0–100, drives flame intensity around hot seat */
  fireLevel: number;
}) {
  return (
    <div className="relative rounded-2xl border border-orange-500/30 bg-gradient-to-b from-orange-950/40 via-red-950/30 to-stone-950/40 p-5 mb-5 overflow-hidden">
      {/* Smoke / heat haze */}
      <div
        className="absolute inset-0 opacity-30 pointer-events-none"
        style={{
          background:
            "radial-gradient(ellipse at center 40%, rgba(251,146,60,0.4) 0%, transparent 60%)",
        }}
      />
      <div className="relative flex flex-col items-center">
        <p className="text-[10px] font-bold uppercase tracking-[0.3em] text-orange-300/70 mb-2">
          ⚖ The Hot Seat
        </p>
        {/* Hot seat figure */}
        <div className="relative mb-3">
          {/* Flame ring */}
          <div
            className="absolute -inset-3 rounded-full opacity-70 blur-md transition-all duration-1000"
            style={{
              background: `conic-gradient(from 0deg, transparent, rgba(251,146,60,${0.3 + fireLevel * 0.005}) ${fireLevel * 1.5}deg, transparent ${fireLevel * 3}deg)`,
              animation: "spin 4s linear infinite",
            }}
          />
          <div
            className="absolute -inset-2 rounded-full bg-orange-500/40 blur-xl"
            style={{ opacity: 0.3 + (fireLevel / 100) * 0.5 }}
          />
          <div className="relative scale-[1.6]">
            <AgentAvatar agent={{ name: respondent.name, color: respondentColor }} size="xl" />
          </div>
        </div>
        <p className="font-bold text-sm text-orange-100">{respondent.name}</p>
        <p className="text-[10px] uppercase tracking-wider text-orange-300/70 mb-3">
          Under questioning
        </p>

        {/* Fire meter */}
        <div className="w-full max-w-xs">
          <div className="flex items-center justify-between mb-1">
            <span className="text-[10px] font-bold uppercase tracking-wider text-orange-300/80 flex items-center gap-1">
              <Flame size={10} className="fill-orange-500 text-orange-500" />
              Heat Level
            </span>
            <span className="text-[10px] font-mono text-orange-300/70">{fireLevel}%</span>
          </div>
          <div className="relative h-2.5 rounded-full bg-stone-900/60 overflow-hidden border border-orange-500/20">
            <div
              className="absolute inset-y-0 left-0 transition-all duration-1000 ease-out"
              style={{
                width: `${fireLevel}%`,
                background: "linear-gradient(90deg, #f59e0b, #ef4444, #dc2626)",
                boxShadow: "0 0 12px rgba(239,68,68,0.6)",
              }}
            />
          </div>
        </div>

        {/* Questioner row */}
        <div className="mt-4 flex items-center gap-2 text-xs text-orange-200/70">
          <span className="text-[10px] uppercase tracking-wider">Questioner:</span>
          <AgentAvatar agent={{ name: questioner.name, color: questionerColor }} size="sm" />
          <span className="font-medium">{questioner.name}</span>
        </div>
      </div>
    </div>
  );
}

export function computeFireLevel(turns: TurnDetail[]): number {
  const fire = sumReactionKey(turns, "fire");
  const questions = countTurnsOfType(turns, "Question");
  // 8% per question, 6% per fire reaction, capped at 100
  return Math.min(100, questions * 8 + fire * 6);
}

/* ─────────────────── Roast — Laughter Meter ─────────────────── */

export function LaughterMeter({ turns }: { turns: TurnDetail[] }) {
  const roastTurns = countTurnsOfType(turns, "Roast");
  const fire = sumReactionKey(turns, "fire");
  const level = Math.min(100, roastTurns * 12 + fire * 5);

  // Convert to flame emoji count (0-5)
  const flameCount = Math.ceil((level / 100) * 5);
  const flames = Array.from({ length: 5 }, (_, i) => i < flameCount);

  return (
    <div className="relative rounded-2xl border border-amber-500/30 bg-gradient-to-b from-amber-950/40 via-stone-950/30 to-stone-950/40 p-4 mb-5 overflow-hidden">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <Mic size={14} className="text-amber-400" />
          <span className="text-xs font-bold uppercase tracking-wider text-amber-300">
            Laughter Meter
          </span>
        </div>
        <div className="flex items-center gap-0.5">
          {flames.map((on, i) => (
            <Flame
              key={i}
              size={14}
              className={cn(
                "transition-all duration-500",
                on ? "fill-amber-400 text-amber-400 drop-shadow-[0_0_6px_rgba(251,191,36,0.7)]" : "text-stone-700"
              )}
              style={on ? { animationDelay: `${i * 150}ms` } : undefined}
            />
          ))}
        </div>
      </div>
      <p className="text-[10px] text-amber-300/60 italic">
        🎤 {roastTurns} roast{roastTurns === 1 ? "" : "s"} delivered · {fire} 🔥 from the crowd
      </p>
    </div>
  );
}

/* ─────────────────── Tweet — header strip ─────────────────── */

export function TweetHeader({ totalTurns, currentTurn }: { totalTurns: number; currentTurn: number }) {
  return (
    <div className="rounded-xl border border-sky-500/30 bg-sky-500/5 px-4 py-3 mb-5 flex items-center justify-between">
      <div className="flex items-center gap-2">
        <span className="flex h-7 w-7 items-center justify-center rounded-full bg-sky-500 text-white font-bold">
          𝕏
        </span>
        <div>
          <p className="text-sm font-bold text-sky-700 dark:text-sky-300">Tweet Battle</p>
          <p className="text-[10px] text-muted-foreground">280 chars · ratio or be ratio'd</p>
        </div>
      </div>
      <div className="text-right">
        <p className="text-xs font-mono text-sky-600 dark:text-sky-400">
          {currentTurn} / {totalTurns}
        </p>
        <p className="text-[9px] text-muted-foreground uppercase tracking-wider">tweets</p>
      </div>
    </div>
  );
}

/* ─────────────────── Rapid Fire — Round Counter ─────────────────── */

export function RapidFireBanner({ totalTurns, currentTurn }: { totalTurns: number; currentTurn: number }) {
  return (
    <div className="relative rounded-xl border border-red-500/30 bg-gradient-to-r from-red-950/30 via-orange-950/20 to-red-950/30 px-4 py-3 mb-5 overflow-hidden">
      {/* Speed lines */}
      <div
        className="absolute inset-0 opacity-20"
        style={{
          background:
            "repeating-linear-gradient(90deg, transparent 0 8px, rgba(239,68,68,0.4) 8px 9px, transparent 9px 24px)",
        }}
      />
      <div className="relative flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Zap size={16} className="text-red-400 fill-red-400" />
          <span className="text-sm font-black uppercase tracking-wider text-red-300">Rapid Fire</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-bold uppercase tracking-wider text-orange-300/70">Round</span>
          <span className="text-2xl font-black text-orange-200 tabular-nums leading-none">{currentTurn}</span>
          <span className="text-sm text-orange-300/60">/ {totalTurns}</span>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────── Longform — Essay Header ─────────────────── */

export function LongformHeader({ proponentName, opponentName }: { proponentName: string; opponentName: string }) {
  return (
    <div className="text-center mb-8 pb-6 border-b border-border">
      <div className="flex items-center justify-center gap-2 mb-2">
        <Feather size={14} className="text-muted-foreground" />
        <span className="text-[10px] font-bold uppercase tracking-[0.3em] text-muted-foreground">
          A Longform Exchange
        </span>
        <Feather size={14} className="text-muted-foreground" />
      </div>
      <p className="text-xs italic text-muted-foreground/70">
        Featuring essays from <span className="font-semibold text-foreground/80">{proponentName}</span> and{" "}
        <span className="font-semibold text-foreground/80">{opponentName}</span>
      </p>
    </div>
  );
}

/* ─────────────────── Common Ground — Convergence ─────────────────── */

export function CommonGroundHeader({
  proponent,
  opponent,
  proponentColor,
  opponentColor,
}: {
  proponent: Agent;
  opponent: Agent;
  proponentColor: AgentColor;
  opponentColor: AgentColor;
}) {
  return (
    <div className="relative rounded-2xl border border-rose-300/30 bg-gradient-to-r from-rose-50/30 via-pink-50/40 to-rose-50/30 dark:from-rose-950/20 dark:via-pink-950/30 dark:to-rose-950/20 p-5 mb-5 overflow-hidden">
      <div
        className="absolute inset-0 opacity-30 pointer-events-none"
        style={{ background: "radial-gradient(circle at 50% 50%, rgba(244,114,182,0.3), transparent 60%)" }}
      />
      <div className="relative flex items-center justify-center gap-4">
        <div className="flex flex-col items-center">
          <AgentAvatar agent={{ name: proponent.name, color: proponentColor }} size="lg" />
          <p className="text-xs font-semibold mt-1 text-rose-700 dark:text-rose-200">{proponent.name}</p>
        </div>
        <div className="flex flex-col items-center px-2">
          <Heart size={28} className="fill-rose-500 text-rose-500 animate-pulse" />
          <p className="text-[9px] font-bold uppercase tracking-widest text-rose-600 dark:text-rose-300 mt-1">
            seeking common ground
          </p>
        </div>
        <div className="flex flex-col items-center">
          <AgentAvatar agent={{ name: opponent.name, color: opponentColor }} size="lg" />
          <p className="text-xs font-semibold mt-1 text-rose-700 dark:text-rose-200">{opponent.name}</p>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────── Roast — Stage Header ─────────────────── */

export function RoastStageHeader({ proponent, opponent }: { proponent: Agent; opponent: Agent }) {
  return (
    <div className="relative rounded-2xl border border-amber-500/30 bg-gradient-to-b from-stone-950 via-stone-900 to-amber-950/40 p-5 mb-5 overflow-hidden">
      {/* Spotlight */}
      <div
        className="absolute -top-10 left-1/2 -translate-x-1/2 w-64 h-64 rounded-full opacity-30 blur-2xl pointer-events-none"
        style={{ background: "radial-gradient(circle, rgba(251,191,36,0.7) 0%, transparent 60%)" }}
      />
      <div className="relative flex flex-col items-center">
        <Mic size={32} className="text-amber-400 mb-2" />
        <p className="text-[10px] font-bold uppercase tracking-[0.4em] text-amber-300/70 mb-3">
          ★ Tonight at the Comedy Cellar ★
        </p>
        <div className="flex items-center gap-6">
          <div className="text-center">
            <p className="text-sm font-black uppercase text-amber-100">{proponent.name}</p>
          </div>
          <Crown size={16} className="text-amber-400" />
          <div className="text-center">
            <p className="text-sm font-black uppercase text-amber-100">{opponent.name}</p>
          </div>
        </div>
        <p className="text-[10px] italic text-amber-300/60 mt-2">winner takes the crown</p>
      </div>
    </div>
  );
}

/* ─────────────────── Format → Bubble Style Modifier ─────────────────── */

export interface FormatBubbleStyles {
  /** Extra classes for the outer flex row */
  rowClass?: string;
  /** Extra classes for the bubble div itself */
  bubbleClass?: string;
  /** Force all bubbles to one side (no left/right alternation) */
  alignment?: "left" | "right" | "center" | "alternating";
  /** Hide the small avatar bubble label thing */
  hideAvatarLabel?: boolean;
}

export function getFormatBubbleStyles(format?: string): FormatBubbleStyles {
  // Each bubble uses an opaque background (light tint in light mode, dark tint
  // in dark mode) so text stays readable even when sitting over a dark
  // matchup-theme backdrop. The `!` prefix overrides the agent-color BUBBLE_BG
  // applied earlier in the className chain.
  switch (format) {
    case "tweet":
      return {
        rowClass: "gap-2",
        bubbleClass: "rounded-xl border border-sky-500/30 !bg-sky-50 dark:!bg-sky-950/60 !text-sm !py-2 !px-3",
        alignment: "left",
      };
    case "rapid_fire":
      return {
        rowClass: "gap-2",
        bubbleClass: "rounded-lg !py-2 !px-3 border border-red-500/40 !bg-red-50 dark:!bg-red-950/50",
        alignment: "alternating",
      };
    case "longform":
      return {
        rowClass: "gap-4",
        bubbleClass: "!rounded-md border-l-4 !bg-card !text-base !leading-loose font-serif !px-6 !py-5 max-w-full",
        alignment: "left",
      };
    case "roast":
      return {
        rowClass: "gap-3",
        bubbleClass: "border border-amber-500/50 !bg-amber-50 dark:!bg-amber-950/60 shadow-[0_0_20px_-5px_rgba(251,191,36,0.5)]",
        alignment: "alternating",
      };
    case "common_ground":
      return {
        rowClass: "gap-3",
        bubbleClass: "border border-rose-400/40 !bg-rose-50 dark:!bg-rose-950/50",
        alignment: "alternating",
      };
    case "town_hall":
      return {
        rowClass: "gap-3",
        bubbleClass: "border-l-2 border-orange-500/50 !bg-orange-50 dark:!bg-orange-950/40",
        alignment: "alternating",
      };
    default:
      return { alignment: "alternating" };
  }
}
