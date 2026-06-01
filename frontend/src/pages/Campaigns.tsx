import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listCampaigns } from "@/api/client";
import type { CampaignSummary } from "@/api/types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { Megaphone, Plus, Trophy, Flag } from "lucide-react";

function statusClasses(status: string): string {
  if (status === "Completed") return "bg-blue-500/10 text-blue-600";
  if (status === "Abandoned") return "bg-red-500/10 text-red-500";
  return "bg-emerald-500/10 text-emerald-600";
}

function CampaignCard({ c }: { c: CampaignSummary }) {
  const completed = c.status === "Completed";
  const won = c.won === true;
  return (
    <Link to={`/campaigns/${c.id}`} className="no-underline block group">
      <div className="rounded-xl border border-border bg-card p-4 transition-colors group-hover:border-primary/30">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="text-sm font-semibold text-card-foreground truncate group-hover:text-primary transition-colors">
              {c.candidateName}
            </p>
            <p className="text-xs text-muted-foreground mt-0.5">vs {c.opponentName}</p>
          </div>
          <span
            className={cn(
              "rounded-full px-2 py-0.5 text-[10px] font-semibold shrink-0",
              statusClasses(c.status)
            )}
          >
            {c.status}
          </span>
        </div>

        <p className="text-[11px] text-muted-foreground mt-2">{c.theme}</p>

        <div className="flex items-center gap-4 mt-3 text-[11px] text-muted-foreground">
          <span>
            Week {Math.min(c.currentWeek, c.totalWeeks)}/{c.totalWeeks}
          </span>
          <span className="rounded bg-secondary px-1.5 py-0.5">{c.difficulty}</span>
          <span className="ml-auto flex items-center gap-1 font-semibold text-card-foreground">
            {Math.round(c.approval)}% approval
          </span>
        </div>

        {completed && (
          <div
            className={cn(
              "mt-3 flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[11px] font-semibold",
              won ? "bg-amber-500/10 text-amber-600" : "bg-secondary text-muted-foreground"
            )}
          >
            {won ? <Trophy size={12} /> : <Flag size={12} />}
            {won ? "Won" : "Lost"} — final {Math.round(c.finalApproval ?? c.approval)}%
          </div>
        )}
      </div>
    </Link>
  );
}

export default function Campaigns() {
  const [campaigns, setCampaigns] = useState<CampaignSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listCampaigns()
      .then((data) => {
        if (active) setCampaigns(data);
      })
      .catch(() => {
        if (active) setError("Could not load campaigns.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-2">
          <Megaphone size={20} className="text-primary" />
          <h1 className="text-2xl font-bold text-foreground">Campaign Manager</h1>
        </div>
        <Link to="/campaigns/new">
          <Button size="sm" className="gap-1.5 text-xs">
            <Plus size={13} />
            New Campaign
          </Button>
        </Link>
      </div>

      {loading ? (
        <div className="flex flex-col gap-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-5 h-24 animate-pulse" />
          ))}
        </div>
      ) : error ? (
        <p className="text-sm text-destructive text-center py-8">{error}</p>
      ) : campaigns.length === 0 ? (
        <div className="rounded-xl border border-dashed border-border bg-card/50 text-center py-16 px-4">
          <Megaphone size={32} className="mx-auto text-muted-foreground/30 mb-3" />
          <p className="text-sm font-medium text-foreground">No campaigns yet</p>
          <p className="text-xs text-muted-foreground mt-1 mb-4">
            Run for office: pick a candidate, manage resources, and win the debates.
          </p>
          <Link to="/campaigns/new">
            <Button size="sm" className="gap-1.5 text-xs">
              <Plus size={13} />
              Start your first campaign
            </Button>
          </Link>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {campaigns.map((c) => (
            <CampaignCard key={c.id} c={c} />
          ))}
        </div>
      )}
    </main>
  );
}
