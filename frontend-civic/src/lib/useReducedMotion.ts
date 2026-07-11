import { useSyncExternalStore } from "react";

// Reactive read of the OS "reduce motion" accessibility setting. Components that
// animate imperatively (rAF count-ups, ring sweeps) can't lean on the CSS
// `@media (prefers-reduced-motion)` guard, so they check this and skip straight
// to the final value instead.
//
// Uses useSyncExternalStore so the value stays correct if the user flips the
// setting while the app is open (rare, but free to support).

const QUERY = "(prefers-reduced-motion: reduce)";

function subscribe(onChange: () => void): () => void {
  if (typeof window === "undefined" || !window.matchMedia) return () => {};
  const mql = window.matchMedia(QUERY);
  mql.addEventListener("change", onChange);
  return () => mql.removeEventListener("change", onChange);
}

function getSnapshot(): boolean {
  if (typeof window === "undefined" || !window.matchMedia) return false;
  return window.matchMedia(QUERY).matches;
}

export function usePrefersReducedMotion(): boolean {
  // Server snapshot is always false — there's no motion preference to honor
  // before hydration, and the client value takes over immediately on mount.
  return useSyncExternalStore(subscribe, getSnapshot, () => false);
}
