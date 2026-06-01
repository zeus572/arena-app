import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Trophy, Loader2 } from "lucide-react";
import {
  getCampaign,
  takeAction,
  advanceWeek,
  getCampaignResults,
  type CivicCampaignDetail,
  type CivicCampaignResults,
  type CivicActionOption,
  type CivicCampaignActionType,
} from "@/api/campaignManager";

export default function CampaignDashboard() {
  const { id } = useParams();
  const [campaign, setCampaign] = useState<CivicCampaignDetail | null>(null);
  const [results, setResults] = useState<CivicCampaignResults | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!id) return;
    const detail = await getCampaign(id);
    setCampaign(detail ?? null);
    if (detail?.status === "Completed") {
      setResults(await getCampaignResults(id));
    }
  }, [id]);

  useEffect(() => {
    setLoaded(false);
    void refresh().finally(() => setLoaded(true));
  }, [refresh]);

  async function onAction(option: CivicActionOption) {
    if (!id || busy) return;
    setBusy(true);
    setNotice(null);
    try {
      const res = await takeAction(id, {
        actionType: option.actionType as CivicCampaignActionType,
        target: option.suggestedTarget ?? undefined,
      });
      setNotice(res.action.summary);
      await refresh();
    } catch (err) {
      setNotice(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function onAdvance() {
    if (!id || busy) return;
    setBusy(true);
    setNotice(null);
    try {
      const res = await advanceWeek(id);
      setNotice(res.summary);
      await refresh();
    } catch (err) {
      setNotice(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading…
      </p>
    );
  }

  if (!campaign) {
    return (
      <div className="py-16 text-center" data-testid="campaign-not-found">
        <h1 className="display text-4xl">Campaign not found.</h1>
        <Link to="/campaigns" className="mt-6 inline-block text-sm font-semibold text-[var(--accent)]">
          ← Back to campaigns
        </Link>
      </div>
    );
  }

  const player = campaign.standings.find((s) => s.isPlayer);
  const sorted = [...campaign.standings].sort((a, b) => b.supportShare - a.supportShare);
  const completed = campaign.status === "Completed";

  return (
    <section data-testid="campaign-dashboard">
      <Link
        to="/campaigns"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Campaigns
      </Link>

      <header className="mt-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
            {campaign.raceLabel} · {campaign.difficulty}
          </p>
          <h1 className="display mt-1 text-4xl">{campaign.candidateName}</h1>
          <p className="text-sm text-[var(--fg-soft)]">{campaign.party}</p>
        </div>
        <div className="text-right">
          <p className="text-sm text-[var(--muted)]">
            {completed ? "Final" : `Week ${campaign.currentWeek} of ${campaign.totalWeeks}`}
          </p>
          <p className="text-4xl font-bold text-[var(--fg)]" data-testid="player-support">
            {(player?.supportShare ?? 0).toFixed(1)}%
          </p>
        </div>
      </header>

      {completed && results && <ResultsBanner results={results} />}

      <SupportTrend campaign={campaign} />

      <div className="mt-8 grid gap-8 lg:grid-cols-[1.1fr,0.9fr]">
        {/* Standings */}
        <div>
          <h2 className="display text-2xl">Standings</h2>
          <ul className="mt-3 space-y-2" data-testid="standings">
            {sorted.map((s, i) => (
              <li
                key={s.candidateId}
                className={`flex items-center gap-3 border p-3 ${
                  s.isPlayer ? "border-[var(--accent)] bg-[var(--accent)]/5" : "border-[var(--border)]"
                }`}
              >
                <span className="w-6 text-center text-sm font-bold text-[var(--muted)]">{i + 1}</span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-semibold text-[var(--fg)]">
                    {s.candidateName}
                    {s.isPlayer && (
                      <span className="ml-2 text-xs font-semibold uppercase text-[var(--accent)]">You</span>
                    )}
                  </span>
                  <span className="block text-xs text-[var(--muted)]">{s.party}</span>
                </span>
                <span className="text-right">
                  <span className="block text-lg font-bold text-[var(--fg)]">
                    {s.supportShare.toFixed(1)}%
                  </span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        {/* Action panel */}
        <div>
          <div className="flex items-baseline justify-between">
            <h2 className="display text-2xl">This week</h2>
            {!completed && (
              <span className="text-sm text-[var(--muted)]" data-testid="actions-remaining">
                {campaign.actionsRemaining} action{campaign.actionsRemaining === 1 ? "" : "s"} left
              </span>
            )}
          </div>

          {campaign.salientIssues.length > 0 && (
            <div className="mt-2">
              <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                Hot issues this week
              </p>
              <div className="mt-1 flex flex-wrap gap-1.5" data-testid="salient-issues">
                {campaign.salientIssues.map((issue) => (
                  <span
                    key={issue}
                    className="rounded-full bg-[var(--border)] px-2.5 py-0.5 text-xs font-semibold text-[var(--fg-soft)]"
                  >
                    {issue}
                  </span>
                ))}
              </div>
            </div>
          )}

          {notice && (
            <p className="mt-3 rounded-md bg-[var(--bg-elev)] p-3 text-sm text-[var(--fg-soft)]" data-testid="notice">
              {notice}
            </p>
          )}

          {!completed && (
            <>
              <ul className="mt-4 space-y-2" data-testid="actions">
                {campaign.availableActions.map((opt) => (
                  <li key={opt.actionType}>
                    <button
                      type="button"
                      data-testid={`action-${opt.actionType}`}
                      disabled={busy || campaign.actionsRemaining <= 0}
                      onClick={() => onAction(opt)}
                      className="w-full border border-[var(--border)] bg-[var(--bg-elev)] p-3 text-left transition hover:border-[var(--accent)] disabled:opacity-50"
                    >
                      <span className="flex items-center justify-between">
                        <span className="font-semibold text-[var(--fg)]">{opt.label}</span>
                        {opt.suggestedTarget && (
                          <span className="text-xs font-semibold text-[var(--accent)]">
                            {opt.suggestedTarget}
                          </span>
                        )}
                      </span>
                      <span className="mt-1 block text-sm text-[var(--fg-soft)]">{opt.description}</span>
                    </button>
                  </li>
                ))}
              </ul>

              <button
                type="button"
                data-testid="advance-week"
                disabled={busy}
                onClick={onAdvance}
                className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-full bg-[var(--accent)] px-6 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
              >
                {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                {campaign.currentWeek >= campaign.totalWeeks ? "Hold the election" : "Advance to next week"}
              </button>
            </>
          )}

          {campaign.thisWeekActions.length > 0 && (
            <div className="mt-6">
              <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                Actions taken
              </p>
              <ul className="mt-2 space-y-1" data-testid="this-week-actions">
                {campaign.thisWeekActions.map((a, i) => (
                  <li key={i} className="text-sm text-[var(--fg-soft)]">
                    · {a.summary}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      </div>

      {/* Week-by-week log */}
      {campaign.history.length > 0 && (
        <div className="mt-10">
          <h2 className="display text-2xl">Campaign log</h2>
          <ol className="mt-3 space-y-2" data-testid="history">
            {campaign.history.map((w) => (
              <li key={w.weekNumber} className="border-l-2 border-[var(--border)] pl-4 text-sm">
                <span className="font-semibold text-[var(--fg)]">Week {w.weekNumber}:</span>{" "}
                <span className="text-[var(--fg-soft)]">{w.summary}</span>
              </li>
            ))}
          </ol>
        </div>
      )}
    </section>
  );
}

function ResultsBanner({ results }: { results: CivicCampaignResults }) {
  return (
    <div
      className={`mt-6 flex items-center gap-4 border p-5 ${
        results.won ? "border-[var(--accent)] bg-[var(--accent)]/10" : "border-[var(--border)] bg-[var(--bg-elev)]"
      }`}
      data-testid="results-banner"
    >
      {results.won && <Trophy className="h-8 w-8 shrink-0 text-[var(--accent)]" />}
      <div>
        <p className="display text-2xl">{results.won ? "Victory!" : "Campaign over"}</p>
        <p className="text-sm text-[var(--fg-soft)]">{results.outcome}</p>
        <p className="mt-1 text-xs text-[var(--muted)]">
          Finished #{results.finalRank} of {results.fieldSize} · {results.finalSupport.toFixed(1)}% support
        </p>
      </div>
    </div>
  );
}

// Hand-rolled inline-SVG sparkline of the player's support across weeks. No chart dependency.
function SupportTrend({ campaign }: { campaign: CivicCampaignDetail }) {
  const points = campaign.history.map((w) => w.playerSupportAfter);
  if (points.length < 2) return null;

  const w = 320;
  const h = 64;
  const max = Math.max(...points, 1);
  const min = Math.min(...points, 0);
  const range = max - min || 1;
  const coords = points.map((p, i) => {
    const x = (i / (points.length - 1)) * w;
    const y = h - ((p - min) / range) * h;
    return `${x.toFixed(1)},${y.toFixed(1)}`;
  });

  return (
    <div className="mt-6" data-testid="support-trend">
      <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
        Support trend
      </p>
      <svg
        viewBox={`0 0 ${w} ${h}`}
        className="mt-2 h-16 w-full max-w-md"
        preserveAspectRatio="none"
        role="img"
        aria-label="Support over time"
      >
        <polyline
          points={coords.join(" ")}
          fill="none"
          stroke="var(--accent)"
          strokeWidth={2}
          vectorEffect="non-scaling-stroke"
        />
      </svg>
    </div>
  );
}

function errorMessage(err: unknown): string {
  const msg = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
  return msg ?? "Something went wrong. Please try again.";
}
