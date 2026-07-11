import { useEffect, useRef, useState, type ReactNode } from "react";

/**
 * Defers mounting its children until the placeholder scrolls near the viewport,
 * so below-the-fold sections don't fire their data fetches in the initial page
 * load burst. On a single-core backend those parallel calls serialize, so keeping
 * the slowest, furthest-down ones out of the first wave lets above-the-fold
 * content (the cover story, the feature tile) paint sooner. Once visible the
 * children mount and stay mounted.
 *
 * Degrades to rendering immediately where IntersectionObserver is unavailable
 * (jsdom in unit tests, very old browsers) — i.e. back to the original eager path.
 */
export function WhenVisible({
  children,
  rootMargin = "200px",
  minHeight = 0,
}: {
  children: ReactNode;
  /** How far before entering the viewport to mount (a prefetch margin, so the
   *  content is usually loaded by the time the user reaches it). */
  rootMargin?: string;
  /** Reserve vertical space while deferred, to limit layout shift on mount. */
  minHeight?: number;
}) {
  const ref = useRef<HTMLDivElement | null>(null);
  const [visible, setVisible] = useState(
    typeof IntersectionObserver === "undefined",
  );

  useEffect(() => {
    if (visible) return;
    const el = ref.current;
    if (!el) return;
    const io = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          setVisible(true);
          io.disconnect();
        }
      },
      { rootMargin },
    );
    io.observe(el);
    return () => io.disconnect();
  }, [visible, rootMargin]);

  if (visible) return <>{children}</>;
  return (
    <div ref={ref} aria-hidden style={minHeight ? { minHeight } : undefined} />
  );
}
