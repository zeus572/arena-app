import { useState } from "react";
import { Settings as SettingsIcon, Sparkles, Check, Play, Shuffle } from "lucide-react";
import { cn } from "@/lib/utils";
import { useSettings, MATCHUP_THEMES, resolveMatchupTheme, type MatchupTheme, type ResolvedMatchupTheme } from "@/lib/use-settings";
import { MatchupIntro, type MatchupFighter } from "@/components/matchup-intro";
import { Button } from "@/components/ui/button";

const DEMO_PROPONENT: MatchupFighter = {
  id: "demo-prop",
  name: "Ada Reyes",
  label: "Progressive",
  color: "progressive",
};

const DEMO_OPPONENT: MatchupFighter = {
  id: "demo-opp",
  name: "Sam Holt",
  label: "Conservative",
  color: "conservative",
};

const DEMO_TOPIC = "Should the federal government cap prescription drug prices?";

export default function SettingsPage() {
  const [settings, updateSettings] = useSettings();
  const [playingTheme, setPlayingTheme] = useState<ResolvedMatchupTheme | null>(null);

  const playPreview = (choice: MatchupTheme) => {
    const resolved = resolveMatchupTheme(choice);
    if (resolved !== "off") setPlayingTheme(resolved);
  };

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <div className="flex items-center gap-2 mb-2">
        <SettingsIcon size={22} className="text-primary" />
        <h1 className="text-2xl font-bold text-foreground">Settings</h1>
      </div>
      <p className="text-sm text-muted-foreground mb-8 max-w-xl">
        Tune how Debate Arena feels. Pick a matchup intro, preview each one, and the next debate you open will use it.
      </p>

      <section className="rounded-xl border border-border bg-card p-6 mb-6">
        <div className="flex items-center gap-2 mb-1">
          <Sparkles size={16} className="text-primary" />
          <h2 className="text-base font-semibold text-card-foreground">Debate Matchup Intro</h2>
        </div>
        <p className="text-xs text-muted-foreground mb-5">
          A fighting-game style "lineup" animation that plays when you open a debate. Pick a theme below.
        </p>

        <div className="flex flex-col gap-3">
          {MATCHUP_THEMES.map((theme) => {
            const isActive = settings.matchupTheme === theme.value;
            return (
              <button
                key={theme.value}
                onClick={() => updateSettings({ matchupTheme: theme.value })}
                className={cn(
                  "relative flex items-start gap-4 rounded-lg border p-4 text-left transition-all",
                  isActive
                    ? "border-primary bg-primary/5 ring-2 ring-primary/30"
                    : "border-border bg-background hover:border-primary/40 hover:bg-secondary/40"
                )}
              >
                <div
                  className={cn(
                    "h-5 w-5 rounded-full border-2 shrink-0 mt-0.5 flex items-center justify-center",
                    isActive ? "border-primary bg-primary" : "border-muted-foreground/40"
                  )}
                >
                  {isActive && <Check size={12} className="text-primary-foreground" />}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2 mb-0.5">
                    <p className="text-sm font-semibold text-card-foreground flex items-center gap-1.5">
                      {theme.value === "random" && <Shuffle size={13} className="text-primary" />}
                      {theme.label}
                    </p>
                    {theme.value !== "off" && (
                      <span
                        role="button"
                        tabIndex={0}
                        onClick={(e) => {
                          e.stopPropagation();
                          playPreview(theme.value);
                        }}
                        onKeyDown={(e) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            e.stopPropagation();
                            playPreview(theme.value);
                          }
                        }}
                        className="inline-flex items-center gap-1 rounded-md border border-border bg-card px-2 py-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground hover:text-primary hover:border-primary/40 transition"
                      >
                        <Play size={10} />
                        Preview
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-muted-foreground">{theme.tagline}</p>
                </div>
              </button>
            );
          })}
        </div>

        <div className="mt-6 pt-5 border-t border-border">
          <p className="text-[11px] text-muted-foreground">
            Tip — during the intro, click anywhere or press <kbd className="px-1 py-0.5 rounded bg-secondary text-[10px] font-mono">Esc</kbd> to skip.
          </p>
        </div>
      </section>

      {playingTheme && (
        <MatchupIntro
          theme={playingTheme}
          proponent={DEMO_PROPONENT}
          opponent={DEMO_OPPONENT}
          topic={DEMO_TOPIC}
          onComplete={() => setPlayingTheme(null)}
        />
      )}

      <div className="rounded-xl border border-dashed border-border p-5 text-center">
        <p className="text-xs text-muted-foreground">
          More settings coming soon. Want something specific?{" "}
          <Button variant="link" size="sm" className="h-auto p-0 text-xs" asChild>
            <a href="/start">Tell us in a debate.</a>
          </Button>
        </p>
      </div>
    </main>
  );
}
