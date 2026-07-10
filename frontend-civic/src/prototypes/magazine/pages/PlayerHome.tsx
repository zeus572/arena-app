import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Check } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import {
  getCampaign,
  type CivicCampaignDetail,
  type CivicCampaignSummary,
} from "@/api/campaignManager";
import { getQuests } from "@/api/coalition";
import type { Me, ProvisionSummary, Quest as ApiQuest } from "@/api/coalition";
import { PlayReadToggle, MAGAZINE_HOME } from "../components/PlayReadToggle";

// The gamified, resume-first home ("Mission Control") for a signed-in player
// with a game in motion. Recreated from design_handoff_player_home/ using the
// magazine theme tokens (var(--accent), --fg, --muted, --border, --bg-elev …),
// lucide icons, and react-router <Link>s. It renders inside MagazineLayout, so
// the theme CSS variables are already in scope.

// Coalition states that mean the bill has resolved (matches CoalitionProvisions).
const CLOSED_STATES = ["Passed", "Forked", "Died"];

// Level ladder. There's no discrete "level" field yet, so we derive it from
// reasoning XP (500 XP/level) and name the tiers from this ladder. Every level in
// the rendered range (1–10) has a DISTINCT name; levels past it fall back to a
// unique "Level N" label. (Earlier this fell back to the player's circle name for
// any unnamed level, which made low levels all render the same name — e.g. four
// "Citizen" rungs. This ladder is separate from the coalition Circle ladder.)
const XP_PER_LEVEL = 500;
const TIER_NAMES: Record<number, string> = {
  // Names are kept distinct from the Circle ladder (Citizen/Delegate/Framer/…) so the
  // two systems never echo the same word on the page.
  1: "Voter",
  2: "Advocate",
  3: "Organizer",
  4: "Aspirant",
  5: "Apprentice",
  6: "Co-signer",
  7: "Bridgewright",
  8: "Statewright",
  9: "Whip",
  10: "Speaker",
};

export type PlayerHomeProps = {
  me: Me;
  campaigns: CivicCampaignSummary[];
  provisions: ProvisionSummary[];
};

export default function PlayerHome({ me, campaigns, provisions }: PlayerHomeProps) {
  const { user } = useAuth();

  // Most-recent active campaign → fetch its detail for standings, support
  // history (sparkline), and the "headlines waiting" count.
  const activeCampaign = useMemo(
    () =>
      campaigns
        .filter((c) => c.status === "Active")
        .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))[0] ?? null,
    [campaigns],
  );
  const [detail, setDetail] = useState<CivicCampaignDetail | null>(null);
  useEffect(() => {
    if (!activeCampaign) {
      setDetail(null);
      return;
    }
    let cancelled = false;
    void getCampaign(activeCampaign.id).then((d) => {
      if (!cancelled) setDetail(d ?? null);
    });
    return () => {
      cancelled = true;
    };
  }, [activeCampaign]);

  // Open coalition bills, soonest deadline first.
  const openProvisions = useMemo(
    () =>
      provisions
        .filter((p) => !CLOSED_STATES.includes(p.state))
        .sort((a, b) => deadlineMs(a.deadline) - deadlineMs(b.deadline)),
    [provisions],
  );
  const resumeBill = openProvisions[0] ?? null;
  const closingSoon = openProvisions.slice(0, 3);
  const cultureBill = openProvisions.find((p) => !p.governance) ?? null;

  // ----- Campaign card values -----
  const playerSupport =
    detail?.standings.find((s) => s.isPlayer)?.supportShare ??
    activeCampaign?.playerSupport ??
    0;
  const supportPoints = detail?.history.map((d) => d.playerSupportAfter) ?? [];
  const supportChange =
    supportPoints.length >= 2
      ? supportPoints[supportPoints.length - 1] - supportPoints[0]
      : 0;
  const newsWaiting = detail?.newsItems.length ?? 0;
  const firstNewsSlug = detail?.newsItems[0]?.briefingSlug ?? null;

  // ----- Player record / level -----
  const reasoningXp = me.reasoningXp ?? 0;
  const level = Math.max(1, Math.floor(reasoningXp / XP_PER_LEVEL) + 1);
  const xpInto = reasoningXp % XP_PER_LEVEL;
  const xpFraction = xpInto / XP_PER_LEVEL;
  const tierName = (lvl: number) => TIER_NAMES[lvl] ?? `Level ${lvl}`;

  const skillPct = Math.round((me.skill ?? 0) * 100);
  const governancePct = Math.round((me.record?.governanceRatio ?? 0) * 100);

  // ----- Streak -----
  const cadence = me.cadence?.last7Days ?? [];
  const streak = trailingStreak(cadence);
  const bestRun = longestRun(cadence);

  // ----- Quests -----
  // The backend is the source of truth: it computes each quest's done-state from the
  // acts ledger and grants reward XP once per day. Reading /coalition/quests also
  // triggers that grant, so we refetch when today's activity changes. The client only
  // owns presentation — routing + subtitle, resolved from the quest id below.
  const [serverQuests, setServerQuests] = useState<ApiQuest[]>([]);
  useEffect(() => {
    let cancelled = false;
    void getQuests().then((qs) => {
      if (!cancelled) setServerQuests(qs);
    });
    return () => {
      cancelled = true;
    };
  }, [me.todayReasoning, newsWaiting]);

  const campaignTo =
    activeCampaign?.id && firstNewsSlug
      ? `/campaigns/${activeCampaign.id}/news/${firstNewsSlug}`
      : activeCampaign?.id
        ? `/campaigns/${activeCampaign.id}`
        : "/campaigns";
  const questPresentation: Record<string, { to: string; subtitle?: string }> = {
    "briefing-read": { to: MAGAZINE_HOME },
    "co-sign": { to: resumeBill ? `/coalition/${resumeBill.id}` : "/coalition" },
    "campaign-headline": {
      to: campaignTo,
      subtitle: activeCampaign?.candidateName
        ? `${newsWaiting} waiting · ${activeCampaign.candidateName}`
        : undefined,
    },
    "bridge-culture": {
      to: cultureBill ? `/coalition/${cultureBill.id}` : "/coalition",
      subtitle: cultureBill?.title ?? "Find a culture bill with a carve-out both sides can sign",
    },
  };
  const quests: Quest[] = serverQuests.map((q) => ({
    title: q.title,
    xp: q.xp,
    done: q.done,
    to: questPresentation[q.id]?.to ?? "/coalition",
    subtitle: questPresentation[q.id]?.subtitle,
  }));
  const questsDone = quests.filter((q) => q.done).length;
  const questXpLeft = quests.filter((q) => !q.done).reduce((sum, q) => sum + q.xp, 0);

  const inMotion = (activeCampaign ? 1 : 0) + (resumeBill ? 1 : 0);
  const firstName = (user?.displayName ?? user?.email ?? "there").split(/[\s@]/)[0];

  const bothResume = !!activeCampaign && !!resumeBill;

  return (
    <div data-testid="player-home">
      {/* Play / Read toggle — Play is the current page; Read navigates to the magazine. */}
      <div className="mb-5 flex justify-end">
        <PlayReadToggle active="play" />
      </div>

      {/* Welcome line */}
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <p className="text-sm text-[var(--fg-soft)]">
          Welcome back, <strong className="font-semibold text-[var(--fg)]">{firstName}</strong> — you
          have{" "}
          <strong className="font-semibold text-[var(--accent)]">
            {inMotion} thing{inMotion === 1 ? "" : "s"} in motion
          </strong>{" "}
          and a streak to keep alive.
        </p>
        <Link
          to={MAGAZINE_HOME}
          className="border-b border-[var(--border)] pb-px text-xs font-semibold text-[var(--muted)] transition hover:border-[var(--accent)] hover:text-[var(--accent)]"
        >
          Prefer to just read today? Open the magazine →
        </Link>
      </div>

      {/* ===== Resume row ===== */}
      {(activeCampaign || resumeBill) && (
        <div
          className={`grid gap-4 ${bothResume ? "lg:grid-cols-[1.55fr_1fr]" : "grid-cols-1"}`}
          data-testid="resume-row"
        >
          {activeCampaign && (
            <CampaignResumeCard
              campaignId={activeCampaign.id}
              candidateName={activeCampaign.candidateName}
              party={activeCampaign.party}
              currentDay={activeCampaign.currentDay}
              totalDays={activeCampaign.totalDays}
              daysRemaining={activeCampaign.daysRemaining}
              support={playerSupport}
              points={supportPoints}
              change={supportChange}
              newsWaiting={newsWaiting}
            />
          )}
          {resumeBill && <CoalitionResumeCard p={resumeBill} />}
        </div>
      )}

      {/* ===== Dashboard grid ===== */}
      <div className="mt-4 grid gap-4 lg:grid-cols-[1.5fr_1fr]">
        {/* Left column */}
        <div className="flex flex-col gap-4">
          <QuestsCard
            quests={quests}
            done={questsDone}
            total={quests.length}
            xpLeft={questXpLeft}
          />
          <ClosingSoonCard bills={closingSoon} />
        </div>

        {/* Right column */}
        <div className="flex flex-col gap-4">
          <PlayerStatsCard
            name={user?.displayName ?? user?.email ?? "Player"}
            level={level}
            tierName={tierName(level)}
            xpInto={xpInto}
            xpFraction={xpFraction}
            circleName={me.circleName}
            skillPct={skillPct}
            governancePct={governancePct}
            planksPassed={me.record?.planksPassed ?? 0}
            scarcePoints={me.scarcePoints ?? 0}
          />
          <StreakCard
            streak={streak}
            best={bestRun}
            days={cadence}
            todayReasoning={me.todayReasoning ?? 0}
            dailyCap={me.dailyReasoningCap ?? 0}
          />
        </div>
      </div>

      {/* ===== Progression rail ===== */}
      <ProgressionRail level={level} xpFraction={xpFraction} tierName={tierName} />

      {/* ===== Read-mode footer prompt ===== */}
      <div className="mt-4 flex flex-wrap items-center justify-between gap-4 border border-[var(--border)] bg-[var(--bg-elev)] p-5">
        <div>
          <p className="display text-lg">Not in the mood to play?</p>
          <p className="mt-1 text-sm text-[var(--fg-soft)]">
            Switch to the magazine for the full issue — briefings, the budget, and concept of the day.
          </p>
        </div>
        <Link
          to={MAGAZINE_HOME}
          className="shrink-0 whitespace-nowrap rounded-full border border-[var(--accent)] px-5 py-2.5 text-sm font-semibold text-[var(--accent)] transition hover:bg-[var(--accent)]/5"
          data-testid="footer-open-magazine"
        >
          Open the magazine →
        </Link>
      </div>
    </div>
  );
}

/* ============================ Resume cards ============================ */

function CampaignResumeCard({
  campaignId,
  candidateName,
  party,
  currentDay,
  totalDays,
  daysRemaining,
  support,
  points,
  change,
  newsWaiting,
}: {
  campaignId: string;
  candidateName: string;
  party: string;
  currentDay: number;
  totalDays: number;
  daysRemaining: number;
  support: number;
  points: number[];
  change: number;
  newsWaiting: number;
}) {
  const up = change >= 0;
  return (
    <Link
      to={`/campaigns/${campaignId}`}
      className="group flex flex-col border border-[var(--accent)]/30 bg-[var(--accent)]/5 p-6 transition hover:border-[var(--accent)]"
      data-testid="resume-campaign"
    >
      <div className="flex items-center justify-between gap-3">
        <span className="text-[10px] font-bold uppercase tracking-[0.18em] text-[var(--accent)]">
          Resume · Campaign Manager
        </span>
        <span className="text-[11px] font-semibold text-rose-600">
          {daysRemaining} day{daysRemaining === 1 ? "" : "s"} to election
        </span>
      </div>

      <h2 className="display mt-2 text-3xl leading-[1.05]">{candidateName}</h2>
      <p className="text-[13px] text-[var(--fg-soft)]">
        {party} · Day {currentDay} of {totalDays}
      </p>

      <div className="mt-5 flex items-end gap-6">
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-[0.12em] text-[var(--muted)]">
            Your support
          </p>
          <p className="display mt-0.5 text-[40px] leading-none">
            {support.toFixed(1)}
            <span className="text-xl">%</span>
          </p>
        </div>
        <div className="flex-1">
          <div className="flex items-center justify-between text-[10px] font-semibold uppercase tracking-[0.1em] text-[var(--muted)]">
            <span>Support trend</span>
            {points.length >= 2 && (
              <span className={up ? "text-emerald-600" : "text-rose-600"}>
                {up ? "▲" : "▼"} {up ? "+" : ""}
                {change.toFixed(1)}
              </span>
            )}
          </div>
          <Sparkline points={points} />
        </div>
      </div>

      <div className="mt-4 flex items-center justify-between gap-3 border-t border-[var(--accent)]/20 pt-4">
        <span className="text-xs text-[var(--fg-soft)]">
          <strong className="font-semibold text-[var(--fg)]">
            {newsWaiting} headline{newsWaiting === 1 ? "" : "s"}
          </strong>{" "}
          waiting for your response today
        </span>
        <span className="shrink-0 whitespace-nowrap rounded-full bg-[var(--accent)] px-4 py-2 text-[13px] font-semibold text-white transition group-hover:opacity-90">
          Resume campaign →
        </span>
      </div>
    </Link>
  );
}

function CoalitionResumeCard({ p }: { p: ProvisionSummary }) {
  const breadthPct =
    p.totalBuckets > 0 ? Math.round((p.coveredBuckets / p.totalBuckets) * 100) : 0;
  const deadline = deadlineLabel(p.deadline);
  return (
    <Link
      to={`/coalition/${p.id}`}
      className="group flex flex-col border border-[var(--border)] bg-[var(--bg-elev)] p-6 transition hover:border-[var(--accent)]"
      data-testid="resume-coalition"
    >
      <div className="flex items-center justify-between gap-3">
        <span className="text-[10px] font-bold uppercase tracking-[0.18em] text-[var(--accent)]">
          Resume · Coalition
        </span>
        {deadline && (
          <span className={`text-[11px] font-bold ${deadline.urgent ? "text-rose-600" : "text-[var(--muted)]"}`}>
            Closes in {deadline.short}
          </span>
        )}
      </div>

      <h3 className="display mt-2.5 text-[22px] leading-[1.15]">{p.title}</h3>

      <div className="mt-2.5 flex flex-wrap gap-1.5">
        <Chip className="bg-amber-100 text-amber-700">{p.state}</Chip>
        <DifficultyChip difficulty={p.difficulty} />
        <GovChip governance={p.governance} />
      </div>

      <div className="mt-4">
        <div className="flex items-center justify-between text-[11px] text-[var(--muted)]">
          <span>Coalition breadth</span>
          <span className="font-bold text-[var(--fg)]">
            {p.coveredBuckets} / {p.totalBuckets} buckets
          </span>
        </div>
        <Track value={breadthPct} className="mt-1.5" />
        <p className="mt-2 text-xs leading-relaxed text-[var(--fg-soft)]">
          You're one cross-aisle co-sign from a spanning coalition.
        </p>
      </div>

      <div className="flex-1" />
      <span className="mt-4 rounded-full border border-[var(--accent)] px-4 py-2 text-center text-xs font-semibold text-[var(--accent)] transition group-hover:bg-[var(--accent)]/5">
        Open the bill →
      </span>
    </Link>
  );
}

/* ============================ Dashboard cards ============================ */

type Quest = {
  title: string;
  subtitle?: string;
  xp: number;
  done: boolean;
  to: string;
};

function QuestsCard({
  quests,
  done,
  total,
  xpLeft,
}: {
  quests: Quest[];
  done: number;
  total: number;
  xpLeft: number;
}) {
  const pct = total > 0 ? Math.round((done / total) * 100) : 0;
  const toGo = total - done;
  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5" data-testid="quests-card">
      <div className="flex items-baseline justify-between">
        <h3 className="display text-lg">Today's quests</h3>
        <span className="text-[11px] font-semibold text-[var(--muted)]">
          {done} / {total} done · <span className="text-[var(--accent)]">+{xpLeft} XP left</span>
        </span>
      </div>
      <Track value={pct} className="mt-2 h-[5px]" />

      <div className="mt-3.5">
        {quests.map((q, i) => (
          <Link
            key={q.title}
            to={q.to}
            className={`flex items-center gap-3 py-2.5 transition ${
              i < quests.length - 1 ? "border-b border-[var(--border)]/60" : ""
            } ${q.done ? "" : "hover:opacity-80"}`}
          >
            {q.done ? (
              <span className="flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-full bg-emerald-500">
                <Check className="h-3 w-3 text-white" strokeWidth={3.5} />
              </span>
            ) : (
              <span className="h-[22px] w-[22px] shrink-0 rounded-full border-2 border-[var(--accent)]" />
            )}
            <span className="flex-1">
              <span
                className={`block text-sm font-semibold ${
                  q.done ? "text-[var(--muted)] line-through" : "text-[var(--fg)]"
                }`}
              >
                {q.title}
              </span>
              {q.subtitle && !q.done && (
                <span className="block text-xs text-[var(--muted)]">{q.subtitle}</span>
              )}
            </span>
            <span
              className={`text-[10px] font-bold uppercase tracking-[0.08em] ${
                q.done ? "text-emerald-600" : "text-[var(--accent)]"
              }`}
            >
              +{q.xp} XP
            </span>
          </Link>
        ))}
      </div>

      <div className="mt-3.5 flex items-center justify-between gap-3 bg-[var(--accent)]/5 px-4 py-3">
        <span className="text-xs text-[var(--fg-soft)]">
          Finish all {total} to keep your streak and claim a{" "}
          <strong className="font-semibold text-[var(--accent)]">scarce coalition point</strong>.
        </span>
        <span className="shrink-0 whitespace-nowrap text-[11px] font-bold text-[var(--muted)]">
          {toGo} to go
        </span>
      </div>
    </div>
  );
}

function ClosingSoonCard({ bills }: { bills: ProvisionSummary[] }) {
  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5" data-testid="closing-soon-card">
      <div className="flex items-baseline justify-between">
        <h3 className="display text-lg">Bills closing soon</h3>
        <Link
          to="/coalition"
          className="text-[11px] font-semibold text-[var(--accent)] hover:underline"
        >
          See all coalitions →
        </Link>
      </div>
      <div className="mt-3">
        {bills.length === 0 && (
          <p className="py-4 text-sm text-[var(--muted)]">No open bills right now. Check back soon.</p>
        )}
        {bills.map((p, i) => {
          const d = deadlineLabel(p.deadline);
          return (
            <Link
              key={p.id}
              to={`/coalition/${p.id}`}
              className={`flex items-start gap-3 py-3 transition hover:opacity-80 ${
                i < bills.length - 1 ? "border-b border-[var(--border)]/60" : ""
              }`}
            >
              <span className="min-w-0 flex-1 text-sm font-semibold text-[var(--fg)]">{p.title}</span>
              <div className="flex shrink-0 flex-col items-end gap-1">
                <GovChip governance={p.governance} />
                <DifficultyChip difficulty={p.difficulty} />
                <span
                  className={`text-[11px] font-bold ${
                    d?.urgent ? "text-rose-600" : "text-[var(--muted)]"
                  }`}
                >
                  {d ? `Closes ${d.short}` : "Open"}
                </span>
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}

function PlayerStatsCard({
  name,
  level,
  tierName,
  xpInto,
  xpFraction,
  circleName,
  skillPct,
  governancePct,
  planksPassed,
  scarcePoints,
}: {
  name: string;
  level: number;
  tierName: string;
  xpInto: number;
  xpFraction: number;
  circleName: string | null;
  skillPct: number;
  governancePct: number;
  planksPassed: number;
  scarcePoints: number;
}) {
  const deg = Math.round(xpFraction * 360);
  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5" data-testid="player-stats-card">
      <div className="flex items-center gap-3.5">
        {/* XP / level ring */}
        <div
          className="flex h-[84px] w-[84px] shrink-0 items-center justify-center rounded-full"
          style={{ background: `conic-gradient(var(--accent) ${deg}deg, var(--border) ${deg}deg)` }}
        >
          <div className="flex h-[66px] w-[66px] flex-col items-center justify-center rounded-full bg-[var(--bg-elev)]">
            <span className="display text-[22px] leading-none">L{level}</span>
            <span className="text-[8px] uppercase tracking-[0.1em] text-[var(--muted)]">
              {tierName}
            </span>
          </div>
        </div>
        <div>
          <p className="text-base font-bold text-[var(--fg)]">{name}</p>
          <p className="mt-0.5 text-xs text-[var(--muted)]">
            {xpInto} / {XP_PER_LEVEL} XP to Level {level + 1}
          </p>
          {circleName && (
            <p className="mt-2 inline-flex items-center rounded-full bg-[var(--accent)]/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.1em] text-[var(--accent)]">
              {circleName} Circle
            </p>
          )}
        </div>
      </div>

      <div className="mt-4 flex flex-col gap-3">
        <Meter label="Skill" pct={skillPct} />
        <Meter label="Governance vs culture" pct={governancePct} />
        <div className="mt-0.5 flex gap-2.5">
          <StatTile value={planksPassed} label="Planks passed" />
          <StatTile value={scarcePoints} label="Coalition pts" accent />
        </div>
      </div>
    </div>
  );
}

function StreakCard({
  streak,
  best,
  days,
  todayReasoning,
  dailyCap,
}: {
  streak: number;
  best: number;
  days: boolean[];
  todayReasoning: number;
  dailyCap: number;
}) {
  const labels = weekdayLabels(days.length);
  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5" data-testid="streak-card">
      <div className="flex items-center justify-between">
        <h3 className="display text-lg">{streak}-day streak</h3>
        <span className="text-[11px] font-semibold text-[var(--muted)]">Best: {best}</span>
      </div>
      <div className="mt-3.5 flex gap-1.5">
        {days.map((active, i) => {
          const isToday = i === days.length - 1;
          if (active) {
            return <span key={i} className="h-[30px] flex-1 rounded-sm bg-[var(--accent)]" />;
          }
          if (isToday) {
            return (
              <span
                key={i}
                className="h-[30px] flex-1 rounded-sm border-[1.5px] border-dashed border-[var(--accent)]/50"
              />
            );
          }
          return <span key={i} className="h-[30px] flex-1 rounded-sm bg-[var(--border)]/60" />;
        })}
      </div>
      <div className="mt-1.5 flex justify-between text-[9px] uppercase tracking-[0.06em] text-[var(--muted)]">
        {labels.map((l, i) => (
          <span key={i}>{l}</span>
        ))}
      </div>
      <p className="mt-3.5 text-xs leading-relaxed text-[var(--fg-soft)]">
        Daily reasoning XP{" "}
        <strong className="font-semibold text-[var(--fg)]">
          {todayReasoning} / {dailyCap}
        </strong>{" "}
        used — finish your quests before midnight to keep the streak.
      </p>
    </div>
  );
}

function ProgressionRail({
  level,
  xpFraction,
  tierName,
}: {
  level: number;
  xpFraction: number;
  tierName: (lvl: number) => string;
}) {
  const COUNT = 6;
  const start = Math.max(1, level - 2);
  const levels = Array.from({ length: COUNT }, (_, i) => start + i);
  const currentIndex = level - start;
  const fillPct = Math.min(
    100,
    Math.max(0, ((currentIndex + xpFraction) / (COUNT - 1)) * 100),
  );

  return (
    <div
      className="mt-4 border border-[var(--border)] bg-[var(--bg-elev)] px-6 py-5"
      data-testid="progression-rail"
    >
      <div className="mb-5 flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="display text-lg">Your progression</h3>
        <span className="text-[11px] font-semibold text-[var(--muted)]">
          Next reward at <strong className="font-semibold text-[var(--accent)]">Level {level + 1}</strong>:
          promote-eligible to {tierName(level + 1)} Circle
        </span>
      </div>
      <div className="relative flex items-start justify-between">
        {/* track */}
        <div className="absolute left-0 right-0 top-[13px] h-[3px] bg-[var(--border)]" />
        <div
          className="absolute left-0 top-[13px] h-[3px] bg-[var(--accent)]"
          style={{ width: `${fillPct}%` }}
        />
        {levels.map((lvl) => {
          const state = lvl < level ? "done" : lvl === level ? "current" : "future";
          return (
            <div key={lvl} className="relative flex flex-col items-center gap-2">
              {state === "current" ? (
                <span className="-mt-[3px] flex h-[34px] w-[34px] items-center justify-center rounded-full border-[3px] border-[var(--accent)] bg-[var(--bg-elev)] text-[13px] font-bold text-[var(--accent)] ring-4 ring-[var(--accent)]/10">
                  {lvl}
                </span>
              ) : state === "done" ? (
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--accent)] text-[11px] font-bold text-white">
                  {lvl}
                </span>
              ) : (
                <span className="flex h-7 w-7 items-center justify-center rounded-full border-2 border-[var(--border)] bg-[var(--bg-elev)] text-[11px] font-bold text-[var(--muted)]">
                  {lvl}
                </span>
              )}
              <span
                className={`text-[10px] ${
                  state === "current" ? "font-bold text-[var(--accent)]" : "text-[var(--muted)]"
                }`}
              >
                {tierName(lvl)}
                {state === "current" ? " · you" : ""}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ============================ Small UI atoms ============================ */

function Chip({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <span
      className={`rounded-full px-2 py-0.5 text-[9px] font-bold uppercase tracking-[0.1em] ${className}`}
    >
      {children}
    </span>
  );
}

// Difficulty colors mirror CoalitionProvisions.tsx's difficultyBadge.
function DifficultyChip({ difficulty }: { difficulty: string }) {
  const map: Record<string, string> = {
    Narrow: "bg-emerald-100 text-emerald-700",
    Moderate: "bg-amber-100 text-amber-700",
    Wide: "bg-rose-100 text-rose-700",
  };
  return <Chip className={map[difficulty] ?? "bg-[var(--border)] text-[var(--muted)]"}>{difficulty}</Chip>;
}

// Governance vs culture tag — same axis CoalitionProvisions shows. Culture bills
// are the ones the "Bridge a culture-war bill" quest targets, so we surface
// the tag here to make that connection legible at a glance.
function GovChip({ governance }: { governance: boolean }) {
  return governance ? (
    <Chip className="bg-[var(--border)] text-[var(--muted)]">Governance</Chip>
  ) : (
    <Chip className="bg-violet-100 text-violet-700">Culture</Chip>
  );
}

function Track({ value, className = "" }: { value: number; className?: string }) {
  return (
    <div className={`h-1.5 overflow-hidden rounded-full bg-[var(--border)] ${className}`}>
      <div
        className="h-full rounded-full bg-[var(--accent)]"
        style={{ width: `${Math.min(100, Math.max(0, value))}%` }}
      />
    </div>
  );
}

function Meter({ label, pct }: { label: string; pct: number }) {
  return (
    <div>
      <div className="flex items-center justify-between text-[11px] text-[var(--muted)]">
        <span>{label}</span>
        <span className="font-bold text-[var(--fg)]">{pct}%</span>
      </div>
      <Track value={pct} className="mt-1" />
    </div>
  );
}

function StatTile({ value, label, accent = false }: { value: number; label: string; accent?: boolean }) {
  return (
    <div className="flex-1 border border-[var(--border)] p-2.5">
      <p className={`display text-2xl leading-none ${accent ? "text-[var(--accent)]" : ""}`}>{value}</p>
      <p className="mt-1 text-[10px] uppercase tracking-[0.08em] text-[var(--muted)]">{label}</p>
    </div>
  );
}

// Hand-rolled support sparkline — same approach as SupportTrend in CampaignDashboard.
function Sparkline({ points }: { points: number[] }) {
  if (points.length < 2) {
    return <div className="mt-1.5 h-[46px]" />;
  }
  const w = 240;
  const h = 48;
  const max = Math.max(...points);
  const min = Math.min(...points);
  const range = max - min || 1;
  const coords = points
    .map((p, i) => {
      const x = (i / (points.length - 1)) * w;
      const y = h - ((p - min) / range) * h;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
  return (
    <svg
      viewBox={`0 0 ${w} ${h}`}
      preserveAspectRatio="none"
      className="mt-1.5 block h-[46px] w-full"
      role="img"
      aria-label="Support trend over time"
    >
      <polyline
        points={coords}
        fill="none"
        stroke="var(--accent)"
        strokeWidth={2.5}
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

/* ============================ Pure helpers ============================ */

function deadlineMs(deadline: string | null): number {
  if (!deadline) return Number.POSITIVE_INFINITY;
  const ms = new Date(deadline).getTime();
  return Number.isNaN(ms) ? Number.POSITIVE_INFINITY : ms;
}

// Short, friendly countdown ("6h", "1d", "3d") + an urgency flag, matching the
// Deadline component in CoalitionProvisions.
function deadlineLabel(deadline: string | null): { short: string; urgent: boolean } | null {
  const ms = deadlineMs(deadline);
  if (!Number.isFinite(ms)) return null;
  const remaining = ms - Date.now();
  if (remaining <= 0) return { short: "now", urgent: true };
  const hours = remaining / 3_600_000;
  if (hours < 24) return { short: `${Math.max(1, Math.ceil(hours))}h`, urgent: true };
  const days = Math.ceil(hours / 24);
  return { short: `${days}d`, urgent: days <= 2 };
}

// Consecutive active days ending now. A pending today (the final cell still
// false) doesn't break the streak — we count back from the most recent done day.
function trailingStreak(days: boolean[]): number {
  let i = days.length - 1;
  if (i >= 0 && !days[i]) i--;
  let n = 0;
  for (; i >= 0; i--) {
    if (days[i]) n++;
    else break;
  }
  return n;
}

function longestRun(days: boolean[]): number {
  let best = 0;
  let run = 0;
  for (const active of days) {
    run = active ? run + 1 : 0;
    if (run > best) best = run;
  }
  return best;
}

// Weekday short labels for the last `count` days ending today; the final cell
// reads "Today".
function weekdayLabels(count: number): string[] {
  if (count <= 0) return [];
  const today = new Date();
  const out: string[] = [];
  for (let i = count - 1; i >= 0; i--) {
    if (i === 0) {
      out.push("Today");
      continue;
    }
    const d = new Date(today);
    d.setDate(today.getDate() - i);
    out.push(d.toLocaleDateString("en-US", { weekday: "short" }));
  }
  return out;
}

