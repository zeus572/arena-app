import { useEffect, useRef } from "react";

import { prefersReducedMotion, supportsHover } from "@/lib/motion";

export interface UseTiltOptions {
  maxTiltDeg?: number;
  glow?: boolean;
}

export function useTilt<T extends HTMLElement = HTMLElement>(
  options: UseTiltOptions = {},
): React.RefObject<T | null> {
  const { maxTiltDeg = 6, glow = true } = options;
  const ref = useRef<T | null>(null);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (prefersReducedMotion() || !supportsHover()) return;

    const setVars = (tx: number, ty: number, gx: number, gy: number, snap: boolean) => {
      node.style.setProperty("--tilt-x", `${tx.toFixed(2)}deg`);
      node.style.setProperty("--tilt-y", `${ty.toFixed(2)}deg`);
      if (glow) {
        node.style.setProperty("--glow-x", `${gx.toFixed(0)}%`);
        node.style.setProperty("--glow-y", `${gy.toFixed(0)}%`);
      }
      node.style.setProperty("--tilt-transition", snap ? "350ms" : "120ms");
    };

    let raf = 0;
    const handleMove = (e: PointerEvent) => {
      const rect = node.getBoundingClientRect();
      const px = (e.clientX - rect.left) / rect.width;
      const py = (e.clientY - rect.top) / rect.height;
      const tx = (px - 0.5) * 2 * maxTiltDeg;
      const ty = (0.5 - py) * 2 * maxTiltDeg;
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => setVars(tx, ty, px * 100, py * 100, false));
    };
    const handleLeave = () => {
      cancelAnimationFrame(raf);
      setVars(0, 0, 50, 50, true);
    };

    node.addEventListener("pointermove", handleMove);
    node.addEventListener("pointerleave", handleLeave);
    setVars(0, 0, 50, 50, true);
    return () => {
      cancelAnimationFrame(raf);
      node.removeEventListener("pointermove", handleMove);
      node.removeEventListener("pointerleave", handleLeave);
    };
  }, [maxTiltDeg, glow]);

  return ref;
}
