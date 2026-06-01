import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Megaphone, Trophy } from "lucide-react";
import { listCampaigns, type CivicCampaignSummary } from "@/api/campaignManager";

export default function Campaigns() {
  const [campaigns, setCampaigns] = useState<CivicCampaignSummary[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    void listCampaigns()
      .then(setCampaigns)
      .finally(() => setLoaded(true));
  }, []);

  return (
    <section data-testid="campaigns-page">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
            Campaign Manager
          </p>
          <h1 className="display mt-1 text-4xl">Run a campaign</h1>
          <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
            Take the reins for a candidate and out-campaign their rivals. Pick the issues,
            shape the message, and win the race by election day.
          </p>
        </div>
        <Link
          to="/campaigns/new"
          data-testid="new-campaign"
          className="inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white"
        >
          <Megaphone className="h-4 w-4" />
          New campaign
        </Link>
      </header>

      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Loading…
        </p>
      ) : campaigns.length === 0 ? (
        <div
          className="mt-10 border border-dashed border-[var(--border)] bg-[var(--bg-elev)] p-10 text-center"
          data-testid="empty-state"
        >
          <Megaphone className="mx-auto h-8 w-8 text-[var(--muted)]" />
          <p className="mt-3 text-lg font-semibold text-[var(--fg)]">No campaigns yet</p>
          <p className="mt-1 text-sm text-[var(--fg-soft)]">
            Start your first campaign and try to take a candidate all the way to victory.
          </p>
          <Link
            to="/campaigns/new"
            className="mt-5 inline-block rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white"
          >
            Start a campaign
          </Link>
        </div>
      ) : (
        <ul className="mt-8 grid gap-4 md:grid-cols-2">
          {campaigns.map((c) => (
            <li key={c.id}>
              <Link
                to={`/campaigns/${c.id}`}
                data-testid="campaign-card"
                className="block border border-[var(--border)] bg-[var(--bg-elev)] p-5 transition hover:border-[var(--accent)]"
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                    {c.raceLabel}
                  </span>
                  <StatusChip status={c.status} won={c.won} />
                </div>
                <p className="display mt-2 text-2xl">{c.candidateName}</p>
                <p className="text-sm text-[var(--fg-soft)]">
                  {c.party} · {c.difficulty}
                </p>
                <div className="mt-4 flex items-end justify-between">
                  <div>
                    <p className="text-3xl font-bold text-[var(--fg)]">
                      {c.playerSupport.toFixed(1)}%
                    </p>
                    <p className="text-xs text-[var(--muted)]">
                      {c.isLeading ? "Leading the field" : "Trailing the leader"}
                    </p>
                  </div>
                  <p className="text-sm text-[var(--fg-soft)]">
                    Week {c.currentWeek}/{c.totalWeeks}
                  </p>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function StatusChip({ status, won }: { status: string; won: boolean | null }) {
  if (status === "Completed") {
    return (
      <span
        className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ${
          won ? "bg-[var(--accent)] text-white" : "bg-[var(--border)] text-[var(--fg-soft)]"
        }`}
      >
        {won && <Trophy className="h-3 w-3" />}
        {won ? "Won" : "Lost"}
      </span>
    );
  }
  return (
    <span className="rounded-full border border-[var(--border)] px-2 py-0.5 text-xs font-semibold text-[var(--fg-soft)]">
      {status}
    </span>
  );
}
