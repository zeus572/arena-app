import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Loader2, ArrowLeft } from "lucide-react";
import {
  getNewsResponsePage,
  takeAction,
  type NewsResponsePage,
  type CandidateValue,
} from "@/api/campaignManager";
import { CandidateAvatar } from "../components/CandidateAvatar";

function ValueBar({ value }: { value: CandidateValue }) {
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

export default function CampaignNewsResponse() {
  const { id, slug } = useParams();
  const navigate = useNavigate();
  const [page, setPage] = useState<NewsResponsePage | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id || !slug) return;
    try {
      const data = await getNewsResponsePage(id, slug);
      setPage(data);
    } catch (err) {
      setError(errorMessage(err));
    }
  }, [id, slug]);

  useEffect(() => {
    setLoaded(false);
    void load().finally(() => setLoaded(true));
  }, [load]);

  async function choose(optionId: string) {
    if (!id || !slug || submitting) return;
    setSubmitting(optionId);
    setError(null);
    try {
      await takeAction(id, {
        actionType: "RespondToNews",
        briefingSlug: slug,
        optionId,
      });
      // Published to the candidate's (tailored) feed — return to the dashboard.
      navigate(`/campaigns/${id}`);
    } catch (err) {
      setError(errorMessage(err));
      setSubmitting(null);
    }
  }

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading…
      </p>
    );
  }

  if (!page) {
    return (
      <div className="py-16 text-center" data-testid="response-not-found">
        <h1 className="display text-4xl">News item not found.</h1>
        <Link to={`/campaigns/${id}`} className="mt-6 inline-block text-sm font-semibold text-[var(--accent)]">
          ← Back to the campaign
        </Link>
      </div>
    );
  }

  const disabled = page.alreadyResponded || page.actionsRemaining <= 0;

  return (
    <section data-testid="news-response-page">
      <Link
        to={`/campaigns/${id}`}
        className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        <ArrowLeft className="h-3.5 w-3.5" /> Back to campaign
      </Link>

      {/* The news item */}
      <header className="mt-4">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">In the news</p>
        <h1 className="display mt-1 text-3xl">{page.headline}</h1>
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">{page.summary}</p>
        {page.valuesInConflict.length > 0 && (
          <p className="mt-2 text-xs uppercase tracking-wide text-[var(--muted)]">
            Values in conflict: {page.valuesInConflict.join(" · ")}
          </p>
        )}
      </header>

      {/* Candidate profile summary */}
      <section className="mt-8 border border-[var(--border)] bg-[var(--bg-elev)] p-4">
        <div className="flex items-center gap-3">
          <CandidateAvatar
            candidate={{
              slug: page.candidateSlug,
              name: page.candidateName,
              avatarBaseUrl: page.avatarBaseUrl,
            }}
            size={52}
          />
          <div>
            <p className="display text-xl">{page.candidateName}</p>
            <p className="text-sm text-[var(--fg-soft)]">{page.party}</p>
          </div>
        </div>
        <p className="mt-3 text-sm leading-relaxed text-[var(--fg-soft)]">{page.candidateBio}</p>
        {page.values.length > 0 && (
          <div className="mt-4">
            <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              Where they stand
            </p>
            <ul className="mt-2 space-y-3">
              {page.values.map((v) => (
                <ValueBar key={v.axisKey} value={v} />
              ))}
            </ul>
          </div>
        )}
      </section>

      {/* Response options */}
      <section className="mt-8">
        <h2 className="display text-2xl">How does {page.candidateName.split(" ")[0]} respond?</h2>
        <p className="mt-1 text-sm text-[var(--fg-soft)]">
          {page.alreadyResponded
            ? "You've already responded to this story."
            : page.actionsRemaining <= 0
              ? "No actions left today — advance to the next day to respond."
              : "Pick a response. It will post to your candidate's campaign feed."}
        </p>

        {error && (
          <p className="mt-3 text-sm font-semibold text-[var(--danger,#dc2626)]" data-testid="response-error">
            {error}
          </p>
        )}

        <ul className="mt-4 space-y-4" data-testid="response-options">
          {page.options.map((opt) => (
            <li
              key={opt.id}
              className="border border-[var(--border)] bg-[var(--bg-elev)] p-4"
              data-testid="response-option"
            >
              <div className="flex items-center justify-between gap-2">
                <p className="font-semibold text-[var(--fg)]">{opt.label}</p>
                <span className="rounded-full bg-[var(--border)] px-2 py-0.5 text-xs font-semibold text-[var(--fg-soft)]">
                  {opt.tone}
                </span>
              </div>
              {opt.angle && <p className="mt-1 text-sm text-[var(--muted)]">{opt.angle}</p>}

              {/* The actual post the candidate would publish */}
              <blockquote className="mt-3 border-l-2 border-[var(--accent)] pl-3 text-[var(--fg-soft)] italic">
                “{opt.body}”
              </blockquote>

              <button
                type="button"
                data-testid={`choose-${opt.id}`}
                disabled={disabled || submitting !== null}
                onClick={() => choose(opt.id)}
                className="mt-3 inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white disabled:opacity-50"
              >
                {submitting === opt.id && <Loader2 className="h-4 w-4 animate-spin" />}
                Post this response
              </button>
            </li>
          ))}
        </ul>
      </section>
    </section>
  );
}

function errorMessage(err: unknown): string {
  const msg = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
  return msg ?? "Something went wrong. Please try again.";
}
