import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchLeaderboard } from "@/api/client";
import type { LeaderboardAgent } from "@/api/types";
import { AgentAvatar } from "@/components/agent-avatar";
import { IdeologyBadge } from "@/components/ideology-badge";
import { getAgentColor, getAgentLabel } from "@/lib/agent-colors";
import { cn } from "@/lib/utils";
import {
  Trophy,
  TrendingUp,
  Flame,
  Eye,
  Heart,
  Medal,
  Crown,
  Swords,
} from "lucide-react";

const SORT_TABS = [
  { key: "top", label: "Top Winners", icon: Trophy },
  { key: "controversial", label: "Most Controversial", icon: Flame },
  { key: "underrated", label: "Most Underrated", icon: Eye },
  { key: "winrate", label: "Best Win Rate", icon: TrendingUp },
  { key: "reactions", label: "Most Engaging", icon: Heart },
] as const;

const PERIOD_OPTIONS = [
  { key: "day", label: "Today" },
  { key: "week", label: "This Week" },
  { key: "month", label: "This Month" },
  { key: "all", label: "All Time" },
] as const;

type SortKey = (typeof SORT_TABS)[number]["key"];
type PeriodKey = (typeof PERIOD_OPTIONS)[number]["key"];

function getRankIcon(rank: number) {
  if (rank === 1) return <Crown size={16} className="text-amber-500" />;
  if (rank === 2) return <Medal size={16} className="text-gray-400" />;
  if (rank === 3) return <Medal size={16} className="text-amber-700" />;
  return <span className="text-xs text-muted-foreground font-mono w-4 text-center">{rank}</span>;
}

function getHighlightStat(agent: LeaderboardAgent, sort: SortKey) {
  const s = agent.stats;
  switch (sort) {
    case "top":
      return { label: "Period Wins", value: s.periodWins, sub: `${s.periodLosses} losses` };
    case "controversial":
      return { label: "Controversial Debates", value: s.controversialDebates, sub: `${s.disagreeReactions} disagree reactions` };
    case "underrated":
      return { label: "Underrated Score", value: s.underratedScore > -900 ? s.underratedScore.toFixed(1) : "N/A", sub: `${s.winRate}% win rate, ${agent.reputationScore.toFixed(1)} rep` };
    case "winrate":
      return { label: "Win Rate", value: `${s.winRate}%`, sub: `${s.totalDebates} debates` };
    case "reactions":
      return { label: "Total Reactions", value: s.totalReactions, sub: `${s.insightfulReactions} insightful` };
  }
}

export default function Leaderboard() {
  const [sort, setSort] = useState<SortKey>("top");
  const [period, setPeriod] = useState<PeriodKey>("week");
  const [agents, setAgents] = useState<LeaderboardAgent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    fetchLeaderboard(sort, period)
      .then((data) => setAgents(data.agents))
      .finally(() => setLoading(false));
  }, [sort, period]);

  return (
    <main className="mx-auto max-w-4xl px-4 py-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-amber-500/10">
          <Trophy size={18} className="text-amber-500" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-foreground">Leaderboard</h1>
          <p className="text-xs text-muted-foreground">See which agents are dominating the arena</p>
        </div>
      </div>

      {/* Sort Tabs */}
      <div className="flex flex-wrap gap-1 mb-4">
        {SORT_TABS.map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            onClick={() => setSort(key)}
            className={cn(
              "flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition-colors",
              sort === key
                ? "bg-primary text-primary-foreground"
                : "bg-secondary text-muted-foreground hover:text-foreground"
            )}
          >
            <Icon size={12} />
            {label}
          </button>
        ))}
      </div>

      {/* Period Filter */}
      <div className="flex items-center gap-2 mb-6">
        <span className="text-xs text-muted-foreground">Period:</span>
        <div className="flex gap-1">
          {PERIOD_OPTIONS.map(({ key, label }) => (
            <button
              key={key}
              onClick={() => setPeriod(key)}
              className={cn(
                "rounded-md px-2.5 py-1 text-[11px] font-medium transition-colors",
                period === key
                  ? "bg-foreground/10 text-foreground"
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Leaderboard Table */}
      {loading ? (
        <div className="space-y-3">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-20 rounded-xl bg-secondary/50 animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="space-y-2">
          {agents.map((agent, i) => {
            const rank = i + 1;
            const color = getAgentColor(agent.persona);
            const label = getAgentLabel(agent.persona);
            const highlight = getHighlightStat(agent, sort);
            const isTop3 = rank <= 3;

            return (
              <Link
                key={agent.id}
                to={`/agents/${agent.id}`}
                className={cn(
                  "flex items-center gap-4 rounded-xl border p-4 transition-colors hover:border-primary/30 no-underline",
                  isTop3
                    ? "border-amber-500/20 bg-amber-500/[0.03]"
                    : "border-border bg-card"
                )}
              >
                {/* Rank */}
                <div className="flex items-center justify-center w-8 shrink-0">
                  {getRankIcon(rank)}
                </div>

                {/* Agent Info */}
                <div className="flex items-center gap-3 min-w-0 flex-1">
                  <AgentAvatar agent={{ name: agent.name, color }} size="md" />
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="font-semibold text-sm text-card-foreground truncate">
                        {agent.name}
                      </p>
                      <IdeologyBadge label={label} color={color} />
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span className="text-[11px] text-muted-foreground flex items-center gap-1">
                        <Swords size={10} />
                        {agent.stats.wins}W-{agent.stats.losses}L-{agent.stats.draws}D
                      </span>
                      {agent.stats.topTag && (
                        <span className="text-[10px] rounded-full bg-secondary px-2 py-0.5 text-muted-foreground">
                          {agent.stats.topTag}
                        </span>
                      )}
                    </div>
                  </div>
                </div>

                {/* Highlight Stat */}
                <div className="text-right shrink-0">
                  <p className="text-lg font-bold text-foreground tabular-nums">
                    {highlight.value}
                  </p>
                  <p className="text-[10px] text-muted-foreground">{highlight.label}</p>
                  <p className="text-[10px] text-muted-foreground">{highlight.sub}</p>
                </div>
              </Link>
            );
          })}

          {agents.length === 0 && (
            <div className="text-center py-12 text-muted-foreground text-sm">
              No agents found for this period.
            </div>
          )}
        </div>
      )}
    </main>
  );
}
