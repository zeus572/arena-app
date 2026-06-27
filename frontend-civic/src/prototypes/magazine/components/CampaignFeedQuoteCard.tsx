import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Quote } from "lucide-react";
import { getCampaignFeed, type CampaignPost } from "@/api/campaign";

/* A live pull-quote from the Campaign Feed, to nudge readers toward /candidates.
   Replaces the old hardcoded "Words you need to know" PullQuote: instead of static
   copy, it surfaces a top fictional-candidate post so the home always feels fresh
   and points at the Campaign Feed. We pick the strongest fragment (the lines people
   actually react to) when one exists, else the post body. Renders nothing until a
   post loads, so the home never shows an empty frame. */
export function CampaignFeedQuoteCard() {
  const [post, setPost] = useState<CampaignPost | null>(null);

  useEffect(() => {
    void getCampaignFeed({ sort: "top", limit: 10 })
      .then((feed) => setPost(feed.items.find((p) => p.candidate) ?? feed.items[0] ?? null))
      .catch(() => {});
  }, []);

  if (!post) return null;

  // The fragment with the highest net reaction is the line that moved people; fall
  // back to the whole post body when a post hasn't been fragment-reacted to yet.
  const topFragment = post.fragments.length
    ? [...post.fragments].sort((a, b) => b.up - b.down - (a.up - a.down))[0]
    : null;
  const quote = topFragment && topFragment.up - topFragment.down > 0 ? topFragment.text : post.body;
  const c = post.candidate;

  return (
    <section className="mt-16" data-testid="campaign-feed-quote">
      <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
        From the Campaign Feed
      </p>
      <Link
        to="/candidates"
        className="group mt-4 block border border-[var(--border)] bg-[var(--bg-elev)] p-8 transition hover:border-[var(--accent)]"
        data-testid="campaign-feed-quote-link"
      >
        <Quote className="h-5 w-5 text-[var(--accent)]" aria-hidden />
        <blockquote className="mt-3 line-clamp-4 text-base leading-relaxed text-[var(--fg)] group-hover:text-[var(--accent)] md:text-lg">
          "{quote}"
        </blockquote>
        {c && (
          <p className="mt-4 text-sm font-semibold text-[var(--fg-soft)]">
            — {c.name}
            <span className="font-normal text-[var(--muted)]">
              {" · "}{c.party}
              {c.office === "President"
                ? " · for President"
                : c.state
                  ? ` · ${c.office} · ${c.state}`
                  : ` · ${c.office}`}
            </span>
          </p>
        )}
        <p className="mt-4 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] group-hover:text-[var(--accent)]">
          Open the Campaign Feed →
        </p>
      </Link>
    </section>
  );
}
