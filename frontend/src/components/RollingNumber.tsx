import { useEffect, useRef, useState } from "react";

import { useInView } from "@/hooks/useInView";
import { prefersReducedMotion } from "@/lib/motion";

interface RollingNumberProps {
  value: number;
  durationMs?: number;
  className?: string;
}

export function RollingNumber({ value, durationMs = 600, className }: RollingNumberProps) {
  const [ref, inView] = useInView<HTMLSpanElement>({ threshold: 0.4 });
  const [displayed, setDisplayed] = useState(prefersReducedMotion() ? value : 0);
  const animatedRef = useRef(false);

  useEffect(() => {
    if (!inView) return;
    if (animatedRef.current) {
      setDisplayed(value);
      return;
    }
    animatedRef.current = true;
    if (prefersReducedMotion()) {
      setDisplayed(value);
      return;
    }
    const start = performance.now();
    const from = 0;
    const to = value;
    if (to === from) return;
    let raf = 0;
    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / durationMs);
      const eased = 1 - Math.pow(1 - t, 3);
      setDisplayed(Math.round(from + (to - from) * eased));
      if (t < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [inView, value, durationMs]);

  return (
    <span ref={ref} className={className}>
      {displayed}
    </span>
  );
}
