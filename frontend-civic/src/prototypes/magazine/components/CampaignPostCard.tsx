import { useState } from "react";
import { Link } from "react-router-dom";
import { Flame, ThumbsUp, ThumbsDown, Highlighter, Quote } from "lucide-react";
import { cn } from "@/lib/cn";
import {
  TONE_META,
  toneColor,
  intensityBorderWidth,
  netSentimentColor,
} from "@/lib/campaignVisuals";
import {
  type CampaignPost,
  type PostFragment,
  reactToPost,
  removePostReaction,
  reactToFragment,
} from "@/api/campaign";
import { CandidateAvatar } from "./CandidateAvatar";
import { DisclaimerBadge } from "./DisclaimerBadge";

function timeAgo(iso: string): string {
  const secs = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (secs < 60) return "now";
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

type FragState = Record<string, { up: number; down: number; net: number }>;

function initialFragState(fragments: PostFragment[]): FragState {
  const state: FragState = {};
  for (const f of fragments) {
    const total = f.up + f.down;
    state[f.id] = { up: f.up, down: f.down, net: total === 0 ? 0 : (f.up - f.down) / total };
  }
  return state;
}

/** Renders the body as fragment spans (heat-map tinted, tappable) plus the gaps between them. */
function HeatBody({
  post,
  fragState,
  activeId,
  onSelect,
}: {
  post: CampaignPost;
  fragState: FragState;
  activeId: string | null;
  onSelect: (id: string) => void;
}) {
  const frags = [...post.fragments].sort((a, b) => a.start - b.start);
  const nodes: React.ReactNode[] = [];
  let cursor = 0;
  frags.forEach((f) => {
    if (f.start > cursor) nodes.push(<span key={`gap-${f.id}`}>{post.body.slice(cursor, f.start)}</span>);
    const fs = fragState[f.id] ?? { up: 0, down: 0, net: 0 };
    nodes.push(
      <button
        key={f.id}
        type="button"
        onClick={() => onSelect(f.id)}
        className={cn(
          "rounded px-0.5 text-left transition",
          activeId === f.id && "ring-2 ring-indigo-400",
        )}
        style={{ backgroundColor: netSentimentColor(fs.net) }}
        data-testid="fragment-span"
        title={`👍 ${fs.up} · 👎 ${fs.down}`}
      >
        {post.body.slice(f.start, f.end)}
      </button>,
    );
    cursor = Math.max(cursor, f.end);
  });
  if (cursor < post.body.length) nodes.push(<span key="tail">{post.body.slice(cursor)}</span>);
  return <p className="text-lg leading-relaxed text-[var(--fg)]">{nodes}</p>;
}

export function CampaignPostCard({
  post,
  showCompare = true,
}: {
  post: CampaignPost;
  showCompare?: boolean;
}) {
  const candidate = post.candidate;
  const [up, setUp] = useState(post.up);
  const [down, setDown] = useState(post.down);
  const [mine, setMine] = useState<"up" | "down" | null>(null);
  const [heatOn, setHeatOn] = useState(false);
  const [fragState, setFragState] = useState<FragState>(() => initialFragState(post.fragments));
  const [activeFrag, setActiveFrag] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const tone = TONE_META[post.tone] ?? TONE_META.Casual;
  const accent = toneColor(post.tone);

  async function vote(type: "up" | "down") {
    if (busy) return;
    setBusy(true);
    try {
      const res = mine === type ? await removePostReaction(post.id) : await reactToPost(post.id, type);
      setUp(res.postUp);
      setDown(res.postDown);
      setMine(mine === type ? null : type);
    } finally {
      setBusy(false);
    }
  }

  async function voteFragment(fragmentId: string, type: "up" | "down") {
    const res = await reactToFragment(post.id, fragmentId, type);
    if (res.fragmentUp != null && res.fragmentDown != null) {
      const total = res.fragmentUp + res.fragmentDown;
      setFragState((s) => ({
        ...s,
        [fragmentId]: {
          up: res.fragmentUp!,
          down: res.fragmentDown!,
          net: total === 0 ? 0 : (res.fragmentUp! - res.fragmentDown!) / total,
        },
      }));
    }
  }

  const officeLine = candidate
    ? candidate.office === "President"
      ? "President"
      : candidate.office === "Senate"
        ? `Senate · ${candidate.state}`
        : `House · ${candidate.state}-${candidate.district}`
    : "";

  return (
    <article
      className="border bg-[var(--bg-elev)] p-5"
      style={{ borderColor: accent, borderLeftWidth: intensityBorderWidth(post.intensity) + 2 }}
      data-testid="campaign-post-card"
    >
      <header className="flex items-start gap-3">
        {candidate && <CandidateAvatar candidate={candidate} />}
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            {candidate ? (
              <Link
                to={`/candidates/${candidate.slug}`}
                className="font-semibold text-[var(--fg)] hover:text-[var(--accent)]"
              >
                {candidate.name}
              </Link>
            ) : (
              <span className="font-semibold">Candidate</span>
            )}
            <span className="text-sm text-[var(--muted)]">
              {officeLine}
              {candidate && ` · ${candidate.party}`} · {timeAgo(post.createdAt)}
            </span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <span
              className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider text-white"
              style={{ backgroundColor: accent }}
              data-testid="tone-chip"
            >
              {post.intensity >= 5 && <Flame className="h-3 w-3" />}
              {post.intensityLabel} · {tone.label}
            </span>
            <DisclaimerBadge />
          </div>
        </div>
      </header>

      <div className="mt-4">
        {heatOn ? (
          <HeatBody post={post} fragState={fragState} activeId={activeFrag} onSelect={setActiveFrag} />
        ) : (
          <p className="text-lg leading-relaxed text-[var(--fg)]">{post.body}</p>
        )}
      </div>

      {heatOn && activeFrag && (
        <div
          className="mt-3 flex items-center gap-2 rounded border border-[var(--border)] bg-[var(--bg)] p-2 text-sm"
          data-testid="fragment-react-bar"
        >
          <span className="truncate text-[var(--muted)]">React to this line:</span>
          <button
            type="button"
            onClick={() => voteFragment(activeFrag, "up")}
            className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-2 py-0.5 hover:border-green-500"
            data-testid="fragment-up"
          >
            <ThumbsUp className="h-3 w-3" /> {fragState[activeFrag]?.up ?? 0}
          </button>
          <button
            type="button"
            onClick={() => voteFragment(activeFrag, "down")}
            className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-2 py-0.5 hover:border-red-500"
            data-testid="fragment-down"
          >
            <ThumbsDown className="h-3 w-3" /> {fragState[activeFrag]?.down ?? 0}
          </button>
        </div>
      )}

      {post.triggerBriefingSlug && (
        <Link
          to={`/briefings/${post.triggerBriefingSlug}`}
          className="mt-4 flex items-start gap-2 border-l-2 border-[var(--border)] pl-3 text-sm text-[var(--muted)] hover:text-[var(--fg)]"
          data-testid="post-trigger-briefing"
        >
          <Quote className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>
            Reacting to: {post.triggerBriefingHeadline ?? post.triggerBriefingSlug}
          </span>
        </Link>
      )}

      {post.citedReference && (
        <p className="mt-2 text-xs text-[var(--muted)]" data-testid="post-cited-reference">
          Source: {post.citedReference}
        </p>
      )}

      <footer className="mt-4 flex flex-wrap items-center gap-2">
        <button
          type="button"
          disabled={busy}
          onClick={() => vote("up")}
          className={cn(
            "inline-flex items-center gap-1 rounded-full border px-3 py-1 text-sm font-semibold transition",
            mine === "up"
              ? "border-green-600 bg-green-50 text-green-700"
              : "border-[var(--border)] text-[var(--fg-soft)] hover:border-green-500",
          )}
          data-testid="post-up"
        >
          <ThumbsUp className="h-4 w-4" /> {up}
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => vote("down")}
          className={cn(
            "inline-flex items-center gap-1 rounded-full border px-3 py-1 text-sm font-semibold transition",
            mine === "down"
              ? "border-red-600 bg-red-50 text-red-700"
              : "border-[var(--border)] text-[var(--fg-soft)] hover:border-red-500",
          )}
          data-testid="post-down"
        >
          <ThumbsDown className="h-4 w-4" /> {down}
        </button>
        <button
          type="button"
          onClick={() => setHeatOn((v) => !v)}
          className={cn(
            "inline-flex items-center gap-1 rounded-full border px-3 py-1 text-sm font-semibold transition",
            heatOn
              ? "border-indigo-500 bg-indigo-50 text-indigo-700"
              : "border-[var(--border)] text-[var(--fg-soft)] hover:border-indigo-400",
          )}
          data-testid="heatmap-toggle"
        >
          <Highlighter className="h-4 w-4" /> Heat map
        </button>
        {showCompare && (
          <Link
            to={`/posts/${post.id}`}
            className="ml-auto text-sm font-semibold text-[var(--accent)] hover:underline"
            data-testid="post-open"
          >
            Open →
          </Link>
        )}
      </footer>
    </article>
  );
}
