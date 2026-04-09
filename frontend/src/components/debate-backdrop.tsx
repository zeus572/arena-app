import type { ResolvedMatchupTheme } from "@/lib/use-settings";

interface DebateBackdropProps {
  theme: ResolvedMatchupTheme;
}

/**
 * Persistent themed backdrop behind the debate page. Mirrors the look of the
 * matching MatchupIntro but at much lower intensity so the content stays readable.
 *
 * Sits at `fixed inset-0 -z-10` so it covers the viewport behind everything.
 */
export function DebateBackdrop({ theme }: DebateBackdropProps) {
  if (theme === "off") return null;

  if (theme === "arcade") {
    return (
      <div className="fixed inset-0 -z-10 pointer-events-none overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-br from-slate-950 via-purple-950/90 to-slate-950" />
        {/* Slow scrolling diagonal stripes */}
        <div
          className="absolute inset-0 opacity-[0.07]"
          style={{
            backgroundImage:
              "repeating-linear-gradient(45deg, transparent 0 60px, rgba(255,255,255,0.5) 60px 62px, transparent 62px 140px, rgba(236,72,153,0.5) 140px 142px)",
            backgroundSize: "200px 200px",
            animation: "matchup-stripes-scroll 45s linear infinite",
          }}
        />
        {/* Scanlines */}
        <div
          className="absolute inset-0 opacity-[0.06] mix-blend-overlay"
          style={{
            backgroundImage: "repeating-linear-gradient(0deg, transparent 0 2px, rgba(0,0,0,0.6) 2px 4px)",
          }}
        />
        {/* Vignette */}
        <div
          className="absolute inset-0"
          style={{ background: "radial-gradient(ellipse at center, transparent 30%, rgba(0,0,0,0.65) 100%)" }}
        />
      </div>
    );
  }

  if (theme === "anime") {
    return (
      <div className="fixed inset-0 -z-10 pointer-events-none overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-br from-rose-950 via-fuchsia-950/80 to-indigo-950" />
        {/* Dim radial speedlines, no animation so it doesn't distract */}
        <div
          className="absolute inset-0 opacity-[0.08]"
          style={{
            background:
              "repeating-conic-gradient(from 0deg at 50% 30%, transparent 0deg 6deg, rgba(255,255,255,0.5) 6deg 7deg, transparent 7deg 13deg)",
          }}
        />
        {/* Center warm glow */}
        <div
          className="absolute left-1/2 top-1/3 -translate-x-1/2 -translate-y-1/2 w-[60vmin] h-[60vmin] rounded-full opacity-30 blur-3xl"
          style={{ background: "radial-gradient(circle, rgba(253,224,71,0.6) 0%, transparent 70%)" }}
        />
        <div
          className="absolute inset-0"
          style={{ background: "radial-gradient(ellipse at center, transparent 40%, rgba(0,0,0,0.55) 100%)" }}
        />
      </div>
    );
  }

  if (theme === "boxing") {
    return (
      <div className="fixed inset-0 -z-10 pointer-events-none overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-b from-stone-950 via-stone-900 to-black" />
        {/* Two warm spotlights from above */}
        <div
          className="absolute -top-[20vmin] left-1/4 w-[80vmin] h-[80vmin] -translate-x-1/2 rounded-full opacity-20 blur-2xl"
          style={{ background: "radial-gradient(circle, rgba(255,200,80,0.7) 0%, transparent 60%)" }}
        />
        <div
          className="absolute -top-[20vmin] right-1/4 w-[80vmin] h-[80vmin] translate-x-1/2 rounded-full opacity-20 blur-2xl"
          style={{ background: "radial-gradient(circle, rgba(255,200,80,0.7) 0%, transparent 60%)" }}
        />
        {/* Wood floor at the bottom */}
        <div
          className="absolute inset-x-0 bottom-0 h-1/3 opacity-[0.18]"
          style={{
            background:
              "linear-gradient(to top, #1c1410 0%, transparent 100%), repeating-linear-gradient(90deg, #2a1f1a 0 60px, #1c1410 60px 62px)",
            transform: "perspective(800px) rotateX(60deg)",
            transformOrigin: "bottom",
          }}
        />
        <div
          className="absolute inset-0"
          style={{ background: "radial-gradient(ellipse at center top, transparent 30%, rgba(0,0,0,0.65) 100%)" }}
        />
      </div>
    );
  }

  if (theme === "cinematic") {
    return (
      <div className="fixed inset-0 -z-10 pointer-events-none overflow-hidden">
        <div className="absolute inset-0 bg-black" />
        {/* Subtle blue moonlight from above */}
        <div
          className="absolute inset-0"
          style={{ background: "radial-gradient(ellipse at 50% 0%, rgba(59,130,246,0.08) 0%, transparent 50%)" }}
        />
        {/* Persistent letterbox bars */}
        <div className="absolute top-0 inset-x-0 h-[6vh] bg-black border-b border-white/[0.04]" />
        <div className="absolute bottom-0 inset-x-0 h-[6vh] bg-black border-t border-white/[0.04]" />
        {/* Film grain */}
        <div
          className="absolute inset-0 opacity-[0.05] mix-blend-overlay"
          style={{
            backgroundImage:
              "url(\"data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E\")",
          }}
        />
      </div>
    );
  }

  return null;
}

/** Theme-matching accent colors for the header card. */
export const BACKDROP_ACCENTS: Record<Exclude<ResolvedMatchupTheme, "off">, string> = {
  arcade: "ring-pink-500/30 shadow-[0_0_30px_-10px_rgba(236,72,153,0.6)]",
  anime: "ring-fuchsia-400/30 shadow-[0_0_30px_-10px_rgba(217,70,239,0.6)]",
  boxing: "ring-amber-500/30 shadow-[0_0_30px_-10px_rgba(245,158,11,0.6)]",
  cinematic: "ring-white/15 shadow-[0_0_40px_-15px_rgba(255,255,255,0.4)]",
};
