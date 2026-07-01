import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { X } from "lucide-react";
import { listProvisions, type ProvisionSummary } from "@/api/coalition";
import { getBriefings, type BriefingPage } from "@/api/briefings";
import { fetchBudgetFacts, type BudgetFact } from "@/api/budgetFacts";
import {
  buildFeed,
  initialMixerState,
  type MixerState,
  type ShortItem,
  type ShortsPools,
} from "@/lib/shortsFeed";
import { ShortCard } from "../components/shorts/ShortCard";
import "../theme.css";

// News briefings are the paginated fact spine that keeps the feed going. Budget facts and
// coalition provisions are finite and loaded once up front.
const BRIEFINGS_PAGE_SIZE = 20;

const emptyBriefings: BriefingPage = { items: [], total: 0, page: 1, pageSize: 0 };

/**
 * The casual, mobile-first "Shorts" feed: a full-screen, vertically snap-scrolling mix of
 * short civic content that leads with interesting facts (budget + news) and weaves in
 * reflective prompts (think-deeper, coalition provisions). Candidate campaign posts are
 * intentionally excluded — they only matter to Campaign Managers. The feed keeps going by
 * paging through news briefings; each page's facts are mixed in client-side via buildFeed.
 * Lives outside MagazineLayout for a full-bleed viewport.
 */
export default function ShortsFeed() {
  const navigate = useNavigate();
  const [items, setItems] = useState<ShortItem[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "empty">("loading");
  const [loadingMore, setLoadingMore] = useState(false);

  const poolsRef = useRef<ShortsPools>({ coalition: [], thinkDeeper: [], news: [], budget: [] });
  const mixerRef = useRef<MixerState>({ ...initialMixerState });
  const nextPageRef = useRef(1);
  const hasMoreRef = useRef(true);
  const scrollRef = useRef<HTMLDivElement>(null);
  const sentinelRef = useRef<HTMLDivElement>(null);

  // Fetch the next briefings page, fold it into the pools, and return the newly-mixed items.
  // Briefings split into news (fact cards, carry an upstream publisher) and think-deeper prompts
  // (reflective fillers) — partitioned so a briefing never shows twice. Fillers only flush on the
  // final page so they keep weaving between facts rather than clumping at a page boundary.
  const loadBriefingsPage = useCallback(async (): Promise<ShortItem[]> => {
    if (!hasMoreRef.current) return [];
    const page = await getBriefings(nextPageRef.current, BRIEFINGS_PAGE_SIZE).catch(() => emptyBriefings);
    const news = page.items.filter((b) => b.sourcePublisher?.trim());
    const thinkDeeper = page.items.filter(
      (b) => b.thinkDeeperQuestion?.trim() && !b.sourcePublisher?.trim(),
    );
    poolsRef.current.news.push(...news);
    poolsRef.current.thinkDeeper.push(...thinkDeeper);
    nextPageRef.current += 1;
    hasMoreRef.current = page.pageSize > 0 && page.page * page.pageSize < page.total;
    return buildFeed(poolsRef.current, mixerRef.current, { flushFillers: !hasMoreRef.current });
  }, []);

  // Initial load. Coalition + budget facts are finite (fetched once); news is then paged in.
  // Each source failing independently just yields an empty pool rather than blanking the feed.
  useEffect(() => {
    let cancelled = false;
    setStatus("loading");
    void (async () => {
      const [coalition, budget] = await Promise.all([
        listProvisions().catch(() => [] as ProvisionSummary[]),
        fetchBudgetFacts().catch(() => [] as BudgetFact[]),
      ]);
      if (cancelled) return;
      poolsRef.current = { coalition, thinkDeeper: [], news: [], budget };
      mixerRef.current = { ...initialMixerState };
      nextPageRef.current = 1;
      hasMoreRef.current = true;

      // Page in briefings until there's something to show (guards the rare case where the first
      // page carries only think-deeper prompts and no budget facts) or the source is exhausted.
      const acc: ShortItem[] = [];
      while (!cancelled && acc.length === 0 && hasMoreRef.current) {
        acc.push(...(await loadBriefingsPage()));
      }
      if (cancelled) return;
      scrollRef.current?.scrollTo({ top: 0 });
      setItems(acc);
      setStatus(acc.length ? "ready" : "empty");
    })();
    return () => {
      cancelled = true;
    };
  }, [loadBriefingsPage]);

  const loadMore = useCallback(async () => {
    if (loadingMore || !hasMoreRef.current) return;
    setLoadingMore(true);
    try {
      const more = await loadBriefingsPage();
      if (more.length) setItems((prev) => prev.concat(more));
    } finally {
      setLoadingMore(false);
    }
  }, [loadingMore, loadBriefingsPage]);

  // Prefetch the next page roughly one viewport before the end.
  useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;
    const io = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) void loadMore();
      },
      { root: scrollRef.current, rootMargin: "0px 0px 100% 0px" },
    );
    io.observe(el);
    return () => io.disconnect();
  }, [loadMore, status]);

  return (
    <div className="theme-magazine relative h-[100dvh] overflow-hidden bg-[var(--bg)] text-[var(--fg)]">
      {/* Overlay chrome — stays put over the scrolling feed. Pads in the top
          safe-area inset so the close button + title clear the notch/status bar. */}
      <div
        data-testid="shorts-header"
        className="pointer-events-none absolute inset-x-0 top-0 z-40 flex items-center justify-between gap-2 bg-gradient-to-b from-[var(--bg)] to-transparent px-4 pb-3"
        style={{ paddingTop: "max(0.75rem, env(safe-area-inset-top))" }}
      >
        <button
          type="button"
          onClick={() => navigate("/")}
          aria-label="Close feed"
          data-testid="shorts-close"
          className="pointer-events-auto inline-flex h-9 w-9 items-center justify-center rounded-full border border-[var(--border)] bg-[var(--bg-elev)] text-[var(--fg-soft)] transition hover:text-[var(--fg)]"
        >
          <X className="h-5 w-5" />
        </button>
        <Link
          to="/"
          className="pointer-events-auto display text-lg tracking-tight text-[var(--accent)]"
        >
          Shorts
        </Link>
        {/* Spacer to keep the title centered now that the sort toggle is gone. */}
        <div className="h-9 w-9" aria-hidden />
      </div>

      {status === "loading" && (
        <div className="flex h-full items-center justify-center text-sm text-[var(--muted)]">
          Loading the feed…
        </div>
      )}

      {status === "empty" && (
        <div className="flex h-full flex-col items-center justify-center gap-3 px-8 text-center">
          <p className="text-[var(--fg-soft)]">No short content right now.</p>
          <button
            type="button"
            onClick={() => navigate("/")}
            className="text-sm font-semibold text-[var(--accent)] hover:underline"
          >
            Back to Civersify →
          </button>
        </div>
      )}

      {status === "ready" && (
        <div
          ref={scrollRef}
          data-testid="shorts-scroll"
          className="h-full snap-y snap-mandatory overflow-y-scroll overscroll-contain"
        >
          {items.map((item) => (
            <div key={item.key} className="h-[100dvh] w-full snap-start snap-always">
              <ShortCard item={item} />
            </div>
          ))}
          <div ref={sentinelRef} aria-hidden className="h-px w-full" />
        </div>
      )}
    </div>
  );
}
