import { useEffect, useRef } from "react";

import { prefersReducedMotion } from "@/lib/motion";

interface ParallaxRegistration {
  node: HTMLElement;
  intensity: number;
}

const registry = new Set<ParallaxRegistration>();
let scrollListenerAttached = false;
let pendingFrame = 0;

function update() {
  pendingFrame = 0;
  const vh = window.innerHeight || 1;
  for (const reg of registry) {
    const rect = reg.node.getBoundingClientRect();
    if (rect.bottom < 0 || rect.top > vh) continue;
    // -1 (above viewport) → +1 (below viewport)
    const center = rect.top + rect.height / 2;
    const progress = (center - vh / 2) / (vh / 2 + rect.height / 2);
    const offset = -progress * reg.intensity;
    reg.node.style.setProperty("--parallax-y", `${offset.toFixed(1)}px`);
  }
}

function scheduleUpdate() {
  if (pendingFrame) return;
  pendingFrame = requestAnimationFrame(update);
}

function ensureListener() {
  if (scrollListenerAttached) return;
  scrollListenerAttached = true;
  window.addEventListener("scroll", scheduleUpdate, { passive: true });
  window.addEventListener("resize", scheduleUpdate, { passive: true });
}

export function useParallax<T extends HTMLElement = HTMLElement>(
  intensity = 16,
): React.RefObject<T | null> {
  const ref = useRef<T | null>(null);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (prefersReducedMotion()) {
      node.style.setProperty("--parallax-y", "0px");
      return;
    }
    const reg: ParallaxRegistration = { node, intensity };
    registry.add(reg);
    ensureListener();
    scheduleUpdate();
    return () => {
      registry.delete(reg);
    };
  }, [intensity]);

  return ref;
}
