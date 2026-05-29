import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import {
  type CandidateDetail,
  type CampaignPost,
  type CandidateValue,
  getCandidate,
  getCandidatePosts,
  followCandidate,
  unfollowCandidate,
  muteCandidate,
  unmuteCandidate,
} from "@/api/campaign";
import { CampaignPostCard } from "../components/CampaignPostCard";
import { CandidateAvatar } from "../components/CandidateAvatar";
import { DisclaimerBadge } from "../components/DisclaimerBadge";

function ValueBar({ value }: { value: CandidateValue }) {
  // score in [-1,1] -> 0..100% across the axis
  const pct = ((value.score + 1) / 2) * 100;
  return (
    <li data-testid="value-axis">
      <div className="flex items-baseline justify-between text-xs text-[var(--muted)]">
        <span>{value.lowLabel}</span>
        <span className="font-semibold text-[var(--fg-soft)]">{value.axisName}</span>
        <span>{value.highLabel}</span>
      </div>
      <div className="relative mt-1 h-2 rounded-full bg-[var(--border)]">
        <span
          className="absolute top-1/2 h-3 w-3 -translate-x-1/2 -translate-y-1/2 rounded-full bg-[var(--accent)]"
          style={{ left: `${pct}%` }}
        />
      </div>
    </li>
  );
}

export default function CandidateProfile() {
  const { slug } = useParams();
  const [candidate, setCandidate] = useState<CandidateDetail | null>(null);
  const [posts, setPosts] = useState<CampaignPost[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [following, setFollowing] = useState(false);
  const [muted, setMuted] = useState(false);

  useEffect(() => {
    if (!slug) return;
    setLoaded(false);
    void getCandidate(slug).then((c) => setCandidate(c ?? null));
    void getCandidatePosts(slug)
      .then((f) => setPosts(f.items))
      .finally(() => setLoaded(true));
  }, [slug]);

  if (!loaded && !candidate) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading…
      </p>
    );
  }

  if (!candidate) {
    return (
      <div className="py-16 text-center" data-testid="candidate-not-found">
        <h1 className="display text-4xl">No such candidate.</h1>
        <Link to="/candidates" className="mt-6 inline-block text-sm font-semibold text-[var(--accent)]">
          ← Back to the Campaign Feed
        </Link>
      </div>
    );
  }

  async function toggleFollow() {
    if (!slug) return;
    if (following) await unfollowCandidate(slug);
    else await followCandidate(slug);
    setFollowing(!following);
  }

  async function toggleMute() {
    if (!slug) return;
    if (muted) await unmuteCandidate(slug);
    else await muteCandidate(slug);
    setMuted(!muted);
  }

  const officeLine =
    candidate.office === "President"
      ? "Running for President"
      : candidate.office === "Senate"
        ? `Running for U.S. Senate · ${candidate.state}`
        : `Running for U.S. House · ${candidate.state}-${candidate.district}`;

  return (
    <article data-testid="candidate-profile">
      <Link
        to="/candidates"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Campaign Feed
      </Link>

      <header className="mt-6 flex flex-col gap-4 sm:flex-row sm:items-start">
        <CandidateAvatar candidate={candidate} size={72} />
        <div className="flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="display text-4xl">{candidate.name}</h1>
            <DisclaimerBadge />
          </div>
          <p className="mt-1 text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">
            {candidate.party} · {officeLine}
            {candidate.isIncumbent && " · Incumbent"}
          </p>
          <p className="mt-3 text-lg leading-relaxed text-[var(--fg-soft)]">{candidate.bio}</p>
          <div className="mt-4 flex gap-2">
            <button
              type="button"
              onClick={toggleFollow}
              className={`rounded-full px-4 py-1.5 text-sm font-semibold ${
                following
                  ? "border border-[var(--border)] text-[var(--fg-soft)]"
                  : "bg-[var(--accent)] text-white"
              }`}
              data-testid="follow-button"
            >
              {following ? "Following" : "Follow"}
            </button>
            <button
              type="button"
              onClick={toggleMute}
              className="rounded-full border border-[var(--border)] px-4 py-1.5 text-sm font-semibold text-[var(--fg-soft)]"
              data-testid="mute-button"
            >
              {muted ? "Muted" : "Mute"}
            </button>
          </div>
        </div>
      </header>

      <section className="mt-10">
        <h2 className="display text-2xl">Background</h2>
        <p className="mt-3 leading-relaxed text-[var(--fg-soft)]">{candidate.background}</p>
      </section>

      <section className="mt-10">
        <h2 className="display text-2xl">Where they stand</h2>
        <ul className="mt-4 space-y-4">
          {candidate.values.map((v) => (
            <ValueBar key={v.axisKey} value={v} />
          ))}
        </ul>
      </section>

      <section className="mt-10">
        <h2 className="display text-2xl">Platform</h2>
        <ul className="mt-4 grid gap-4 md:grid-cols-2">
          {candidate.platformPlanks.map((p) => (
            <li key={p.id} className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="plank">
              <p className="font-semibold text-[var(--fg)]">{p.title}</p>
              <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">{p.body}</p>
              <p className="mt-2 text-xs uppercase tracking-wider text-[var(--muted)]">
                {p.issueTags.join(" · ")}
              </p>
            </li>
          ))}
        </ul>
      </section>

      <section className="mt-10">
        <div className="flex items-center justify-between">
          <h2 className="display text-2xl">Recent posts</h2>
          <Link
            to={`/candidates/${candidate.slug}/sources`}
            className="text-sm font-semibold text-[var(--accent)] hover:underline"
            data-testid="view-sources"
          >
            View source library →
          </Link>
        </div>
        <div className="mt-4 space-y-5">
          {posts.length === 0 ? (
            <p className="text-base text-[var(--muted)]" data-testid="no-posts">
              {candidate.name} hasn't posted yet. When today's headlines land, watch this space.
            </p>
          ) : (
            posts.map((p) => <CampaignPostCard key={p.id} post={p} />)
          )}
        </div>
      </section>
    </article>
  );
}
