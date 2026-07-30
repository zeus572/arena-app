import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Search } from "lucide-react";
import { listBills, type BillSummary } from "@/api/bills";
import { getMyProfile, type Profile } from "@/api/profile";
import { ValueChip } from "../components/ValueChip";
import FrontPageView from "./bills/FrontPageView";
import FloorView, { type FloorPreset } from "./bills/FloorView";
import FieldView from "./bills/FieldView";
import {
  AXES,
  buildBillVM,
  fmtDate,
  leanPole,
  SORT_OPTIONS,
  sortVms,
  STAGES,
  userVecFromProfile,
  type SortKey,
} from "./bills/model";

type ViewKey = "front" | "floor" | "field";

const TABS: { key: ViewKey; label: string; desc: string }[] = [
  { key: "front", label: "Front Page", desc: "What leads, what closes" },
  { key: "floor", label: "The Floor", desc: "Every bill by stage" },
  { key: "field", label: "Compass Field", desc: "Plotted on your axes" },
];

function parseView(v: string | null): ViewKey {
  return v === "floor" || v === "field" ? v : "front";
}

export default function MagazineBills() {
  const [bills, setBills] = useState<BillSummary[]>([]);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loaded, setLoaded] = useState(false);

  const [searchParams, setSearchParams] = useSearchParams();
  const view = parseView(searchParams.get("view"));

  const [query, setQuery] = useState("");
  const [stageFilter, setStageFilter] = useState<Set<number>>(new Set());
  const [axisFilter, setAxisFilter] = useState<Set<number>>(new Set());
  const [alignmentMin, setAlignmentMin] = useState(0);
  const [sort, setSort] = useState<SortKey>("recent");

  useEffect(() => {
    let alive = true;
    void Promise.all([
      listBills().catch(() => [] as BillSummary[]),
      getMyProfile().catch(() => null),
    ]).then(([b, p]) => {
      if (!alive) return;
      setBills(b);
      setProfile(p);
      setLoaded(true);
    });
    return () => {
      alive = false;
    };
  }, []);

  const userVec = useMemo(() => userVecFromProfile(profile), [profile]);
  const hasCompass = userVec != null;

  const allVms = useMemo(() => bills.map((b) => buildBillVM(b, userVec)), [bills, userVec]);

  const setView = (key: ViewKey) => {
    const next = new URLSearchParams(searchParams);
    if (key === "front") next.delete("view");
    else next.set("view", key);
    setSearchParams(next, { replace: true });
  };

  const toggleStage = (idx: number) =>
    setStageFilter((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) next.delete(idx);
      else next.add(idx);
      return next;
    });

  const toggleAxis = (i: number) =>
    setAxisFilter((prev) => {
      const next = new Set(prev);
      if (next.has(i)) next.delete(i);
      else next.add(i);
      return next;
    });

  const reset = () => {
    setAxisFilter(new Set());
    setAlignmentMin(0);
  };

  const onPreset = (p: FloorPreset) => {
    switch (p) {
      case "committee":
        setStageFilter(new Set([1]));
        setSort("recent");
        break;
      case "passed":
        setStageFilter(new Set([2, 3]));
        setSort("recent");
        break;
      case "enacted":
        setStageFilter(new Set([4]));
        setSort("recent");
        break;
      case "clash":
        setStageFilter(new Set());
        setSort("distance");
        break;
    }
  };

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = allVms.filter((vm) => {
      const b = vm.bill;
      if (q) {
        const hay = `${b.identifier} ${b.title} ${b.shortTitle ?? ""} ${b.sponsor} ${b.teaser}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      if (stageFilter.size > 0 && !stageFilter.has(vm.stage)) return false;
      if (axisFilter.size > 0 && ![...axisFilter].some((i) => vm.hasAxis[i])) return false;
      if (alignmentMin > 0 && hasCompass) {
        if (vm.alignPct == null || vm.alignPct < alignmentMin) return false;
      }
      return true;
    });
    return sortVms(list, sort);
  }, [allVms, query, stageFilter, axisFilter, alignmentMin, hasCompass, sort]);

  const sortLabel = SORT_OPTIONS.find((s) => s.key === sort)?.label ?? "Recent action";

  const updated = useMemo(() => {
    const dates = bills.map((b) => b.latestActionDate).filter((d): d is string => Boolean(d));
    if (dates.length === 0) return null;
    return dates.sort().at(-1) ?? null;
  }, [bills]);

  // Header stat trio — all real, derived from status (the design's "floor votes"
  // / "act within 7d" needed forward-looking scheduling data that doesn't exist).
  const stats = useMemo(() => {
    const inCommittee = allVms.filter((v) => v.stage === 1).length;
    const enacted = allVms.filter((v) => v.stage === 4).length;
    return { total: allVms.length, inCommittee, enacted };
  }, [allVms]);

  return (
    <article data-testid="magazine-bills">
      {/* Page header */}
      <header className="flex flex-col items-start gap-8 pb-[22px] pt-2 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex max-w-[760px] flex-col gap-2.5">
          <span className="text-[11px] font-bold uppercase tracking-[0.18em] text-[var(--accent)]">
            Explore · Bills before Congress
          </span>
          <h1 className="display text-[40px] leading-[1.02] tracking-[-0.028em] text-[var(--fg)] [text-wrap:pretty] md:text-[52px]">
            Every bill in the 119th, read against your compass
          </h1>
          <p className="max-w-[64ch] text-[17px] leading-[1.55] text-[var(--fg-soft)] [text-wrap:pretty]">
            Real legislation, plotted on the six value axes you scored. Three ways to look at the same corpus — pick
            whichever way you think.
          </p>
        </div>
        <div className="flex flex-none gap-[34px] pb-1.5">
          <Stat value={stats.total} label="Bills tracked" />
          <Stat value={stats.inCommittee} label="In committee" accent />
          <Stat value={stats.enacted} label="Enacted" />
        </div>
      </header>

      {/* View switcher */}
      <div className="flex items-stretch overflow-x-auto border-b-2 border-[var(--fg)]">
        {TABS.map((t) => {
          const on = view === t.key;
          return (
            <button
              key={t.key}
              type="button"
              onClick={() => setView(t.key)}
              className="-mb-0.5 flex flex-none flex-col gap-0.5 border-b-4 px-6 pb-3 pt-3.5 text-left"
              style={{
                borderColor: on ? "var(--accent)" : "transparent",
                background: on ? "var(--bg-elev)" : "transparent",
              }}
              data-testid={`bills-tab-${t.key}`}
            >
              <span className="display text-xl leading-tight" style={{ color: on ? "var(--fg)" : "var(--muted)" }}>
                {t.label}
              </span>
              <span className="text-xs" style={{ color: on ? "var(--fg-soft)" : "var(--muted)" }}>
                {t.desc}
              </span>
            </button>
          );
        })}
        <div className="ml-auto hidden items-center gap-4 px-1 pb-2 text-xs text-[var(--muted)] md:flex">
          <span className="inline-flex items-center gap-1.5">
            <span className="h-[7px] w-[7px] animate-pulse rounded-full bg-[var(--accent)]" />
            {updated ? `Updated ${fmtDate(updated)} from congress.gov` : "from congress.gov"}
          </span>
        </div>
      </div>

      {/* Filter bar */}
      <div className="flex flex-wrap items-center gap-3 pb-3.5 pt-5">
        <div className="flex min-w-[240px] flex-1 items-center gap-2.5 border border-[var(--fg)] bg-[var(--bg-elev)] px-3.5 py-[11px]">
          <Search className="h-[15px] w-[15px] flex-none text-[var(--muted)]" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search bills, sponsors, or a phrase in the summary…"
            className="min-w-0 flex-1 bg-transparent text-[15px] text-[var(--fg)] outline-none placeholder:text-[var(--muted)]"
            data-testid="bills-search"
          />
        </div>

        {/* The six stage segments are ~910px of nowrap text — wider than any phone and
            wider than the filter row at most desktop widths. `flex-none` sized this box to
            that content and refused to shrink, so its own `overflow-x-auto` never engaged
            and the whole PAGE scrolled sideways instead. `basis-full` + `min-w-0` gives it
            its own line bounded by the container, which is what makes the internal swipe
            work. */}
        <div className="min-w-0 basis-full overflow-x-auto" data-testid="bills-stage-filter">
          {/* The border lives on the inner `w-fit` row, not the scroller, so it hugs the
              segments instead of trailing an empty bordered strip across a wide screen. */}
          <div className="flex w-fit border border-[var(--border)] bg-[var(--bg-elev)]">
            <StageSegment label="All" active={stageFilter.size === 0} onClick={() => setStageFilter(new Set())} />
            {STAGES.map((s, i) => (
              <StageSegment key={s.label} label={s.label} active={stageFilter.has(i)} onClick={() => toggleStage(i)} />
            ))}
          </div>
        </div>

        {/* Same failure in miniature: a select is as wide as its longest option, and
            "Best aligned (needs compass)" pushed this a few px past the viewport. */}
        <label className="flex min-w-0 max-w-full flex-none items-center gap-2 border border-[var(--border)] bg-[var(--bg-elev)] px-3.5 py-[11px] text-xs font-bold uppercase tracking-[0.08em] text-[var(--fg)]">
          <span className="flex-none text-[var(--muted)]">Sort</span>
          <select
            value={sort}
            onChange={(e) => setSort(e.target.value as SortKey)}
            className="min-w-0 flex-1 bg-transparent font-bold uppercase tracking-[0.08em] text-[var(--fg)] outline-none"
            data-testid="bills-sort"
          >
            {SORT_OPTIONS.map((o) => (
              <option key={o.key} value={o.key} disabled={o.needsCompass && !hasCompass}>
                {o.label}
                {o.needsCompass && !hasCompass ? " (needs compass)" : ""}
              </option>
            ))}
          </select>
        </label>
      </div>

      {/* Axis chips row */}
      <div className="flex flex-wrap items-center gap-2.5 pb-6">
        {hasCompass && userVec ? (
          <>
            <span className="mr-1 text-[11px] font-bold uppercase tracking-[0.14em] text-[var(--muted)]">
              Your axes
            </span>
            {AXES.map((ax, i) => (
              <ValueChip
                key={ax.key}
                label={leanPole(i, userVec[i])}
                selected={axisFilter.has(i)}
                onClick={() => toggleAxis(i)}
              />
            ))}
            <span className="ml-auto flex items-center gap-2 text-xs text-[var(--muted)]">
              <label className="flex items-center gap-1.5">
                Alignment ≥
                <select
                  value={alignmentMin}
                  onChange={(e) => setAlignmentMin(Number(e.target.value))}
                  className="bg-transparent font-semibold text-[var(--fg)] outline-none"
                  data-testid="bills-alignment-min"
                >
                  {[0, 40, 50, 60, 70, 80].map((n) => (
                    <option key={n} value={n}>
                      {n}%
                    </option>
                  ))}
                </select>
              </label>
              <button type="button" onClick={reset} className="text-[var(--accent)] hover:underline">
                Reset
              </button>
            </span>
          </>
        ) : (
          <div className="flex w-full flex-wrap items-center justify-between gap-3">
            <span className="text-[13px] text-[var(--fg-soft)]">
              Score your compass and every bill here re-ranks by how it sits against your own values.
            </span>
            <Link
              to="/quizzes"
              className="flex-none text-xs font-bold uppercase tracking-[0.1em] text-[var(--accent)] hover:underline"
              data-testid="bills-take-quiz"
            >
              Take the values quiz →
            </Link>
          </div>
        )}
      </div>

      {/* Body */}
      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="bills-loading">
          Loading bills…
        </p>
      ) : bills.length === 0 ? (
        <p className="py-12 text-base text-[var(--muted)]" data-testid="bills-empty">
          No bills have been analyzed yet. Check back once synthesis has run.
        </p>
      ) : view === "front" ? (
        <FrontPageView vms={filtered} userVec={userVec} hasCompass={hasCompass} sortLabel={sortLabel} />
      ) : view === "floor" ? (
        <FloorView
          vms={filtered}
          userVec={userVec}
          hasCompass={hasCompass}
          axisFilter={axisFilter}
          onToggleAxis={toggleAxis}
          onPreset={onPreset}
        />
      ) : (
        <FieldView vms={filtered} userVec={userVec} hasCompass={hasCompass} />
      )}
    </article>
  );
}

function Stat({ value, label, accent = false }: { value: number; label: string; accent?: boolean }) {
  return (
    <div className="flex flex-col items-end gap-0.5">
      <span className="display text-[30px]" style={{ color: accent ? "var(--accent)" : "var(--fg)" }}>
        {value}
      </span>
      <span className="text-[10px] font-bold uppercase tracking-[0.14em] text-[var(--muted)]">{label}</span>
    </div>
  );
}

function StageSegment({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      // These are toggles whose selected state was conveyed by background colour alone,
      // which says nothing to a screen reader (and gives a test nothing to assert on).
      aria-pressed={active}
      data-testid={`bills-stage-${label.toLowerCase().replace(/\s+/g, "-")}`}
      className="whitespace-nowrap border-r border-[var(--border)] px-[15px] py-[11px] text-xs font-bold uppercase tracking-[0.08em] last:border-r-0"
      style={{
        background: active ? "var(--accent)" : "transparent",
        color: active ? "#fff" : "var(--fg-soft)",
      }}
    >
      {label}
    </button>
  );
}
