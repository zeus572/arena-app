import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { X } from "lucide-react";
import { cn } from "@/lib/cn";
import { getCampaignFeed, type CampaignFeed } from "@/api/campaign";
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

type SortMode = "trending" | "recent";
const PAGE = 15;

const emptyFeed: CampaignFeed = { items: [], nextCursor: null };
const emptyBriefings: BriefingPage = { items: [], total: 0, page: 1, pageSize: 0 };

/**
 * The casual, mobile-first "Shorts" feed: a full-screen, vertically snap-scrolling
 * mix of short civic content (campaign takes, coalition provisions, think-deeper
 * prompts, budget facts). Reuses existing public APIs end-to-end and mixes them
 * client-side via buildFeed. Lives outside MagazineLayout for a full-bleed viewport.
 */
export default function ShortsFeed() {
  const navigate = useNavigate();
  const [items, setItems] = useState<ShortItem[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "empty">("loading");
  const [sort, setSort] = useState<SortMode>("trending");
  const [loadingMore, setLoadingMore] = useState(false);

  const poolsRef = useRef<ShortsPools>({
    posts: [],
    coalition: [],
    thinkDeeper: [],
    news: [],
    budget: [],
  });
  const cursorRef = useRef<string | null>(null);
  const mixerRef = useRef<MixerState>({ ...initialMixerState });
  const scrollRef = useRef<HTMLDivElement>(null);
  const sentinelRef = useRef<HTMLDivElement>(null);

  // Initial load (and reload on sort change). Each source is independent — any one
  // failing just yields an empty pool rather than blanking the feed.
  useEffect(() => {
    let cancelled = false;
    setStatus("loading");
    void (async () => {
      const [postFeed, coalition, briefingPage, budget] = await Promise.all([
        getCampaignFeed({ sort, limit: PAGE }).catch(() => emptyFeed),
        listProvisions().catch(() => [] as ProvisionSummary[]),
        getBriefings(1, 30).catch(() => emptyBriefings),
        fetchBudgetFacts().catch(() => [] as BudgetFact[]),
      ]);
      if (cancelled) return;
      // News-sourced briefings (carry an upstream publisher) lead as fact cards; the
      // remaining briefings with a think-deeper question fill in as reflective prompts.
      // Partitioned so a briefing never shows twice.
      const news = briefingPage.items.filter((b) => b.sourcePublisher?.trim());
      const thinkDeeper = briefingPage.items.filter(
        (b) => b.thinkDeeperQuestion?.trim() && !b.sourcePublisher?.trim(),
      );
      const pools: ShortsPools = {
        posts: postFeed.items,
        coalition,
        thinkDeeper,
        news,
        budget,
      };
      poolsRef.current = pools;
      cursorRef.current = postFeed.nextCursor;
      mixerRef.current = { ...initialMixerState };
      const built = buildFeed(pools.posts, pools, mixerRef.current);
      scrollRef.current?.scrollTo({ top: 0 });
      setItems(built);
      setStatus(built.length ? "ready" : "empty");
    })();
    return () => {
      cancelled = true;
    };
  }, [sort]);

  const loadMore = useCallback(async () => {
    if (loadingMore || cursorRef.current == null) return;
    setLoadingMore(true);
    try {
      const feed = await getCampaignFeed({
        sort,
        limit: PAGE,
        cursor: cursorRef.current,
      });
      cursorRef.current = feed.nextCursor;
      poolsRef.current.posts = poolsRef.current.posts.concat(feed.items);
      const more = buildFeed(feed.items, poolsRef.current, mixerRef.current);
      if (more.length) setItems((prev) => prev.concat(more));
    } catch {
      // Transient — the observer will retry as the user keeps scrolling.
    } finally {
      setLoadingMore(false);
    }
  }, [sort, loadingMore]);

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
      {/* Overlay chrome — stays put over the scrolling feed. */}
      <div className="pointer-events-none absolute inset-x-0 top-0 z-40 flex items-center justify-between gap-2 bg-gradient-to-b from-[var(--bg)] to-transparent px-4 py-3">
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
        <div className="pointer-events-auto flex rounded-full border border-[var(--border)] bg-[var(--bg-elev)] p-0.5 text-[11px] font-semibold uppercase tracking-wider">
          {(["trending", "recent"] as const).map((mode) => (
            <button
              key={mode}
              type="button"
              onClick={() => setSort(mode)}
              data-testid={`shorts-sort-${mode}`}
              className={cn(
                "rounded-full px-3 py-1 transition",
                sort === mode
                  ? "bg-[var(--accent)] text-white"
                  : "text-[var(--muted)] hover:text-[var(--fg)]",
              )}
            >
              {mode}
            </button>
          ))}
        </div>
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
