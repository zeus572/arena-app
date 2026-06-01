import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  getCampaign,
  advanceWeek,
  previewAllocation,
  respondToEvent,
  runDebate,
  getCampaignResults,
} from "@/api/client";
import type {
  CampaignDetail,
  CampaignResults,
  CampaignEvent,
  CampaignWeek,
  ActivityAllocation,
  AllocationPreviewResult,
} from "@/api/types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import {
  ArrowLeft,
  DollarSign,
  Clock,
  Users,
  TrendingUp,
  Megaphone,
  Trophy,
  Flag,
  Swords,
  AlertCircle,
} from "lucide-react";

/* ─────────────────── Approval sparkline ─────────────────── */

function ApprovalSparkline({ weeks }: { weeks: CampaignWeek[] }) {
  const points = useMemo(
    () => [...weeks].sort((a, b) => a.weekNumber - b.weekNumber).map((w) => w.approvalRating),
    [weeks]
  );
  if (points.length < 2) return null;

  const w = 240;
  const h = 56;
  const pad = 4;
  const min = Math.min(...points, 0);
  const max = Math.max(...points, 100);
  const range = max - min || 1;
  const coords = points.map((p, i) => {
    const x = pad + (i / (points.length - 1)) * (w - pad * 2);
    const y = h - pad - ((p - min) / range) * (h - pad * 2);
    return [x, y] as const;
  });
  const line = coords.map(([x, y]) => `${x},${y}`).join(" ");
  const [lastX, lastY] = coords[coords.length - 1];

  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} className="overflow-visible">
      <polyline
        points={line}
        fill="none"
        className="stroke-primary"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx={lastX} cy={lastY} r={3} className="fill-primary" />
    </svg>
  );
}

/* ─────────────────── Resource panel ─────────────────── */

function ResourceStat({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof DollarSign;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded-lg border border-border bg-card px-3 py-2.5">
      <div className="flex items-center gap-1.5 text-[10px] uppercase tracking-wider text-muted-foreground">
        <Icon size={11} />
        {label}
      </div>
      <p className="text-base font-bold text-card-foreground mt-0.5">{value}</p>
    </div>
  );
}

/* ─────────────────── Activity allocation form ─────────────────── */

interface ActivityState {
  adBudget: string;
  townHallCount: string;
  fundraisingStaff: string;
  oppResearch: boolean;
  debatePrep: boolean;
  polling: boolean;
}

const EMPTY_ACTIVITIES: ActivityState = {
  adBudget: "",
  townHallCount: "",
  fundraisingStaff: "",
  oppResearch: false,
  debatePrep: false,
  polling: false,
};

function buildAllocations(s: ActivityState): ActivityAllocation[] {
  const out: ActivityAllocation[] = [];
  const ad = Number(s.adBudget);
  if (ad > 0) out.push({ type: "Advertising", budget: ad });
  const th = Number(s.townHallCount);
  if (th > 0) out.push({ type: "TownHall", count: th });
  const fr = Number(s.fundraisingStaff);
  if (fr > 0) out.push({ type: "Fundraising", staffCount: fr });
  if (s.oppResearch) out.push({ type: "OppResearch", count: 1 });
  if (s.debatePrep) out.push({ type: "DebatePrep", count: 1 });
  if (s.polling) out.push({ type: "Polling", count: 1 });
  return out;
}

/* ─────────────────── Main dashboard ─────────────────── */

export default function CampaignDashboard() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<CampaignDetail | null>(null);
  const [results, setResults] = useState<CampaignResults | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [activities, setActivities] = useState<ActivityState>(EMPTY_ACTIVITIES);
  const [preview, setPreview] = useState<AllocationPreviewResult | null>(null);

  const refresh = useCallback(async () => {
    if (!id) return;
    const d = await getCampaign(id);
    setDetail(d);
    if (d.campaign.status === "Completed") {
      try {
        setResults(await getCampaignResults(id));
      } catch {
        /* ignore */
      }
    }
  }, [id]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    refresh()
      .catch(() => {
        if (active) setError("Could not load this campaign.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [refresh]);

  // Live affordability preview as the player edits activities
  useEffect(() => {
    if (!id || !detail || detail.campaign.status !== "Active") {
      setPreview(null);
      return;
    }
    const allocs = buildAllocations(activities);
    if (allocs.length === 0) {
      setPreview(null);
      return;
    }
    let active = true;
    previewAllocation(id, { activities: allocs })
      .then((p) => {
        if (active) setPreview(p);
      })
      .catch(() => {
        if (active) setPreview(null);
      });
    return () => {
      active = false;
    };
  }, [id, activities, detail]);

  const handleRespond = async (event: CampaignEvent, optionId: string) => {
    if (!id) return;
    setBusy(true);
    setActionError(null);
    try {
      await respondToEvent(id, event.id, optionId);
      await refresh();
    } catch {
      setActionError("Could not record your response.");
    } finally {
      setBusy(false);
    }
  };

  const handleAdvance = async () => {
    if (!id) return;
    setBusy(true);
    setActionError(null);
    try {
      await advanceWeek(id, { activities: buildAllocations(activities) });
      setActivities(EMPTY_ACTIVITIES);
      setPreview(null);
      await refresh();
    } catch {
      setActionError("Could not advance the week. Check your allocations and any pending debate.");
    } finally {
      setBusy(false);
    }
  };

  const handleDebate = async (skip: boolean) => {
    if (!id) return;
    setBusy(true);
    setActionError(null);
    try {
      await runDebate(id, { skip });
      await refresh();
    } catch {
      setActionError("Could not run the debate.");
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <main className="mx-auto max-w-4xl px-4 py-8">
        <div className="h-8 w-48 bg-secondary/50 rounded animate-pulse mb-6" />
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-16 rounded-lg bg-secondary/50 animate-pulse" />
          ))}
        </div>
        <div className="h-48 rounded-xl bg-secondary/50 animate-pulse" />
      </main>
    );
  }

  if (error || !detail) {
    return (
      <main className="mx-auto max-w-4xl px-4 py-8">
        <Link
          to="/campaigns"
          className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground mb-4 no-underline"
        >
          <ArrowLeft size={13} /> Back to campaigns
        </Link>
        <p className="text-sm text-destructive text-center py-8">{error ?? "Campaign not found."}</p>
      </main>
    );
  }

  const { campaign, resources, currentApproval, weeks, pendingEvents, debateMilestoneDue } = detail;
  const isActive = campaign.status === "Active";
  const completed = campaign.status === "Completed";

  return (
    <main className="mx-auto max-w-4xl px-4 py-8">
      <Link
        to="/campaigns"
        className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground mb-4 no-underline"
      >
        <ArrowLeft size={13} /> Back to campaigns
      </Link>

      {/* Header */}
      <div className="flex items-start justify-between gap-3 mb-6">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <Megaphone size={18} className="text-primary shrink-0" />
            <h1 className="text-xl font-bold text-foreground truncate">{campaign.candidateName}</h1>
          </div>
          <p className="text-xs text-muted-foreground mt-1">
            {campaign.theme} · vs {campaign.opponentName} · {campaign.difficulty}
          </p>
        </div>
        <span className="text-xs font-medium text-muted-foreground shrink-0">
          Week {Math.min(campaign.currentWeek, campaign.totalWeeks)}/{campaign.totalWeeks}
        </span>
      </div>

      {/* Results banner */}
      {completed && (
        <div
          className={cn(
            "rounded-xl border p-5 mb-6",
            campaign.won
              ? "border-amber-500/30 bg-amber-500/5"
              : "border-border bg-card"
          )}
        >
          <div className="flex items-center gap-2 mb-1">
            {campaign.won ? (
              <Trophy size={18} className="text-amber-500" />
            ) : (
              <Flag size={18} className="text-muted-foreground" />
            )}
            <h2 className="text-lg font-bold text-foreground">
              {campaign.won ? "Victory!" : "Defeated"}
            </h2>
          </div>
          <p className="text-sm text-muted-foreground">{results?.outcome ?? ""}</p>
          <div className="flex flex-wrap gap-4 mt-3 text-xs text-muted-foreground">
            <span>
              Final approval:{" "}
              <span className="font-semibold text-foreground">
                {Math.round(results?.finalApproval ?? campaign.finalApproval ?? currentApproval)}%
              </span>
            </span>
            {results && (
              <span>
                Debates won:{" "}
                <span className="font-semibold text-foreground">
                  {results.debatesWon}/{results.debatesPlayed}
                </span>
              </span>
            )}
          </div>
        </div>
      )}

      {/* Resource panel */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
        <ResourceStat icon={DollarSign} label="Budget" value={`$${Math.round(resources.budget).toLocaleString()}`} />
        <ResourceStat icon={Clock} label="Time" value={`${resources.timeUnits}`} />
        <ResourceStat icon={Users} label="Staff" value={`${resources.staffCount}`} />
        <ResourceStat icon={TrendingUp} label="Momentum" value={`${Math.round(resources.momentum)}`} />
      </div>

      {/* Approval display */}
      <div className="rounded-xl border border-border bg-card p-5 mb-6">
        <div className="flex items-center justify-between gap-4">
          <div>
            <p className="text-[10px] uppercase tracking-wider text-muted-foreground">Approval</p>
            <p className="text-4xl font-black text-foreground leading-none mt-1">
              {Math.round(currentApproval)}
              <span className="text-lg font-bold text-muted-foreground">%</span>
            </p>
            <p className="text-[10px] text-muted-foreground mt-1">Win at 50% on the final week</p>
          </div>
          <ApprovalSparkline weeks={weeks} />
        </div>
      </div>

      {actionError && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 mb-4 text-xs text-destructive">
          <AlertCircle size={13} /> {actionError}
        </div>
      )}

      {/* Debate milestone */}
      {isActive && debateMilestoneDue && (
        <div className="rounded-xl border border-primary/30 bg-primary/5 p-5 mb-6">
          <div className="flex items-center gap-2 mb-1">
            <Swords size={16} className="text-primary" />
            <h2 className="text-sm font-bold text-foreground">Debate Night!</h2>
          </div>
          <p className="text-xs text-muted-foreground mb-3">
            A debate against {campaign.opponentName} is due this week. Win it to boost your approval.
          </p>
          <div className="flex flex-wrap items-center gap-2">
            <Button size="sm" className="text-xs" disabled={busy} onClick={() => handleDebate(false)}>
              {busy ? "Running..." : "Run Debate"}
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="text-xs"
              disabled={busy}
              onClick={() => handleDebate(true)}
            >
              Skip (polling penalty)
            </Button>
            {detail.activeDebateId && (
              <Link
                to={`/debates/${detail.activeDebateId}`}
                className="text-xs text-primary hover:underline ml-1"
              >
                View last debate
              </Link>
            )}
          </div>
        </div>
      )}

      {/* Pending events */}
      {isActive && pendingEvents.length > 0 && (
        <div className="flex flex-col gap-3 mb-6">
          {pendingEvents.map((ev) => (
            <div key={ev.id} className="rounded-xl border border-amber-500/30 bg-amber-500/5 p-4">
              <div className="flex items-center gap-2 mb-1">
                <span
                  className={cn(
                    "rounded-full px-2 py-0.5 text-[9px] font-semibold uppercase tracking-wider",
                    ev.type === "Crisis"
                      ? "bg-red-500/15 text-red-600"
                      : ev.type === "Opportunity"
                      ? "bg-emerald-500/15 text-emerald-600"
                      : "bg-secondary text-muted-foreground"
                  )}
                >
                  {ev.type}
                </span>
                <p className="text-sm font-semibold text-card-foreground">{ev.title}</p>
              </div>
              <p className="text-xs text-muted-foreground mb-3">{ev.description}</p>
              <div className="flex flex-wrap gap-2">
                {ev.options.map((opt) => (
                  <Button
                    key={opt.id}
                    size="sm"
                    variant="secondary"
                    className="text-xs"
                    disabled={busy}
                    onClick={() => handleRespond(ev, opt.id)}
                  >
                    {opt.label}
                  </Button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Activity allocation form */}
      {isActive && !debateMilestoneDue && (
        <div className="rounded-xl border border-border bg-card p-5 mb-6">
          <h2 className="text-sm font-bold text-card-foreground mb-3">Plan this week</h2>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <label className="flex flex-col gap-1">
              <span className="text-[11px] font-medium text-muted-foreground">Advertising ($)</span>
              <input
                type="number"
                min={0}
                value={activities.adBudget}
                onChange={(e) => setActivities((a) => ({ ...a, adBudget: e.target.value }))}
                placeholder="0"
                className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
              />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-[11px] font-medium text-muted-foreground">Town halls (count)</span>
              <input
                type="number"
                min={0}
                value={activities.townHallCount}
                onChange={(e) => setActivities((a) => ({ ...a, townHallCount: e.target.value }))}
                placeholder="0"
                className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
              />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-[11px] font-medium text-muted-foreground">Fundraising (staff)</span>
              <input
                type="number"
                min={0}
                value={activities.fundraisingStaff}
                onChange={(e) => setActivities((a) => ({ ...a, fundraisingStaff: e.target.value }))}
                placeholder="0"
                className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
              />
            </label>
          </div>

          <div className="flex flex-wrap gap-2 mt-4">
            {(
              [
                { key: "oppResearch" as const, label: "Opp. Research" },
                { key: "debatePrep" as const, label: "Debate Prep" },
                { key: "polling" as const, label: "Polling" },
              ]
            ).map(({ key, label }) => {
              const on = activities[key];
              return (
                <button
                  type="button"
                  key={key}
                  onClick={() => setActivities((a) => ({ ...a, [key]: !a[key] }))}
                  className={cn(
                    "rounded-full border px-3 py-1.5 text-xs font-medium transition-colors",
                    on
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-border bg-background text-muted-foreground hover:border-primary/30"
                  )}
                >
                  {label}
                </button>
              );
            })}
          </div>

          {/* Affordability preview */}
          {preview && (
            <div
              className={cn(
                "mt-4 rounded-lg px-3 py-2 text-xs",
                preview.affordable
                  ? "bg-secondary text-muted-foreground"
                  : "bg-destructive/10 text-destructive"
              )}
            >
              <p className="font-medium">
                {preview.affordable ? "Affordable" : "Cannot afford this plan"}
              </p>
              <p className="mt-0.5">
                Projected — Budget ${Math.round(preview.projectedBudget).toLocaleString()} · Time{" "}
                {preview.projectedTimeUnits} · Staff {preview.projectedStaff}
              </p>
              {preview.issues.length > 0 && (
                <ul className="mt-1 list-disc list-inside">
                  {preview.issues.map((iss, i) => (
                    <li key={i}>{iss}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          <div className="mt-4">
            <Button
              size="sm"
              className="text-xs"
              disabled={busy || (preview != null && !preview.affordable)}
              onClick={handleAdvance}
            >
              {busy ? "Advancing..." : "Advance Week"}
            </Button>
          </div>
        </div>
      )}

      {/* Week timeline */}
      {weeks.length > 0 && (
        <div className="rounded-xl border border-border bg-card p-5">
          <h2 className="text-sm font-bold text-card-foreground mb-3">Timeline</h2>
          <ol className="flex flex-col gap-2">
            {[...weeks]
              .sort((a, b) => a.weekNumber - b.weekNumber)
              .map((wk) => (
                <li
                  key={wk.weekNumber}
                  className="flex items-start gap-3 rounded-lg border border-border bg-background px-3 py-2"
                >
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-secondary text-[11px] font-bold text-muted-foreground">
                    {wk.weekNumber}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-2">
                      <p className="text-xs font-medium text-card-foreground">
                        {wk.summary || `Week ${wk.weekNumber}`}
                      </p>
                      <span className="text-xs font-semibold text-foreground shrink-0">
                        {Math.round(wk.approvalRating)}%
                      </span>
                    </div>
                    {wk.debateId && (
                      <Link
                        to={`/debates/${wk.debateId}`}
                        className="text-[11px] text-primary hover:underline"
                      >
                        View debate
                      </Link>
                    )}
                  </div>
                </li>
              ))}
          </ol>
        </div>
      )}
    </main>
  );
}
