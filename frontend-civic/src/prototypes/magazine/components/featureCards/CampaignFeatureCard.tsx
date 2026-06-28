import { Link } from "react-router-dom";
import { Megaphone } from "lucide-react";
import type { CivicCampaignSummary } from "@/api/campaignManager";

/* The "Manage a campaign" feature tile. Extracted verbatim from the Home grid so
   the rotator can slot it in alongside the other feature cards. Shows the player's
   active (or most recent finished) campaign, or a CTA to start one. */
export function CampaignFeatureCard({
  featuredCampaign,
}: {
  featuredCampaign: CivicCampaignSummary | null;
}) {
  if (featuredCampaign) {
    return (
      <Link
        to={`/campaigns/${featuredCampaign.id}`}
        data-testid="campaign-cta"
        className="flex h-full flex-col justify-between border border-[var(--accent)] bg-[var(--accent)]/5 p-6 transition hover:bg-[var(--accent)]/10"
      >
        <div>
          <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
            <Megaphone className="h-4 w-4" /> Campaign Manager
            <span className="text-[var(--muted)]">· {featuredCampaign.status === "Active" ? "in progress" : "finished"}</span>
          </p>
          <h2 className="display mt-2 text-3xl">{featuredCampaign.candidateName}</h2>
          <p className="mt-1 text-sm leading-relaxed text-[var(--fg-soft)]">
            {featuredCampaign.raceLabel} · {featuredCampaign.party}
          </p>
          {featuredCampaign.status === "Active" ? (
            <p className="mt-3 text-sm text-[var(--fg)]">
              <span className="font-semibold">{featuredCampaign.playerSupport.toFixed(0)}% support</span>
              {" — "}{featuredCampaign.isLeading ? "leading the race" : "trailing"} · day {featuredCampaign.currentDay} of {featuredCampaign.totalDays} ({featuredCampaign.daysRemaining}d left)
            </p>
          ) : (
            <p className="mt-3 text-sm text-[var(--fg)]">
              <span className="font-semibold">{featuredCampaign.won ? "Won the race" : "Lost the race"}</span>
              {" — "}finished at {featuredCampaign.playerSupport.toFixed(0)}% support
            </p>
          )}
        </div>
        <span className="mt-5 inline-block w-fit rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white">
          {featuredCampaign.status === "Active" ? "Resume campaign →" : "View result →"}
        </span>
      </Link>
    );
  }

  return (
    <Link
      to="/campaigns"
      data-testid="campaign-cta"
      className="flex h-full flex-col justify-between border border-[var(--accent)] bg-[var(--accent)]/5 p-6 transition hover:bg-[var(--accent)]/10"
    >
      <div>
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          <Megaphone className="h-4 w-4" /> Campaign Manager
        </p>
        <h2 className="display mt-2 text-3xl">Run a campaign to election day.</h2>
        <p className="mt-1 text-sm leading-relaxed text-[var(--fg-soft)]">
          Take the reins for a candidate, respond to the real headlines, and try to win the race
          before the clock runs out.
        </p>
      </div>
      <span className="mt-5 inline-block w-fit rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white">
        Manage a campaign →
      </span>
    </Link>
  );
}
