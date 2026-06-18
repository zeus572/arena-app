import { useEffect, useState } from "react";
import { useAuth } from "@/auth/AuthContext";
import { listCampaigns, type CivicCampaignSummary } from "@/api/campaignManager";
import { getMe, listProvisions, type Me, type ProvisionSummary } from "@/api/coalition";
import MagazineHome from "./Home";
import PlayerHome from "./PlayerHome";

// The "/" route branches on auth + "has a game in motion":
//   - signed-in WITH an active campaign or live coalition play → PlayerHome
//     ("Mission Control"), the resume-first gamified dashboard.
//   - everyone else (signed-out, or signed-in with no active game) → the
//     magazine Home. The first-time / empty state is intentionally the magazine.
// The magazine stays explicitly reachable for players via the /magazine route
// (the Play/Read toggle + "Open the magazine" links point there).

const CLOSED_STATES = ["Passed", "Forked", "Died"];

type GameData = {
  me: Me;
  campaigns: CivicCampaignSummary[];
  provisions: ProvisionSummary[];
};

function hasActiveGame({ me, campaigns, provisions }: GameData): boolean {
  const hasActiveCampaign = campaigns.some((c) => c.status === "Active");
  const hasOpenBills = provisions.some((p) => !CLOSED_STATES.includes(p.state));
  // We can't yet read which coalitions a user has *joined* (no "my coalitions"
  // endpoint), so we approximate live coalition play as: open bills exist AND
  // the player has any coalition history/activity.
  const coalitionEngaged =
    (me.record?.planksPassed ?? 0) > 0 ||
    (me.todayReasoning ?? 0) > 0 ||
    (me.reasoningXp ?? 0) > 0;
  return hasActiveCampaign || (hasOpenBills && coalitionEngaged);
}

export default function HomeIndex() {
  const { isAuthenticated, isLoading } = useAuth();
  const [decided, setDecided] = useState(false);
  const [data, setData] = useState<GameData | null>(null);

  useEffect(() => {
    if (isLoading) return;
    if (!isAuthenticated) {
      setData(null);
      setDecided(true);
      return;
    }
    let cancelled = false;
    setDecided(false);
    void Promise.all([
      getMe().catch(() => null),
      listCampaigns().catch(() => [] as CivicCampaignSummary[]),
      listProvisions().catch(() => [] as ProvisionSummary[]),
    ]).then(([me, campaigns, provisions]) => {
      if (cancelled) return;
      setData(me ? { me, campaigns, provisions } : null);
      setDecided(true);
    });
    return () => {
      cancelled = true;
    };
  }, [isAuthenticated, isLoading]);

  if (isLoading || (isAuthenticated && !decided)) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="home-loading">
        Loading…
      </p>
    );
  }

  if (data && hasActiveGame(data)) {
    return <PlayerHome me={data.me} campaigns={data.campaigns} provisions={data.provisions} />;
  }
  return <MagazineHome />;
}
