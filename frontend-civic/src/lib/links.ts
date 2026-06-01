// External destinations shared across the app.

/// The Debate Arena (debate floor) site. Override at build time with VITE_DEBATE_ARENA_URL.
export const DEBATE_ARENA_URL =
  import.meta.env.VITE_DEBATE_ARENA_URL ?? "https://www.debatearena.fun";
