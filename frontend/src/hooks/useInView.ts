import { useEffect, useRef, useState } from "react";

import { prefersReducedMotion } from "@/lib/motion";

export interface UseInViewOptions {
  rootMargin?: string;
  threshold?: number;
  once?: boolean;
}

export function useInView<T extends Element = HTMLElement>(
  options: UseInViewOptions = {},
): [React.RefObject<T | null>, boolean] {
  const { rootMargin = "0px 0px -10% 0px", threshold = 0.15, once = true } = options;
  const ref = useRef<T | null>(null);
  const [inView, setInView] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (prefersReducedMotion() || typeof IntersectionObserver === "undefined") {
      setInView(true);
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            setInView(true);
            if (once) observer.unobserve(entry.target);
          } else if (!once) {
            setInView(false);
          }
        }
      },
      { rootMargin, threshold },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [rootMargin, threshold, once]);

  return [ref, inView];
}
