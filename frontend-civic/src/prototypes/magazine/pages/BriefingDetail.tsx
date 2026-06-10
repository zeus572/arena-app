import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import type { CivicBriefing } from "@/api/types";
import { getBriefingBySlug } from "@/api/briefings";
import { requestDebateFromBriefing } from "@/api/debates";
import { useAuth } from "@/auth/AuthContext";
import { PullQuote } from "../components/PullQuote";
import { SharePreviewCard } from "../components/SharePreviewCard";
import { Button } from "../components/Button";

export default function MagazineBriefingDetail() {
  const { slug } = useParams();
  const { user } = useAuth();
  const isPremium = user?.plan === "Premium";
  const [briefing, setBriefing] = useState<CivicBriefing | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [debateBusy, setDebateBusy] = useState(false);
  const [debateError, setDebateError] = useState<string | null>(null);

  useEffect(() => {
    if (!slug) return;
    setLoaded(false);
    void getBriefingBySlug(slug)
      .then((b) => setBriefing(b ?? null))
      .finally(() => setLoaded(true));
  }, [slug]);

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading…
      </p>
    );
  }

  if (!briefing) {
    return (
      <div className="mx-auto max-w-3xl py-16 text-center" data-testid="briefing-not-found">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Not in this issue
        </p>
        <h1 className="display mt-3 text-4xl">
          We don't have that briefing yet.
        </h1>
        <Link
          to="/"
          className="mt-6 inline-block text-sm font-semibold text-[var(--accent)] hover:underline"
        >
          ← Back to the current issue
        </Link>
      </div>
    );
  }

  const paragraphs = briefing.summary3Min.split("\n\n");

  return (
    <article>
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mx-auto mt-8 max-w-3xl text-center">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          {briefing.institution} · {briefing.status}
        </p>
        <h1 className="display mt-3 text-5xl md:text-6xl">
          {briefing.headline}
        </h1>
        <p className="mt-5 text-xl leading-relaxed text-[var(--fg-soft)]">
          {briefing.summary30}
        </p>
        <p className="mt-4 text-xs uppercase tracking-wider text-[var(--muted)]">
          Audience: {briefing.audienceLevel} · Key concept: {briefing.keyConcept}
        </p>
        {briefing.sourceUrl && (
          <p className="mt-3 text-xs text-[var(--muted)]" data-testid="briefing-source">
            Source:{" "}
            <a
              href={briefing.sourceUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="font-semibold text-[var(--accent)] hover:underline"
              data-testid="briefing-source-link"
            >
              {briefing.sourcePublisher || "Read the original"} ↗
            </a>
          </p>
        )}
      </header>

      <div className="mx-auto mt-14 max-w-3xl">
        <div className="grid gap-10 md:grid-cols-[1fr_220px]">
          <div>
            <p className="dropcap text-lg leading-relaxed text-[var(--fg)]">
              {paragraphs[0]}
            </p>
            {paragraphs.slice(1).map((p, i) => (
              <p
                key={i}
                className="mt-5 text-lg leading-relaxed text-[var(--fg-soft)]"
              >
                {p}
              </p>
            ))}
          </div>
          <aside className="space-y-6 md:sticky md:top-6 md:self-start">
            <div className="border-l-4 border-[var(--accent)] pl-4">
              <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
                Key concept
              </p>
              <p className="display mt-2 text-2xl text-[var(--fg)]">
                {briefing.keyConcept}
              </p>
            </div>
            <div>
              <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
                Values in conflict
              </p>
              <ul className="mt-2 space-y-1 text-base">
                {briefing.valuesInConflict.map((v) => (
                  <li key={v} className="text-[var(--fg-soft)]">
                    · {v}
                  </li>
                ))}
              </ul>
            </div>
          </aside>
        </div>

        <PullQuote text={briefing.thinkDeeperQuestion} source="Think deeper" />

        <section className="mt-12 grid gap-6 md:grid-cols-2">
          <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-6">
            <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
              For
            </p>
            <p className="mt-3 text-base leading-relaxed">
              {briefing.strongestArgumentFor}
            </p>
          </div>
          <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-6">
            <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
              Against
            </p>
            <p className="mt-3 text-base leading-relaxed">
              {briefing.strongestArgumentAgainst}
            </p>
          </div>
        </section>

        {isPremium && (
          <section
            className="mt-12 grid gap-3 border border-[var(--accent)] bg-[var(--bg-elev)] p-6 md:grid-cols-[1fr_auto] md:items-center"
            data-testid="debate-this-cta"
          >
            <div>
              <p className="display text-xl">
                Debate this with AI agents on the Debate Arena floor.
              </p>
              <p className="mt-1 text-sm text-[var(--fg-soft)]">
                Premium accounts can spin up a debate seeded from this briefing.
                Two agents argue both sides — you watch, vote, and steelman.
              </p>
              {debateError && (
                <p
                  className="mt-2 text-sm text-red-600"
                  data-testid="debate-this-error"
                >
                  {debateError}
                </p>
              )}
            </div>
            <Button
              disabled={debateBusy}
              onClick={async () => {
                if (!slug) return;
                setDebateBusy(true);
                setDebateError(null);
                try {
                  const { debateUrl } = await requestDebateFromBriefing(slug);
                  window.open(debateUrl, "_blank", "noopener,noreferrer");
                } catch (err) {
                  const msg = (err as { response?: { data?: { error?: string } } })
                    ?.response?.data?.error;
                  setDebateError(msg ?? "Couldn't start the debate. Try again.");
                } finally {
                  setDebateBusy(false);
                }
              }}
              data-testid="debate-this-button"
            >
              {debateBusy ? "Starting…" : "Debate this →"}
            </Button>
          </section>
        )}

        <SharePreviewCard briefing={briefing} />
      </div>
    </article>
  );
}
