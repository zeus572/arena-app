import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import {
  type CampaignPost,
  type PostHeatmap,
  type HeatmapFragment,
  getPost,
  getPostHeatmap,
} from "@/api/campaign";
import { CampaignPostCard } from "../components/CampaignPostCard";

function mostReacted(heatmap: PostHeatmap | null) {
  if (!heatmap) return { loved: null, disliked: null };
  const reacted = heatmap.fragments.filter((f) => f.up + f.down > 0);
  if (reacted.length === 0) return { loved: null, disliked: null };
  const loved = [...reacted].sort((a, b) => b.net - a.net)[0];
  const disliked = [...reacted].sort((a, b) => a.net - b.net)[0];
  return { loved, disliked: disliked.id === loved.id ? null : disliked };
}

function LineInsight({ label, fragment, kind }: { label: string; fragment: HeatmapFragment; kind: "loved" | "disliked" }) {
  return (
    <div
      className={`border-l-4 p-4 ${kind === "loved" ? "border-green-500 bg-green-50" : "border-red-500 bg-red-50"}`}
      data-testid={`insight-${kind}`}
    >
      <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">{label}</p>
      <p className="mt-1 italic text-[var(--fg)]">"{fragment.text}"</p>
      <p className="mt-1 text-xs text-[var(--muted)]">
        👍 {fragment.up} · 👎 {fragment.down}
      </p>
    </div>
  );
}

export default function PostDetail() {
  const { id } = useParams();
  const [post, setPost] = useState<CampaignPost | null>(null);
  const [heatmap, setHeatmap] = useState<PostHeatmap | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!id) return;
    setLoaded(false);
    void getPost(id).then((p) => setPost(p ?? null));
    void getPostHeatmap(id)
      .then(setHeatmap)
      .catch(() => setHeatmap(null))
      .finally(() => setLoaded(true));
  }, [id]);

  if (!loaded && !post) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading…
      </p>
    );
  }

  if (!post) {
    return (
      <div className="py-16 text-center" data-testid="post-not-found">
        <h1 className="display text-4xl">That post isn't here.</h1>
        <Link to="/candidates" className="mt-6 inline-block text-sm font-semibold text-[var(--accent)]">
          ← Back to the Campaign Feed
        </Link>
      </div>
    );
  }

  const { loved, disliked } = mostReacted(heatmap);

  return (
    <div data-testid="post-detail-page">
      <Link
        to="/candidates"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Campaign Feed
      </Link>

      <div className="mt-6">
        <CampaignPostCard post={post} showCompare={false} />
      </div>

      <p className="mt-3 text-sm text-[var(--muted)]">
        Toggle the heat map above, then tap any line to react to it.
      </p>

      {(loved || disliked) && (
        <section className="mt-8 grid gap-4 md:grid-cols-2" data-testid="line-insights">
          {loved && <LineInsight label="Most agreed-with line" fragment={loved} kind="loved" />}
          {disliked && <LineInsight label="Most disagreed-with line" fragment={disliked} kind="disliked" />}
        </section>
      )}
    </div>
  );
}
