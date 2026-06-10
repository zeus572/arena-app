import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Trophy, Vote, Lock, ChevronRight } from "lucide-react";
import {
  getRound,
  submitEntry,
  startVoting,
  closeRound,
  voteEntry,
  unvoteEntry,
  type LeagueRoundDetail,
  type NewsResponseOptionDetail,
} from "@/api/leagues";
import type { ReactionType } from "@/api/campaign";
import { useAuth } from "@/auth/AuthContext";
import { CampaignPostCard } from "../components/CampaignPostCard";
import { SignInPrompt } from "../components/SignInPrompt";
import { Button } from "../components/Button";

export default function LeagueRound() {
  const { id = "", roundId = "" } = useParams();
  const { isAuthenticated, isLoading } = useAuth();
  const [round, setRound] = useState<LeagueRoundDetail | undefined | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    const r = await getRound(id, roundId);
    setRound(r ?? undefined);
  }, [id, roundId]);

  useEffect(() => {
    if (!isAuthenticated) return;
    void refresh();
  }, [isAuthenticated, refresh]);

  async function withBusy(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await fn();
    } catch (err) {
      setError(
        (err as { response?: { data?: { error?: string } } }).response?.data?.error ?? "Something went wrong.",
      );
    } finally {
      setBusy(false);
    }
  }

  if (!isLoading && !isAuthenticated) {
    return (
      <section className="mx-auto max-w-lg">
        <SignInPrompt title="Sign in to view this round" />
      </section>
    );
  }
  if (round === null) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">Loading…</p>;
  }
  if (round === undefined) {
    return (
      <section>
        <h1 className="display text-3xl">Round not found</h1>
        <Link to={`/leagues/${id}`} className="mt-2 inline-block font-semibold text-[var(--accent)]">
          ← Back to the league
        </Link>
      </section>
    );
  }

  const isOwner = round.myRole === "Owner";
  const voting = round.status === "Voting";
  const closed = round.status === "Closed";

  return (
    <section data-testid="league-round-page" className="space-y-6">
      <header>
        <Link to={`/leagues/${id}`} className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]">
          ← Back to the league
        </Link>
        <div className="mt-1 flex flex-wrap items-center gap-2">
          <span className="rounded-full bg-[var(--accent)] px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider text-white">
            Round {round.roundNumber}
          </span>
          <StatusBadge status={round.status} />
        </div>
        <h1 className="display mt-2 text-3xl">{round.headline}</h1>
        {round.summary && <p className="mt-2 max-w-prose text-[var(--fg-soft)]">{round.summary}</p>}
        {round.valuesInConflict.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1.5">
            {round.valuesInConflict.map((v) => (
              <span key={v} className="rounded-full border border-[var(--border)] px-2 py-0.5 text-xs text-[var(--fg-soft)]">
                {v}
              </span>
            ))}
          </div>
        )}
        <Link
          to={`/briefings/${round.briefingSlug}`}
          className="mt-2 inline-flex items-center text-sm font-semibold text-[var(--accent)] hover:underline"
        >
          Read the full briefing <ChevronRight className="h-4 w-4" />
        </Link>
      </header>

      {error && (
        <p className="border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700" data-testid="round-error">
          {error}
        </p>
      )}

      {/* Owner lifecycle controls */}
      {isOwner && !closed && (
        <div className="flex flex-wrap items-center gap-2 border border-[var(--border)] bg-[var(--bg-elev)] p-3" data-testid="owner-controls">
          <span className="text-sm font-semibold text-[var(--fg-soft)]">Owner controls:</span>
          {round.status === "OpenForResponses" ? (
            <Button
              onClick={() => withBusy(async () => { await startVoting(id, roundId); await refresh(); })}
              disabled={busy}
              data-testid="start-voting"
            >
              <Vote className="h-4 w-4" /> Start voting
            </Button>
          ) : (
            <Button
              onClick={() => withBusy(async () => { await closeRound(id, roundId); await refresh(); })}
              disabled={busy}
              data-testid="close-round"
            >
              <Lock className="h-4 w-4" /> Close round & score
            </Button>
          )}
        </div>
      )}

      {/* Submission */}
      {round.canSubmit ? (
        <SubmitForm
          options={round.options}
          busy={busy}
          onSubmit={(optionId) => withBusy(async () => { await submitEntry(id, roundId, { optionId }); await refresh(); })}
        />
      ) : !round.iHaveEntered && round.cannotSubmitReason ? (
        <div className="border border-dashed border-[var(--border)] bg-[var(--bg-elev)] p-4 text-sm text-[var(--fg-soft)]" data-testid="cannot-submit">
          {round.cannotSubmitReason}
          {round.cannotSubmitReason.toLowerCase().includes("link a campaign") && (
            <>
              {" "}
              <Link to={`/leagues/${id}`} className="font-semibold text-[var(--accent)]">Link one →</Link>
            </>
          )}
        </div>
      ) : null}

      {/* Entries */}
      {round.entriesVisible && (
        <div data-testid="entries">
          <div className="flex items-center justify-between">
            <h2 className="flex items-center gap-1 text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">
              <Trophy className="h-4 w-4" /> {closed ? "Results" : "Responses"}
            </h2>
            {voting && <span className="text-xs text-[var(--muted)]">Vote on everyone's response but your own.</span>}
          </div>

          {round.entries.length === 0 ? (
            <p className="mt-3 text-sm text-[var(--muted)]">No responses yet.</p>
          ) : (
            <ul className="mt-3 space-y-5">
              {round.entries.map((entry) => (
                <li key={entry.id} data-testid="round-entry">
                  <div className="mb-1.5 flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-[var(--fg)]">
                      {entry.displayName}
                      {entry.isMe && <span className="ml-1 text-xs text-[var(--accent)]">(you)</span>}
                    </span>
                    {entry.optionLabel && (
                      <span className="rounded-full border border-[var(--border)] px-2 py-0.5 text-xs text-[var(--fg-soft)]">
                        {entry.optionLabel}
                      </span>
                    )}
                    {closed && entry.isWinner && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-[var(--accent)] px-2 py-0.5 text-xs font-semibold text-white">
                        <Trophy className="h-3 w-3" /> Winner +{entry.pointsEarned}
                      </span>
                    )}
                    {closed && !entry.isWinner && (
                      <span className="text-xs text-[var(--muted)]">+{entry.pointsEarned} pts · net {entry.net}</span>
                    )}
                  </div>
                  <CampaignPostCard
                    post={entry.post}
                    showCompare={false}
                    votingDisabled={!voting || entry.isMe}
                    votingDisabledLabel={
                      entry.isMe ? "You can't vote on your own response" : closed ? "Voting is closed" : "Voting hasn't started"
                    }
                    onReact={(type: ReactionType) => voteEntry(id, roundId, entry.id, type)}
                    onRemoveReaction={() => unvoteEntry(id, roundId, entry.id)}
                  />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </section>
  );
}

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { label: string; cls: string }> = {
    OpenForResponses: { label: "Responses open", cls: "border-[var(--border)] text-[var(--fg-soft)]" },
    Voting: { label: "Voting open", cls: "border-[var(--accent)] text-[var(--accent)]" },
    Closed: { label: "Closed", cls: "border-[var(--border)] text-[var(--muted)]" },
  };
  const m = map[status] ?? map.Closed;
  return (
    <span className={`rounded-full border px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider ${m.cls}`}>
      {m.label}
    </span>
  );
}

function SubmitForm({
  options,
  busy,
  onSubmit,
}: {
  options: NewsResponseOptionDetail[];
  busy: boolean;
  onSubmit: (optionId: string) => void;
}) {
  const [selected, setSelected] = useState<string | null>(null);

  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="submit-form">
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">Your response</h2>
      <p className="mt-1 text-sm text-[var(--fg-soft)]">
        Pick how your candidate responds. Your friends will vote on it once responses close.
      </p>
      <ul className="mt-3 space-y-3">
        {options.map((o) => (
          <li key={o.id}>
            <button
              type="button"
              onClick={() => setSelected(o.id)}
              data-testid="response-option"
              className={`block w-full border p-4 text-left transition ${
                selected === o.id ? "border-[var(--accent)] bg-[var(--accent)]/5" : "border-[var(--border)] hover:border-[var(--accent)]"
              }`}
            >
              <span className="flex items-center justify-between gap-2">
                <span className="font-semibold text-[var(--fg)]">{o.label}</span>
                <span className="text-xs uppercase tracking-wider text-[var(--muted)]">{o.tone}</span>
              </span>
              <span className="mt-0.5 block text-xs italic text-[var(--muted)]">{o.angle}</span>
              <span className="mt-2 block leading-relaxed text-[var(--fg)]">{o.body}</span>
            </button>
          </li>
        ))}
      </ul>
      <Button
        onClick={() => selected && onSubmit(selected)}
        disabled={!selected || busy}
        data-testid="submit-entry"
        className="mt-4"
      >
        {busy ? "Submitting…" : "Submit response"}
      </Button>
    </div>
  );
}
