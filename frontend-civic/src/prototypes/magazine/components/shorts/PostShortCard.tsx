import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { Flame, ThumbsUp, ThumbsDown, Quote } from "lucide-react";
import { cn } from "@/lib/cn";
import { TONE_META, toneColor, timeAgo } from "@/lib/campaignVisuals";
import {
  type CampaignPost,
  reactToPost,
  removePostReaction,
} from "@/api/campaign";
import { CandidateAvatar } from "../CandidateAvatar";
import { DisclaimerBadge } from "../DisclaimerBadge";

/**
 * Full-viewport Shorts card for a campaign post. The quick opinion is the post's
 * up/down reaction (which IS the vote in Civic) — anonymous-friendly via the
 * X-User-Id fallback, so no auth gate. Double-tapping the body is a shortcut for
 * Agree, mirroring the familiar "like" gesture. Optimistic-vote logic mirrors
 * CampaignPostCard.vote so the two stay consistent.
 */
export function PostShortCard({ post }: { post: CampaignPost }) {
  const candidate = post.candidate;
  const [up, setUp] = useState(post.up);
  const [down, setDown] = useState(post.down);
  const [mine, setMine] = useState<"up" | "down" | null>(null);
  const [busy, setBusy] = useState(false);
  const [pop, setPop] = useState(false);
  const lastTap = useRef(0);

  const tone = TONE_META[post.tone] ?? TONE_META.Casual;
  const accent = toneColor(post.tone);

  async function vote(type: "up" | "down") {
    if (busy) return;
    setBusy(true);
    try {
      const removing = mine === type;
      const res = removing
        ? await removePostReaction(post.id)
        : await reactToPost(post.id, type);
      setUp(res.postUp);
      setDown(res.postDown);
      setMine(removing ? null : type);
    } finally {
      setBusy(false);
    }
  }

  // Double-tap anywhere on the body = Agree (adds only; never toggles off).
  function onBodyTap() {
    const now = Date.now();
    if (now - lastTap.current < 300) {
      if (mine !== "up") void vote("up");
      setPop(true);
      window.setTimeout(() => setPop(false), 600);
    }
    lastTap.current = now;
  }

  const officeLine = candidate
    ? candidate.office === "President"
      ? "President"
      : candidate.office === "Senate"
        ? `Senate · ${candidate.state}`
        : `House · ${candidate.state}-${candidate.district}`
    : "";

  return (
    <div className="mx-auto flex h-full w-full max-w-xl flex-col px-5 pb-8 pt-20">
      <header className="flex items-center gap-3">
        {candidate && <CandidateAvatar candidate={candidate} size={48} />}
        <div className="min-w-0 flex-1">
          {candidate ? (
            <Link
              to={`/candidates/${candidate.slug}`}
              className="block truncate font-semibold text-[var(--fg)] hover:text-[var(--accent)]"
            >
              {candidate.name}
            </Link>
          ) : (
            <span className="font-semibold">Candidate</span>
          )}
          <span className="block truncate text-sm text-[var(--muted)]">
            {officeLine}
            {candidate && ` · ${candidate.party}`} · {timeAgo(post.createdAt)}
          </span>
        </div>
        <span
          className="inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider text-white"
          style={{ backgroundColor: accent }}
          data-testid="tone-chip"
        >
          {post.intensity >= 5 && <Flame className="h-3 w-3" />}
          {tone.label}
        </span>
      </header>

      {/* Body — the take. Centered, large, double-tap to Agree. */}
      <button
        type="button"
        onClick={onBodyTap}
        className="relative my-4 flex flex-1 cursor-pointer select-none items-center text-left"
        data-testid="short-post-body"
        aria-label="Double-tap to agree"
      >
        <p className="text-2xl font-medium leading-snug text-[var(--fg)]">
          {post.body}
        </p>
        {pop && (
          <ThumbsUp
            className="pointer-events-none absolute left-1/2 top-1/2 h-24 w-24 -translate-x-1/2 -translate-y-1/2 animate-ping text-[var(--accent)]"
            strokeWidth={1.5}
          />
        )}
      </button>

      {post.triggerBriefingSlug && (
        <Link
          to={`/briefings/${post.triggerBriefingSlug}`}
          className="mb-4 block border border-[var(--border)] bg-[var(--bg-elev)] p-3 transition hover:border-[var(--accent)]"
          data-testid="short-post-trigger"
        >
          <span className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">
            <Quote className="h-3 w-3 shrink-0" /> Responding to
          </span>
          <span className="mt-1 block font-semibold leading-snug text-[var(--fg)]">
            {post.triggerBriefingHeadline ?? post.triggerBriefingSlug}
          </span>
        </Link>
      )}

      {/* Quick opinion: Agree / Disagree. */}
      <div className="flex items-stretch gap-2">
        <button
          type="button"
          disabled={busy}
          onClick={() => vote("up")}
          data-testid="short-post-up"
          className={cn(
            "inline-flex flex-1 items-center justify-center gap-2 rounded-full border px-4 py-3.5 text-base font-semibold transition",
            mine === "up"
              ? "border-green-600 bg-green-50 text-green-700"
              : "border-[var(--border)] text-[var(--fg-soft)] hover:border-green-500",
          )}
        >
          <ThumbsUp className="h-5 w-5" /> Agree
          <span className="tabular-nums opacity-70">{up}</span>
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => vote("down")}
          data-testid="short-post-down"
          className={cn(
            "inline-flex flex-1 items-center justify-center gap-2 rounded-full border px-4 py-3.5 text-base font-semibold transition",
            mine === "down"
              ? "border-red-600 bg-red-50 text-red-700"
              : "border-[var(--border)] text-[var(--fg-soft)] hover:border-red-500",
          )}
        >
          <ThumbsDown className="h-5 w-5" /> Disagree
          <span className="tabular-nums opacity-70">{down}</span>
        </button>
      </div>
      <div className="mt-2 flex items-center justify-between">
        <DisclaimerBadge />
        <Link
          to={`/posts/${post.id}`}
          className="text-sm font-semibold text-[var(--accent)] hover:underline"
          data-testid="short-post-open"
        >
          Open →
        </Link>
      </div>
    </div>
  );
}
