import { useEffect, useState, useCallback } from "react";

export type ConcreteMatchupTheme = "arcade" | "anime" | "boxing" | "cinematic";
export type MatchupTheme = "off" | "random" | ConcreteMatchupTheme;
export type ResolvedMatchupTheme = "off" | ConcreteMatchupTheme;

export const CONCRETE_MATCHUP_THEMES: ConcreteMatchupTheme[] = ["arcade", "anime", "boxing", "cinematic"];

export const MATCHUP_THEMES: { value: MatchupTheme; label: string; tagline: string }[] = [
  { value: "off", label: "Off", tagline: "No intro animation, plain debate background" },
  { value: "random", label: "Random", tagline: "Surprise me — pick a different theme each debate" },
  { value: "arcade", label: "Arcade", tagline: "Retro fighter — fighters slam in, neon stripes, glitchy VS stamp" },
  { value: "anime", label: "Anime", tagline: "Speedlines, screen shake, dramatic kanji-style impact" },
  { value: "boxing", label: "Boxing", tagline: "Spotlights, slow zoom, ringside announcer card" },
  { value: "cinematic", label: "Cinematic", tagline: "Letterbox bars, slow ken-burns, movie trailer title cards" },
];

/** Resolve "random" → a concrete theme. "off" stays "off". */
export function resolveMatchupTheme(theme: MatchupTheme): ResolvedMatchupTheme {
  if (theme === "random") {
    return CONCRETE_MATCHUP_THEMES[Math.floor(Math.random() * CONCRETE_MATCHUP_THEMES.length)];
  }
  return theme;
}

export interface Settings {
  matchupTheme: MatchupTheme;
}

const DEFAULT_SETTINGS: Settings = {
  matchupTheme: "arcade",
};

const STORAGE_KEY = "arena-settings";

function loadSettings(): Settings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT_SETTINGS;
    const parsed = JSON.parse(raw);
    return { ...DEFAULT_SETTINGS, ...parsed };
  } catch {
    return DEFAULT_SETTINGS;
  }
}

function saveSettings(s: Settings) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s));
    // Notify other hook subscribers in this tab
    window.dispatchEvent(new CustomEvent("arena-settings-change", { detail: s }));
  } catch {}
}

export function useSettings(): [Settings, (patch: Partial<Settings>) => void] {
  const [settings, setSettings] = useState<Settings>(loadSettings);

  useEffect(() => {
    const onChange = (e: Event) => {
      const detail = (e as CustomEvent<Settings>).detail;
      if (detail) setSettings(detail);
    };
    const onStorage = (e: StorageEvent) => {
      if (e.key === STORAGE_KEY) setSettings(loadSettings());
    };
    window.addEventListener("arena-settings-change", onChange);
    window.addEventListener("storage", onStorage);
    return () => {
      window.removeEventListener("arena-settings-change", onChange);
      window.removeEventListener("storage", onStorage);
    };
  }, []);

  const update = useCallback((patch: Partial<Settings>) => {
    setSettings((prev) => {
      const next = { ...prev, ...patch };
      saveSettings(next);
      return next;
    });
  }, []);

  return [settings, update];
}
