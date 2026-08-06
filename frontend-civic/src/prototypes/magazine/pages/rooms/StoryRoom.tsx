import { Link } from "react-router-dom";

import type { StoryRoomDetail } from "@/api/rooms";
import { EvidenceMark, EvidenceMarkLegend } from "../../components/rooms/EvidenceMark";
import { RoomSourceList } from "../../components/rooms/RoomSourceList";

/**
 * A Story Room (designs 1o and 1p).
 *
 * The atomic unit: one development, told with the same nine parts every time, always
 * carrying an evidence status. The sameness is the feature — a reader who has read one
 * knows where to find "what happens next" in all of them, and a story that cannot fill a
 * part shows the gap instead of quietly dropping the heading.
 *
 * Single column on purpose (design 1o). A Theme Room is a place you scan; a Story Room is
 * a thing you read once, in order, and leave.
 */
export function StoryRoom({ room }: { room: StoryRoomDetail }) {
  const when = room.eventTime ? new Date(room.eventTime) : null;

  return (
    <article className="rooms-square" data-testid="story-room" data-slug={room.slug}>
      <header className="border-b border-[var(--border)] pb-8 pt-6">
        <p className="text-[11px] font-bold uppercase tracking-[0.24em] text-[var(--accent)]">
          Story
          {room.storyType ? ` · ${room.storyType}` : ""}
          {when ? ` · ${when.toLocaleDateString()}` : ""}
          {room.estimatedMinutes ? ` · ${room.estimatedMinutes} min read` : ""}
        </p>
        <h1 className="mt-3 max-w-[760px] text-[32px] leading-[1.06] md:text-[48px]">
          {room.title}
        </h1>
        <p className="mt-4 max-w-[680px] text-[17px] text-[var(--fg-soft)] md:text-[19px]">
          {room.dek}
        </p>

        {room.contentNote && (
          <p
            className="mt-4 border-l-[3px] border-[var(--state)] bg-[var(--state-soft)] px-4 py-2 text-[14px]"
            data-testid="story-content-note"
          >
            {room.contentNote}
          </p>
        )}

        <p className="mt-5 text-[12px] text-[var(--muted)]">Revision r.{room.revision}</p>
      </header>

      {/* --- what happened ----------------------------------------------------- */}
      {room.essentialFacts.length > 0 && (
        <section
          className="border-b border-[var(--border)] py-8"
          data-testid="story-essential-facts"
        >
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            What happened
          </p>
          <ol className="mt-4 flex max-w-[760px] flex-col">
            {room.essentialFacts.map((fact, i) => (
              <li
                key={fact.ordinal}
                className="flex gap-5 border-t border-[var(--border)] py-4"
                data-testid="story-fact"
              >
                <span className="display shrink-0 text-[22px] text-[var(--accent)]">
                  {i + 1}
                </span>
                <div>
                  <p className="text-[17px] leading-snug">{fact.text}</p>
                  {fact.claimStatus && (
                    <p className="mt-2">
                      {/* Renders from the claim, so a correction reaches this line
                          without anyone editing the story. */}
                      <EvidenceMark status={fact.claimStatus} size="inline" />
                      {fact.claimSlug && (
                        <Link
                          to={`/claims/${fact.claimSlug}`}
                          className="ml-3 text-[12px] underline"
                        >
                          Evidence
                        </Link>
                      )}
                    </p>
                  )}
                </div>
              </li>
            ))}
          </ol>
        </section>
      )}

      {/* --- how it works ------------------------------------------------------ */}
      {room.howItWorksIntro && (
        <section className="border-b border-[var(--border)] py-8" data-testid="story-how-it-works">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            How this works
          </p>
          <p className="mt-3 max-w-[760px] text-[17px] leading-relaxed">
            {room.howItWorksIntro}
          </p>
        </section>
      )}

      {/* --- why it matters, along fixed dimensions ---------------------------- */}
      {room.whyItMatters.length > 0 && (
        <section className="border-b border-[var(--border)] py-8" data-testid="story-why-it-matters">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            Why it matters
          </p>
          {/* The dimensions are fixed across every story so a reader can compare two of
              them. An empty dimension is omitted rather than padded. */}
          <dl className="mt-4 grid gap-x-10 gap-y-5 md:grid-cols-2">
            {room.whyItMatters.map((d) => (
              <div key={d.dimension} className="border-t border-[var(--border)] pt-3">
                <dt className="text-[11px] uppercase tracking-[0.16em] text-[var(--accent)]">
                  {d.dimension}
                </dt>
                <dd className="mt-2 text-[15px] leading-snug">{d.text}</dd>
                {d.claimStatus && (
                  <dd className="mt-2">
                    <EvidenceMark status={d.claimStatus} size="inline" />
                    {d.claimSlug && (
                      <Link to={`/claims/${d.claimSlug}`} className="ml-3 text-[12px] underline">
                        Evidence
                      </Link>
                    )}
                  </dd>
                )}
              </div>
            ))}
          </dl>
        </section>
      )}

      {/* --- who this lands on ------------------------------------------------- */}
      {room.stakeholders.length > 0 && (
        <section className="border-b border-[var(--border)] py-8" data-testid="story-stakeholders">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            Who this lands on
          </p>
          <ul className="mt-4 flex flex-col">
            {room.stakeholders.map((s) => (
              <li key={s.group} className="border-t border-[var(--border)] py-4">
                <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
                  <p className="text-[16px] font-semibold">{s.group}</p>
                  {/* Low confidence is shown, not hidden. A group we are unsure about is
                      more useful labelled than dropped. */}
                  <p className="text-[12px] uppercase tracking-[0.16em] text-[var(--muted)]">
                    {confidenceWord(s.confidence)}
                  </p>
                </div>
                <p className="mt-1 max-w-[680px] text-[15px] text-[var(--fg-soft)]">
                  {s.impactSummary}
                </p>
              </li>
            ))}
          </ul>
        </section>
      )}

      {/* --- what happens next ------------------------------------------------- */}
      {room.nextSteps.length > 0 && (
        <section className="border-b border-[var(--border)] py-8" data-testid="story-next-steps">
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            What happens next
          </p>
          <ul className="mt-4 flex flex-col">
            {room.nextSteps.map((n, i) => (
              <li key={i} className="border-t border-[var(--border)] py-4">
                <p className="text-[16px] leading-snug">{n.description}</p>
                {/* Every next step carries an objective test, so a prediction cannot be
                    quietly graded as correct after the fact. */}
                <p className="mt-2 text-[14px] text-[var(--fg-soft)]">
                  <span className="text-[var(--muted)]">Confirmed if:</span>{" "}
                  {n.verificationCondition}
                </p>
                {n.expectedTiming && (
                  <p className="mt-1 text-[12px] text-[var(--muted)]">{n.expectedTiming}</p>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {/* --- sources ----------------------------------------------------------- */}
      <section className="py-8" data-testid="story-sources">
        <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">
          Sources &amp; methodology
        </p>
        <h2 className="mt-2 text-[26px] md:text-[34px]">What this rests on</h2>
        <div className="mt-6">
          <RoomSourceList slug={room.slug} />
        </div>
        <div className="mt-8 max-w-[520px]">
          <h3 className="text-[11px] uppercase tracking-[0.2em] text-[var(--muted)]">
            What the marks mean
          </h3>
          <div className="mt-3">
            <EvidenceMarkLegend />
          </div>
        </div>
      </section>
    </article>
  );
}

/**
 * Confidence as a word, not a percentage.
 *
 * A stakeholder impact is a judgement, and rendering 0.3 as "30%" implies a measurement
 * nobody took. The bands are coarse on purpose.
 */
function confidenceWord(confidence: number): string {
  if (confidence >= 0.75) return "Well established";
  if (confidence >= 0.5) return "Likely";
  return "Uncertain";
}
