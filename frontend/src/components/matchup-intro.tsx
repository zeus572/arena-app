import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import { AgentAvatar } from "@/components/agent-avatar";
import { type AgentColor, AVATAR_COLORS } from "@/lib/agent-colors";
import type { ConcreteMatchupTheme, ResolvedMatchupTheme } from "@/lib/use-settings";

export interface MatchupFighter {
  id: string;
  name: string;
  label: string;
  color: AgentColor;
}

interface MatchupIntroProps {
  proponent: MatchupFighter;
  opponent: MatchupFighter;
  topic: string;
  /** Already-resolved theme — caller is responsible for handling "random". */
  theme: ResolvedMatchupTheme;
  onComplete: () => void;
  /** When true the component renders inline (for previews) and never auto-dismisses. */
  preview?: boolean;
}

const DURATIONS: Record<ConcreteMatchupTheme, number> = {
  arcade: 3400,
  anime: 3200,
  boxing: 3800,
  cinematic: 4200,
};

export function MatchupIntro({ proponent, opponent, topic, theme, onComplete, preview = false }: MatchupIntroProps) {
  const [closing, setClosing] = useState(false);

  useEffect(() => {
    if (preview || theme === "off") return;
    const total = DURATIONS[theme];
    const fadeOutAt = total - 400;
    const fadeTimer = setTimeout(() => setClosing(true), fadeOutAt);
    const doneTimer = setTimeout(onComplete, total);
    return () => {
      clearTimeout(fadeTimer);
      clearTimeout(doneTimer);
    };
  }, [theme, preview, onComplete]);

  useEffect(() => {
    if (preview) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" || e.key === " " || e.key === "Enter") {
        e.preventDefault();
        onComplete();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [preview, onComplete]);

  if (theme === "off") return null;

  const containerClass = preview
    ? "relative w-full h-64 rounded-xl overflow-hidden"
    : cn(
        "fixed inset-0 z-[100] overflow-hidden",
        closing && "animate-[matchup-fade-out_400ms_ease-out_forwards]"
      );

  return (
    <div
      className={containerClass}
      onClick={preview ? undefined : onComplete}
      role={preview ? undefined : "dialog"}
      aria-label={preview ? undefined : "Debate matchup intro"}
    >
      {theme === "arcade" && <ArcadeTheme proponent={proponent} opponent={opponent} topic={topic} preview={preview} />}
      {theme === "anime" && <AnimeTheme proponent={proponent} opponent={opponent} topic={topic} preview={preview} />}
      {theme === "boxing" && <BoxingTheme proponent={proponent} opponent={opponent} topic={topic} preview={preview} />}
      {theme === "cinematic" && <CinematicTheme proponent={proponent} opponent={opponent} topic={topic} preview={preview} />}

      {!preview && (
        <button
          onClick={(e) => { e.stopPropagation(); onComplete(); }}
          className="absolute top-4 right-4 z-50 rounded-full border border-white/30 bg-black/40 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-white/80 backdrop-blur-sm hover:bg-black/60 hover:text-white transition"
        >
          Skip ›
        </button>
      )}
    </div>
  );
}

/* ──────────────── Arcade theme ──────────────── */

function ArcadeTheme({ proponent, opponent, topic, preview }: { proponent: MatchupFighter; opponent: MatchupFighter; topic: string; preview: boolean }) {
  const dur = preview ? "0s" : "";
  return (
    <div className="absolute inset-0 bg-gradient-to-br from-slate-950 via-purple-950 to-slate-950 flex items-center justify-center">
      {/* Diagonal scrolling stripes */}
      <div
        className="absolute inset-0 opacity-30"
        style={{
          backgroundImage: "repeating-linear-gradient(45deg, transparent 0 40px, rgba(255,255,255,0.04) 40px 80px, transparent 80px 120px, rgba(236,72,153,0.08) 120px 160px)",
          backgroundSize: "200px 200px",
          animation: preview ? undefined : "matchup-stripes-scroll 3s linear infinite",
        }}
      />
      {/* CRT scanlines */}
      <div className="absolute inset-0 pointer-events-none mix-blend-overlay opacity-20"
        style={{ backgroundImage: "repeating-linear-gradient(0deg, transparent 0 2px, rgba(0,0,0,0.5) 2px 4px)" }}
      />
      {/* Vignette */}
      <div className="absolute inset-0 bg-radial-gradient" style={{ background: "radial-gradient(ellipse at center, transparent 30%, rgba(0,0,0,0.7) 100%)" }} />

      {/* Fighters */}
      <div className="relative w-full max-w-5xl flex items-center justify-between px-6 sm:px-12">
        {/* Proponent (left) */}
        <div
          className="flex flex-col items-center"
          style={{ animation: `matchup-slam-left 800ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 100ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            <div className={cn("absolute -inset-4 rounded-full blur-2xl opacity-60", AVATAR_COLORS[proponent.color])} />
            <div className="relative scale-[2.2] sm:scale-[3]">
              <AgentAvatar agent={{ name: proponent.name, color: proponent.color }} size="xl" />
            </div>
          </div>
          <div
            className="mt-12 sm:mt-16 text-center"
            style={{ animation: `matchup-name-pop 600ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 1000ms both${dur ? ", none" : ""}` }}
          >
            <p className="text-2xl sm:text-4xl font-black text-white tracking-tight uppercase drop-shadow-[0_4px_0_rgba(236,72,153,0.6)]">{proponent.name}</p>
            <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.3em] text-pink-300 mt-1">{proponent.label}</p>
          </div>
        </div>

        {/* VS stamp */}
        <div
          className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-[60%] z-10"
          style={{ animation: `matchup-vs-stamp 700ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 600ms both, matchup-glitch 200ms steps(2) 1300ms 3${dur ? ", none, none" : ""}` }}
        >
          <div className="relative">
            <span className="block text-[7rem] sm:text-[12rem] font-black italic text-yellow-300 drop-shadow-[0_8px_0_rgba(236,72,153,1)]" style={{ WebkitTextStroke: "4px black" }}>VS</span>
            <span className="absolute inset-0 text-[7rem] sm:text-[12rem] font-black italic text-cyan-300 mix-blend-screen opacity-70 -translate-x-1 translate-y-1">VS</span>
          </div>
        </div>

        {/* Opponent (right) */}
        <div
          className="flex flex-col items-center"
          style={{ animation: `matchup-slam-right 800ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 100ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            <div className={cn("absolute -inset-4 rounded-full blur-2xl opacity-60", AVATAR_COLORS[opponent.color])} />
            <div className="relative scale-[2.2] sm:scale-[3]">
              <AgentAvatar agent={{ name: opponent.name, color: opponent.color }} size="xl" />
            </div>
          </div>
          <div
            className="mt-12 sm:mt-16 text-center"
            style={{ animation: `matchup-name-pop 600ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 1200ms both${dur ? ", none" : ""}` }}
          >
            <p className="text-2xl sm:text-4xl font-black text-white tracking-tight uppercase drop-shadow-[0_4px_0_rgba(34,211,238,0.6)]">{opponent.name}</p>
            <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.3em] text-cyan-300 mt-1">{opponent.label}</p>
          </div>
        </div>
      </div>

      {/* Topic strip */}
      <div
        className="absolute bottom-8 left-0 right-0 text-center px-6"
        style={{ animation: `matchup-name-pop 500ms ease-out 1800ms both${dur ? ", none" : ""}` }}
      >
        <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.4em] text-yellow-300/80 mb-1">★ Round 1 ★</p>
        <p className="text-sm sm:text-base font-semibold text-white/90 max-w-2xl mx-auto line-clamp-2">{topic}</p>
      </div>
    </div>
  );
}

/* ──────────────── Anime theme ──────────────── */

function AnimeTheme({ proponent, opponent, topic, preview }: { proponent: MatchupFighter; opponent: MatchupFighter; topic: string; preview: boolean }) {
  const dur = preview ? "0s" : "";
  return (
    <div
      className="absolute inset-0 bg-gradient-to-br from-rose-950 via-fuchsia-900 to-indigo-950 flex items-center justify-center"
      style={{ animation: preview ? undefined : "matchup-screen-shake 200ms steps(4) 1400ms 2" }}
    >
      {/* Speedlines radial */}
      <div
        className="absolute inset-0 opacity-70"
        style={{
          background: "repeating-conic-gradient(from 0deg at 50% 50%, transparent 0deg 4deg, rgba(255,255,255,0.18) 4deg 5deg, transparent 5deg 9deg)",
          animation: preview ? undefined : "matchup-speedlines 1.2s ease-out both",
        }}
      />
      {/* Center burst */}
      <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-[40vmin] h-[40vmin] rounded-full bg-yellow-300/30 blur-3xl" />

      <div className="relative w-full max-w-5xl flex items-center justify-between px-6 sm:px-12">
        {/* Proponent */}
        <div
          className="flex flex-col items-center"
          style={{ animation: `matchup-anime-zoom-left 700ms cubic-bezier(0.16, 1, 0.3, 1) 200ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            <div className="absolute -inset-6 rounded-full bg-pink-400/40 blur-2xl animate-pulse" />
            <div className="relative scale-[2.4] sm:scale-[3.2]">
              <AgentAvatar agent={{ name: proponent.name, color: proponent.color }} size="xl" />
            </div>
          </div>
          <div className="mt-12 sm:mt-16 text-center" style={{ animation: `matchup-name-pop 500ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 1100ms both${dur ? ", none" : ""}` }}>
            <p className="text-2xl sm:text-4xl font-black text-white italic tracking-tight" style={{ textShadow: "0 0 20px rgba(244,114,182,0.8), 4px 4px 0 #be185d" }}>{proponent.name}</p>
            <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.3em] text-pink-200 mt-1">{proponent.label}</p>
          </div>
        </div>

        {/* Impact stamp */}
        <div
          className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-[55%] z-10"
          style={{ animation: `matchup-impact-stamp 600ms cubic-bezier(0.2, 0.9, 0.3, 1.4) 700ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            {/* Star burst */}
            <div className="absolute inset-0 -m-12 flex items-center justify-center">
              <div className="w-48 h-48 sm:w-64 sm:h-64" style={{ background: "conic-gradient(from 0deg, #fde047 0deg 20deg, transparent 20deg 40deg, #fde047 40deg 60deg, transparent 60deg 80deg, #fde047 80deg 100deg, transparent 100deg 120deg, #fde047 120deg 140deg, transparent 140deg 160deg, #fde047 160deg 180deg, transparent 180deg 200deg, #fde047 200deg 220deg, transparent 220deg 240deg, #fde047 240deg 260deg, transparent 260deg 280deg, #fde047 280deg 300deg, transparent 300deg 320deg, #fde047 320deg 340deg, transparent 340deg 360deg)", clipPath: "polygon(50% 0%, 60% 35%, 100% 50%, 60% 65%, 50% 100%, 40% 65%, 0% 50%, 40% 35%)" }} />
            </div>
            <span className="relative block text-[6rem] sm:text-[10rem] font-black italic text-white" style={{ WebkitTextStroke: "5px #be185d", textShadow: "8px 8px 0 #831843" }}>VS</span>
          </div>
        </div>

        {/* Opponent */}
        <div
          className="flex flex-col items-center"
          style={{ animation: `matchup-anime-zoom-right 700ms cubic-bezier(0.16, 1, 0.3, 1) 200ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            <div className="absolute -inset-6 rounded-full bg-indigo-400/40 blur-2xl animate-pulse" />
            <div className="relative scale-[2.4] sm:scale-[3.2]">
              <AgentAvatar agent={{ name: opponent.name, color: opponent.color }} size="xl" />
            </div>
          </div>
          <div className="mt-12 sm:mt-16 text-center" style={{ animation: `matchup-name-pop 500ms cubic-bezier(0.2, 0.9, 0.3, 1.2) 1300ms both${dur ? ", none" : ""}` }}>
            <p className="text-2xl sm:text-4xl font-black text-white italic tracking-tight" style={{ textShadow: "0 0 20px rgba(165,180,252,0.8), 4px 4px 0 #4338ca" }}>{opponent.name}</p>
            <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.3em] text-indigo-200 mt-1">{opponent.label}</p>
          </div>
        </div>
      </div>

      <div
        className="absolute bottom-8 left-0 right-0 text-center px-6"
        style={{ animation: `matchup-name-pop 500ms ease-out 1700ms both${dur ? ", none" : ""}` }}
      >
        <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.4em] text-yellow-300 mb-1">⚔ DESTINY CLASH ⚔</p>
        <p className="text-sm sm:text-base font-semibold text-white/90 italic max-w-2xl mx-auto line-clamp-2">{topic}</p>
      </div>
    </div>
  );
}

/* ──────────────── Boxing theme ──────────────── */

function BoxingTheme({ proponent, opponent, topic, preview }: { proponent: MatchupFighter; opponent: MatchupFighter; topic: string; preview: boolean }) {
  const dur = preview ? "0s" : "";
  return (
    <div className="absolute inset-0 bg-gradient-to-b from-stone-950 via-stone-900 to-black flex items-center justify-center overflow-hidden">
      {/* Floor wood pattern */}
      <div
        className="absolute inset-x-0 bottom-0 h-1/3 opacity-30"
        style={{
          background: "linear-gradient(to top, #1c1410 0%, transparent 100%), repeating-linear-gradient(90deg, #2a1f1a 0 60px, #1c1410 60px 62px)",
          transform: "perspective(600px) rotateX(60deg)",
          transformOrigin: "bottom",
        }}
      />
      {/* Two spotlights */}
      <div
        className="absolute left-1/4 top-0 w-[60vmin] h-[60vmin] -translate-x-1/2 rounded-full"
        style={{
          background: "radial-gradient(circle, rgba(255,220,140,0.4) 0%, rgba(255,200,80,0.15) 30%, transparent 70%)",
          animation: preview ? undefined : "matchup-spotlight-sweep 1.4s ease-out 200ms both",
        }}
      />
      <div
        className="absolute right-1/4 top-0 w-[60vmin] h-[60vmin] translate-x-1/2 rounded-full"
        style={{
          background: "radial-gradient(circle, rgba(255,220,140,0.4) 0%, rgba(255,200,80,0.15) 30%, transparent 70%)",
          animation: preview ? undefined : "matchup-spotlight-sweep 1.4s ease-out 200ms both",
        }}
      />
      {/* Ring rope border */}
      <div
        className="absolute inset-8 sm:inset-12 border-4 border-amber-700/60 rounded-sm"
        style={{ animation: preview ? undefined : "matchup-rope-glow 2s ease-in-out infinite" }}
      />

      <div className="relative w-full max-w-5xl flex items-center justify-between px-12 sm:px-20">
        {/* Proponent */}
        <div
          className="flex flex-col items-center"
          style={{ animation: `matchup-slow-zoom 1200ms ease-out 400ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            <div className="absolute -inset-3 rounded-full bg-amber-300/30 blur-2xl" />
            <div className="relative scale-[2.2] sm:scale-[2.8]">
              <AgentAvatar agent={{ name: proponent.name, color: proponent.color }} size="xl" />
            </div>
          </div>
          <div
            className="mt-10 sm:mt-14 px-4 py-2 bg-gradient-to-b from-amber-600 to-amber-800 border-2 border-amber-400 rounded shadow-2xl text-center"
            style={{ animation: `matchup-card-rise 600ms cubic-bezier(0.16, 1, 0.3, 1) 1400ms both${dur ? ", none" : ""}` }}
          >
            <p className="text-[9px] font-bold uppercase tracking-[0.3em] text-amber-200">In the red corner</p>
            <p className="text-lg sm:text-2xl font-black text-white tracking-tight uppercase">{proponent.name}</p>
            <p className="text-[10px] font-semibold uppercase tracking-wider text-amber-100/80">"{proponent.label}"</p>
          </div>
        </div>

        {/* Center divider */}
        <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-[80%] z-10 text-center">
          <div
            className="text-6xl sm:text-8xl font-serif italic text-amber-300"
            style={{
              animation: `matchup-card-rise 800ms cubic-bezier(0.16, 1, 0.3, 1) 900ms both${dur ? ", none" : ""}`,
              textShadow: "0 0 30px rgba(255,200,80,0.6), 4px 4px 0 #78350f",
            }}
          >
            vs
          </div>
        </div>

        {/* Opponent */}
        <div
          className="flex flex-col items-center"
          style={{ animation: `matchup-slow-zoom 1200ms ease-out 700ms both${dur ? ", none" : ""}` }}
        >
          <div className="relative">
            <div className="absolute -inset-3 rounded-full bg-amber-300/30 blur-2xl" />
            <div className="relative scale-[2.2] sm:scale-[2.8]">
              <AgentAvatar agent={{ name: opponent.name, color: opponent.color }} size="xl" />
            </div>
          </div>
          <div
            className="mt-10 sm:mt-14 px-4 py-2 bg-gradient-to-b from-blue-700 to-blue-900 border-2 border-blue-400 rounded shadow-2xl text-center"
            style={{ animation: `matchup-card-rise 600ms cubic-bezier(0.16, 1, 0.3, 1) 1700ms both${dur ? ", none" : ""}` }}
          >
            <p className="text-[9px] font-bold uppercase tracking-[0.3em] text-blue-200">In the blue corner</p>
            <p className="text-lg sm:text-2xl font-black text-white tracking-tight uppercase">{opponent.name}</p>
            <p className="text-[10px] font-semibold uppercase tracking-wider text-blue-100/80">"{opponent.label}"</p>
          </div>
        </div>
      </div>

      <div
        className="absolute bottom-6 left-0 right-0 text-center px-12"
        style={{ animation: `matchup-card-rise 600ms ease-out 2100ms both${dur ? ", none" : ""}` }}
      >
        <p className="text-[10px] sm:text-xs font-bold uppercase tracking-[0.4em] text-amber-300 mb-1">⚜ Tonight's Main Event ⚜</p>
        <p className="text-sm sm:text-base font-serif italic text-amber-100/90 max-w-2xl mx-auto line-clamp-2">{topic}</p>
      </div>
    </div>
  );
}

/* ──────────────── Cinematic theme ──────────────── */

function CinematicTheme({ proponent, opponent, topic, preview }: { proponent: MatchupFighter; opponent: MatchupFighter; topic: string; preview: boolean }) {
  const dur = preview ? "0s" : "";
  return (
    <div className="absolute inset-0 bg-black flex items-center justify-center overflow-hidden">
      {/* Background portrait collage with ken burns */}
      <div className="absolute inset-0 grid grid-cols-2">
        <div
          className="relative overflow-hidden"
          style={{ animation: `matchup-ken-burns 5s ease-out both${dur ? ", none" : ""}` }}
        >
          <div className={cn("absolute inset-0 opacity-40", AVATAR_COLORS[proponent.color])} />
          <div
            className="absolute inset-0 flex items-center justify-end pr-[10%]"
            style={{ animation: `matchup-cinematic-fade 1500ms ease-out 600ms both${dur ? ", none" : ""}` }}
          >
            <div className="scale-[3] sm:scale-[4] grayscale-[30%] opacity-80">
              <AgentAvatar agent={{ name: proponent.name, color: proponent.color }} size="xl" />
            </div>
          </div>
        </div>
        <div
          className="relative overflow-hidden"
          style={{ animation: `matchup-ken-burns 5s ease-out both${dur ? ", none" : ""}` }}
        >
          <div className={cn("absolute inset-0 opacity-40", AVATAR_COLORS[opponent.color])} />
          <div
            className="absolute inset-0 flex items-center justify-start pl-[10%]"
            style={{ animation: `matchup-cinematic-fade 1500ms ease-out 900ms both${dur ? ", none" : ""}` }}
          >
            <div className="scale-[3] sm:scale-[4] grayscale-[30%] opacity-80">
              <AgentAvatar agent={{ name: opponent.name, color: opponent.color }} size="xl" />
            </div>
          </div>
        </div>
      </div>

      {/* Center divider line */}
      <div
        className="absolute left-1/2 top-[15%] bottom-[15%] w-px bg-gradient-to-b from-transparent via-white/40 to-transparent"
        style={{ animation: `matchup-cinematic-fade 1500ms ease-out 1200ms both${dur ? ", none" : ""}` }}
      />

      {/* Letterbox bars */}
      <div
        className="absolute top-0 left-0 right-0 h-[12%] bg-black z-20"
        style={{ animation: `matchup-letterbox-top 700ms cubic-bezier(0.7, 0, 0.3, 1) both${dur ? ", none" : ""}` }}
      />
      <div
        className="absolute bottom-0 left-0 right-0 h-[12%] bg-black z-20"
        style={{ animation: `matchup-letterbox-bottom 700ms cubic-bezier(0.7, 0, 0.3, 1) both${dur ? ", none" : ""}` }}
      />

      {/* Title cards */}
      <div className="relative z-30 w-full max-w-5xl px-6 flex items-end justify-between pb-2">
        <div
          className="text-left"
          style={{ animation: `matchup-cinematic-fade 1200ms ease-out 1600ms both${dur ? ", none" : ""}` }}
        >
          <p className="text-[10px] font-mono uppercase tracking-[0.4em] text-white/50 mb-1">— Featuring —</p>
          <p className="text-2xl sm:text-4xl font-serif text-white tracking-wide">{proponent.name}</p>
          <p className="text-[10px] sm:text-xs font-mono uppercase tracking-[0.3em] text-white/60 mt-1">{proponent.label}</p>
        </div>
        <div
          className="text-right"
          style={{ animation: `matchup-cinematic-fade 1200ms ease-out 2100ms both${dur ? ", none" : ""}` }}
        >
          <p className="text-[10px] font-mono uppercase tracking-[0.4em] text-white/50 mb-1">— And —</p>
          <p className="text-2xl sm:text-4xl font-serif text-white tracking-wide">{opponent.name}</p>
          <p className="text-[10px] sm:text-xs font-mono uppercase tracking-[0.3em] text-white/60 mt-1">{opponent.label}</p>
        </div>
      </div>

      {/* Title at bottom */}
      <div
        className="absolute bottom-[14%] left-0 right-0 text-center px-12 z-30"
        style={{ animation: `matchup-cinematic-fade 1500ms ease-out 2700ms both${dur ? ", none" : ""}` }}
      >
        <p className="text-[10px] font-mono uppercase tracking-[0.5em] text-white/40 mb-2">In a debate that would change everything</p>
        <p className="text-base sm:text-2xl font-serif italic text-white/90 max-w-3xl mx-auto line-clamp-2">"{topic}"</p>
      </div>

      {/* Film grain */}
      <div className="absolute inset-0 opacity-[0.04] pointer-events-none mix-blend-overlay z-40"
        style={{ backgroundImage: "url(\"data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E\")" }}
      />
    </div>
  );
}
