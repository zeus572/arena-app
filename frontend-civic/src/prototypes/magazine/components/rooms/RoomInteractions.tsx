import { useEffect, useState } from "react";

import {
  getRoomInteractions,
  submitInteraction,
  type InteractionResult,
  type RoomInteraction,
} from "@/api/rooms";
import { Button } from "../Button";

/**
 * Room interactions (PRD 06, designs 1o / 1u / 1v / 1p).
 *
 * Four rules hold across every one of them and are worth stating once:
 *
 *  1. Nobody has to sign in to play. Civic gives every browser a pseudonymous id, so a
 *     signed-out reader's answers are kept against that id and merged into a real account
 *     if they later register; a client that sends no id at all plays fully and stores
 *     nothing. Either way there is no signup wall in front of the part of the product that
 *     teaches, and the result panel says which of the two happened rather than leaving the
 *     reader to assume.
 *  2. The explanation shows whether the answer was right or wrong. Being told you were
 *     wrong teaches nothing on its own, so an interaction without one is a publish blocker.
 *  3. No timers, no streaks, no penalties. These are not games.
 *  4. Nothing here needs a mouse. Ordering is done with move-up/move-down buttons rather
 *     than drag-and-drop — that is not a fallback bolted onto a drag interaction, it is the
 *     only interaction, so there is no second path to keep working.
 */
export function RoomInteractions({ slug }: { slug: string }) {
  const [items, setItems] = useState<RoomInteraction[] | null>(null);

  useEffect(() => {
    let alive = true;
    getRoomInteractions(slug)
      .then((i) => alive && setItems(i))
      .catch(() => alive && setItems(null));
    return () => {
      alive = false;
    };
  }, [slug]);

  if (!items || items.length === 0) return null;

  return (
    <section className="border-b border-[var(--border)] py-8" data-testid="room-interactions">
      <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">Check yourself</p>
      <h2 className="mt-2 text-[28px] md:text-[40px]">Before you decide what you think</h2>
      <p className="mt-3 max-w-[680px] text-[15px] text-[var(--fg-soft)]">
        No timer, no score to beat, nothing recorded unless you are signed in. Each one
        explains itself afterwards whether you got it right or not.
      </p>

      <div className="mt-8 flex flex-col gap-8">
        {items.map((i) => (
          <InteractionCard key={i.slug} roomSlug={slug} interaction={i} />
        ))}
      </div>
    </section>
  );
}

function InteractionCard({
  roomSlug,
  interaction,
}: {
  roomSlug: string;
  interaction: RoomInteraction;
}) {
  const [result, setResult] = useState<InteractionResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(response: unknown, phase: "pre" | "post" = "post") {
    setBusy(true);
    setError(null);
    try {
      setResult(await submitInteraction(roomSlug, interaction.slug, response, phase));
    } catch {
      // Never swallow: the reader pressed a button and is owed an outcome either way.
      setError("Could not record that. Your answer was not saved — try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <article
      className="border-t-2 border-[var(--fg)] pt-4"
      data-testid="interaction"
      data-kind={interaction.kind}
      data-slug={interaction.slug}
    >
      <div className="flex flex-wrap items-baseline justify-between gap-x-4">
        <h3 className="text-[19px] md:text-[22px]">{interaction.title}</h3>
        {!interaction.scored && (
          <p className="text-[11px] uppercase tracking-[0.16em] text-[var(--muted)]">
            No right answer
          </p>
        )}
      </div>

      <p className="mt-2 max-w-[680px] text-[15px] text-[var(--fg-soft)]">{interaction.prompt}</p>

      <div className="mt-5">
        {interaction.kind === "BeforeYouKnow" && (
          <BeforeYouKnow payload={interaction.payload} disabled={busy || !!result} onSubmit={submit} />
        )}
        {interaction.kind === "ClassifyStatement" && (
          <ClassifyStatement payload={interaction.payload} disabled={busy || !!result} onSubmit={submit} />
        )}
        {interaction.kind === "TimelineBuilder" && (
          <TimelineBuilder payload={interaction.payload} disabled={busy || !!result} onSubmit={submit} />
        )}
        {interaction.kind === "VoteBeforeReading" && (
          <VoteBeforeReading
            payload={interaction.payload}
            disabled={busy}
            result={result}
            onSubmit={submit}
          />
        )}
      </div>

      {error && (
        <p className="mt-4 text-[14px] text-[var(--state)]" data-testid="interaction-error">
          {error}
        </p>
      )}

      {result && <ResultPanel result={result} />}
    </article>
  );
}

/** The reveal. Always renders the explanation; a score only when one is meaningful. */
function ResultPanel({ result }: { result: InteractionResult }) {
  return (
    <div className="mt-5 bg-[var(--bg-sunken)] p-5" data-testid="interaction-result">
      {result.scored && result.score !== null && (
        <p className="text-[11px] uppercase tracking-[0.16em] text-[var(--muted)]">
          {result.score}% in the right place
        </p>
      )}

      <p className="mt-1 max-w-[680px] text-[15px] leading-snug">{result.explanation}</p>

      {result.items.length > 0 && (
        <ul className="mt-4 flex flex-col">
          {result.items.map((i) => (
            <li key={i.itemId} className="border-t border-[var(--border)] py-3">
              {/* Only a scored interaction may mark a row right or wrong. Before You Know
                  returns EVERY option's explanation — the point is to show why the tempting
                  wrong answers are tempting — so labelling them against a key the reader was
                  never graded on would read as a verdict on a choice they did not make. */}
              {result.scored && (
                <p className="text-[12px] uppercase tracking-[0.14em] text-[var(--muted)]">
                  {i.correct ? "Right" : "Not quite"}
                  {i.correctLabel ? ` · ${i.correctLabel}` : ""}
                </p>
              )}
              <p className="mt-1 max-w-[680px] text-[14px]">{i.explanation}</p>
            </li>
          ))}
        </ul>
      )}

      {result.moved !== null && (
        <p className="mt-4 text-[14px]" data-testid="interaction-moved">
          {result.moved
            ? "Reading both sides moved you."
            : "Reading both sides left you where you started."}
        </p>
      )}

      {!result.persisted && (
        // Said plainly rather than implied. A reader who assumed this was saved and finds
        // it gone later has been misled by omission.
        <p className="mt-4 text-[12px] text-[var(--muted)]">
          Not signed in, so this was not saved and no points were awarded.
        </p>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- Before you know

function BeforeYouKnow({
  payload,
  disabled,
  onSubmit,
}: {
  payload: unknown;
  disabled: boolean;
  onSubmit: (r: unknown) => void;
}) {
  const p = payload as { question?: string; options?: Array<{ id: string; text: string }> };
  const [choice, setChoice] = useState<string | null>(null);

  return (
    <div>
      {p.question && <p className="text-[17px] leading-snug">{p.question}</p>}

      <ul className="mt-4 flex flex-col gap-2">
        {(p.options ?? []).map((o) => (
          <li key={o.id}>
            <button
              type="button"
              disabled={disabled}
              onClick={() => setChoice(o.id)}
              aria-pressed={choice === o.id}
              className={
                "flex min-h-[44px] w-full items-center border p-3 text-left text-[15px] " +
                (choice === o.id
                  ? "border-[var(--accent)] bg-[var(--accent)]/5"
                  : "border-[var(--border)]")
              }
              data-testid="byk-option"
            >
              {o.text}
            </button>
          </li>
        ))}
      </ul>

      <Button
        className="mt-4 min-h-[44px]"
        disabled={disabled || !choice}
        onClick={() => choice && onSubmit({ optionId: choice })}
        data-testid="byk-submit"
      >
        Lock it in
      </Button>
    </div>
  );
}

// ---------------------------------------------------------------- Classify statement

const LABELS = ["Factual", "Interpretation", "Opinion", "Prediction"];

function ClassifyStatement({
  payload,
  disabled,
  onSubmit,
}: {
  payload: unknown;
  disabled: boolean;
  onSubmit: (r: unknown) => void;
}) {
  const p = payload as { items?: Array<{ id: string; text: string }> };
  const items = p.items ?? [];
  const [labels, setLabels] = useState<Record<string, string>>({});
  const complete = items.length > 0 && items.every((i) => labels[i.id]);

  return (
    <div>
      <ul className="flex flex-col gap-5">
        {items.map((item) => (
          <li key={item.id} data-testid="classify-item">
            {/* Verbatim from coverage, never paraphrased — the exercise is worthless if the
                sentence has been tidied into its own answer. */}
            <p className="max-w-[680px] border-l-[3px] border-[var(--border)] pl-4 text-[16px] leading-snug">
              {item.text}
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              {LABELS.map((label) => (
                <button
                  key={label}
                  type="button"
                  disabled={disabled}
                  onClick={() => setLabels((l) => ({ ...l, [item.id]: label }))}
                  aria-pressed={labels[item.id] === label}
                  className={
                    "min-h-[44px] border px-3 text-[13px] " +
                    (labels[item.id] === label
                      ? "border-[var(--accent)] bg-[var(--accent)]/5"
                      : "border-[var(--border)]")
                  }
                  data-testid="classify-label"
                >
                  {label}
                </button>
              ))}
            </div>
          </li>
        ))}
      </ul>

      <Button
        className="mt-5 min-h-[44px]"
        disabled={disabled || !complete}
        onClick={() => onSubmit({ labels })}
        data-testid="classify-submit"
      >
        {complete ? "Check my labels" : `Label all ${items.length} first`}
      </Button>
    </div>
  );
}

// ---------------------------------------------------------------- Timeline builder

function TimelineBuilder({
  payload,
  disabled,
  onSubmit,
}: {
  payload: unknown;
  disabled: boolean;
  onSubmit: (r: unknown) => void;
}) {
  const p = payload as { eventIds?: string[]; labels?: Record<string, string> };
  const [order, setOrder] = useState<string[]>(p.eventIds ?? []);

  // Move-up / move-down rather than drag. A drag interaction would need a select-then-place
  // fallback to satisfy the accessibility publish gate, and a fallback nobody uses is a
  // fallback nobody notices breaking — so this is the only path, and it works with a
  // keyboard, a screen reader and a thumb without any of them being a special case.
  function move(index: number, delta: number) {
    setOrder((current) => {
      const next = [...current];
      const target = index + delta;
      if (target < 0 || target >= next.length) return current;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  return (
    <div>
      <ol className="flex flex-col" data-testid="timeline-builder">
        {order.map((id, i) => (
          <li
            key={id}
            className="flex items-center gap-3 border-t border-[var(--border)] py-2"
            data-testid="builder-event"
            data-event-id={id}
          >
            <span className="display w-6 shrink-0 text-[18px] text-[var(--muted)]">{i + 1}</span>
            <span className="flex-1 text-[15px] leading-snug">{p.labels?.[id] ?? id}</span>
            <span className="flex shrink-0 gap-1">
              <button
                type="button"
                disabled={disabled || i === 0}
                onClick={() => move(i, -1)}
                aria-label={`Move "${p.labels?.[id] ?? id}" earlier`}
                className="min-h-[44px] min-w-[44px] border border-[var(--border)] disabled:opacity-30"
                data-testid="builder-up"
              >
                ↑
              </button>
              <button
                type="button"
                disabled={disabled || i === order.length - 1}
                onClick={() => move(i, 1)}
                aria-label={`Move "${p.labels?.[id] ?? id}" later`}
                className="min-h-[44px] min-w-[44px] border border-[var(--border)] disabled:opacity-30"
                data-testid="builder-down"
              >
                ↓
              </button>
            </span>
          </li>
        ))}
      </ol>

      <Button
        className="mt-4 min-h-[44px]"
        disabled={disabled}
        onClick={() => onSubmit({ order })}
        data-testid="builder-submit"
      >
        Check the order
      </Button>
    </div>
  );
}

// ---------------------------------------------------------------- Vote before reading

const VOTES: Array<{ id: string; label: string }> = [
  { id: "Yes", label: "Yes" },
  { id: "No", label: "No" },
  { id: "NotSure", label: "Not sure" },
];

function VoteBeforeReading({
  payload,
  disabled,
  result,
  onSubmit,
}: {
  payload: unknown;
  disabled: boolean;
  result: InteractionResult | null;
  onSubmit: (r: unknown, phase: "pre" | "post") => void;
}) {
  const p = payload as { question?: string };
  const [vote, setVote] = useState<string | null>(null);

  // Two passes. The first answer is withheld by the SERVER until the second is in — the
  // client never receives it, so there is nothing here that could render it early.
  const phase: "pre" | "post" = result?.phase === "Pre" ? "post" : "pre";
  const secondPass = phase === "post";

  return (
    <div>
      {p.question && <p className="text-[17px] leading-snug">{p.question}</p>}

      {secondPass && (
        <p className="mt-3 max-w-[680px] text-[14px] text-[var(--fg-soft)]">
          You have answered once. Read the room, then answer again — we will show you both
          answers and whether you moved.
        </p>
      )}

      <div className="mt-4 flex flex-wrap gap-2">
        {VOTES.map((v) => (
          <button
            key={v.id}
            type="button"
            disabled={disabled}
            onClick={() => setVote(v.id)}
            aria-pressed={vote === v.id}
            className={
              "min-h-[44px] border px-5 text-[15px] " +
              (vote === v.id ? "border-[var(--accent)] bg-[var(--accent)]/5" : "border-[var(--border)]")
            }
            data-testid="vote-option"
          >
            {v.label}
          </button>
        ))}
      </div>

      <Button
        className="mt-4 min-h-[44px]"
        disabled={disabled || !vote}
        onClick={() => vote && onSubmit({ vote }, phase)}
        data-testid="vote-submit"
      >
        {secondPass ? "Answer again" : "Record my answer"}
      </Button>
    </div>
  );
}
