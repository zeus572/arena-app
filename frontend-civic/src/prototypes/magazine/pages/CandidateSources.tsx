import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { type CandidateSource, getCandidateSources } from "@/api/campaign";

export default function CandidateSources() {
  const { slug } = useParams();
  const [sources, setSources] = useState<CandidateSource[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!slug) return;
    void getCandidateSources(slug)
      .then(setSources)
      .finally(() => setLoaded(true));
  }, [slug]);

  return (
    <div data-testid="candidate-sources-page">
      <Link
        to={`/candidates/${slug}`}
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to candidate
      </Link>
      <h1 className="display mt-6 text-4xl">Source library</h1>
      <p className="mt-2 max-w-2xl text-sm text-[var(--muted)]">
        Every post this candidate makes traces back to one of these artifacts — speeches, op-eds, and
        policy documents their fictional communications team would draw on.
      </p>

      {!loaded && (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Loading…
        </p>
      )}

      <ul className="mt-6 space-y-4">
        {sources.map((s) => (
          <li key={s.id} className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="source-item">
            <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
              {s.kind} · Priority {s.priority}
            </p>
            <p className="mt-1 font-semibold text-[var(--fg)]">{s.title}</p>
            <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">{s.excerpt}</p>
            {s.issueTags.length > 0 && (
              <p className="mt-2 text-xs uppercase tracking-wider text-[var(--muted)]">
                {s.issueTags.join(" · ")}
              </p>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
