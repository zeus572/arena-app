import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { getClaim, type ClaimDetail as Claim, type ClaimSource } from "@/api/claims";
import { EvidenceMark, STATUS_MEANING, STATUS_WORD } from "../../components/rooms/EvidenceMark";

/**
 * One claim, its evidence, and everywhere it is used (designs 1m and 1n).
 *
 * Every evidence mark in every room links here. That is the load-bearing half of the
 * architecture: rooms reference claims instead of copying their text, so a reader who wants
 * to know why a sentence carries the mark it does has exactly one place to go, and a
 * correction changes that place rather than N copies of a sentence.
 *
 * The page is deliberately willing to be boring. It shows what supports the claim, what
 * contradicts it, what would settle it, who asserted it, where it appears, and how its
 * status has moved. A claim with thin evidence should LOOK thin here.
 */
export default function ClaimDetailPage() {
  const { slug = "" } = useParams();
  const [claim, setClaim] = useState<Claim | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let alive = true;
    setLoaded(false);
    getClaim(slug)
      .then((c) => {
        if (!alive) return;
        setClaim(c ?? null);
        setLoaded(true);
      })
      .catch(() => {
        if (!alive) return;
        setClaim(null);
        setLoaded(true);
      });
    return () => {
      alive = false;
    };
  }, [slug]);

  if (!loaded) {
    return (
      <p className="py-16 text-sm text-[var(--muted)]" data-testid="claim-loading">
        Loading claim…
      </p>
    );
  }

  if (!claim) {
    return (
      <div className="py-16" data-testid="claim-missing">
        <h1 className="text-2xl">Claim not found</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">
          It may have been merged into another claim, or the link may be out of date.
        </p>
        <Link to="/rooms" className="mt-4 inline-block text-sm underline">
          All rooms
        </Link>
      </div>
    );
  }

  const settled = claim.status === "Confirmed" || claim.status === "False";

  return (
    <article className="rooms-square" data-testid="claim-detail" data-slug={claim.slug}>
      <header className="border-b border-[var(--border)] pb-8 pt-6">
        <p className="text-[11px] font-bold uppercase tracking-[0.24em] text-[var(--accent)]">
          Claim · {claim.kind}
        </p>
        <h1 className="display mt-3 max-w-[820px] text-[26px] leading-[1.15] md:text-[38px]">
          {claim.text}
        </h1>

        <div className="mt-5 flex flex-wrap items-center gap-4">
          <EvidenceMark status={claim.status} size="large" />
          <p className="max-w-[520px] text-[14px] text-[var(--fg-soft)]">
            {STATUS_MEANING[claim.status]}
          </p>
        </div>

        {claim.evidenceSummary && (
          <p className="mt-5 max-w-[720px] text-[16px]" data-testid="claim-evidence-summary">
            {claim.evidenceSummary}
          </p>
        )}
      </header>

      {/* What would settle it — required on every claim, so it always renders. */}
      <section
        className="border-b border-[var(--border)] py-7"
        data-testid="claim-what-would-settle-it"
      >
        <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
          {settled ? "How this was settled" : "What would settle it"}
        </p>
        <p className="display mt-3 max-w-[820px] text-[20px] leading-snug md:text-[24px]">
          {claim.whatWouldSettleIt}
        </p>
      </section>

      {/* Evidence, both directions, side by side so a thin case looks thin. */}
      <section className="grid gap-8 border-b border-[var(--border)] py-8 md:grid-cols-2">
        <EvidenceColumn
          title="Supports this"
          sources={claim.evidenceFor}
          empty="Nothing here supports this claim yet."
          testid="claim-evidence-for"
        />
        <EvidenceColumn
          title="Contradicts this"
          sources={claim.evidenceAgainst}
          empty="Nothing here contradicts this claim."
          testid="claim-evidence-against"
        />
      </section>

      {claim.assertedBy.length > 0 && (
        <section className="border-b border-[var(--border)] py-7" data-testid="claim-asserted-by">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            Asserted by
          </p>
          {/* Who said it establishes that it was said — never that it is true. */}
          <ul className="mt-3 flex flex-wrap gap-x-6 gap-y-2">
            {claim.assertedBy.map((a) => (
              <li key={`${a.objectType}-${a.objectId}`} className="text-[15px]">
                {a.label}
              </li>
            ))}
          </ul>
          <p className="mt-3 text-[13px] text-[var(--muted)]">
            That someone asserted this establishes only that they said it.
          </p>
        </section>
      )}

      {claim.appearsIn.length > 0 && (
        <section className="border-b border-[var(--border)] py-7" data-testid="claim-appears-in">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            Where this appears
          </p>
          <p className="mt-1 text-[13px] text-[var(--muted)]">
            If this claim's status changes, every one of these follows automatically.
          </p>
          <ul className="mt-3 flex flex-col">
            {claim.appearsIn.map((a) => (
              <li
                key={`${a.objectType}-${a.objectId}`}
                className="flex flex-wrap items-baseline gap-x-3 border-t border-[var(--border)] py-3"
              >
                <span className="text-[11px] uppercase tracking-[0.16em] text-[var(--muted)]">
                  {a.objectType}
                </span>
                {a.objectType === "Room" && a.slug ? (
                  <Link to={`/rooms/${a.slug}`} className="text-[15px] underline">
                    {a.label}
                  </Link>
                ) : (
                  <span className="text-[15px]">{a.label}</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {claim.statusHistory.length > 0 && (
        <section className="py-7" data-testid="claim-status-history">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            How this status has moved
          </p>
          <ol className="mt-3 flex flex-col">
            {claim.statusHistory.map((h, i) => (
              <li key={i} className="border-t border-[var(--border)] py-3">
                <p className="text-[14px]">
                  {h.fromStatus ? (
                    <>
                      <span className="text-[var(--muted)]">
                        {STATUS_WORD[h.fromStatus]}
                      </span>{" "}
                      → <strong>{STATUS_WORD[h.toStatus]}</strong>
                    </>
                  ) : (
                    <>
                      First assessed as <strong>{STATUS_WORD[h.toStatus]}</strong>
                    </>
                  )}
                  <span className="ml-3 text-[12px] text-[var(--muted)]">
                    {new Date(h.changedAt).toLocaleDateString()}
                  </span>
                </p>
                {h.rationale && (
                  <p className="mt-1 text-[14px] text-[var(--fg-soft)]">{h.rationale}</p>
                )}
                {h.sourceCorrectedAt && (
                  // Time-from-SOURCE-correction is the metric we publish, so the date the
                  // source changed matters more than the date we noticed.
                  <p className="mt-1 text-[12px] text-[var(--muted)]">
                    Source corrected {new Date(h.sourceCorrectedAt).toLocaleDateString()}
                  </p>
                )}
              </li>
            ))}
          </ol>
        </section>
      )}
    </article>
  );
}

function EvidenceColumn({
  title,
  sources,
  empty,
  testid,
}: {
  title: string;
  sources: ClaimSource[];
  empty: string;
  testid: string;
}) {
  return (
    <div data-testid={testid}>
      <h2 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
        {title} · {sources.length}
      </h2>
      {sources.length === 0 ? (
        <p className="mt-3 text-[14px] text-[var(--muted)]">{empty}</p>
      ) : (
        <ul className="mt-3 flex flex-col">
          {sources.map((s) => (
            <li key={s.id} className="border-t border-[var(--border)] py-3">
              <a
                href={s.url}
                target="_blank"
                rel="noreferrer"
                className="text-[15px] leading-snug underline"
              >
                {s.title}
              </a>
              <p className="mt-1 text-[12px] text-[var(--muted)]">
                {[
                  s.organization,
                  s.sourceType,
                  s.isPrimary ? "primary" : null,
                  s.publishedAt ? new Date(s.publishedAt).toLocaleDateString() : null,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
              {!s.fullTextAvailable && (
                // Saying so is the honest alternative to implying we quoted from it.
                <p className="mt-1 text-[12px] text-[var(--muted)]">
                  We hold the headline and summary for this, not the full text.
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
