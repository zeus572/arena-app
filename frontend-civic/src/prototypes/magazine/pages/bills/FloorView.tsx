import { Link } from "react-router-dom";
import { Button } from "../../components/Button";
import {
  alignmentColor,
  AXES,
  fmtDateShort,
  radarPoints,
  stageColor,
  stageLabel,
  STAGES,
  type BillVM,
} from "./model";

export type FloorPreset = "committee" | "passed" | "enacted" | "clash";

type Props = {
  vms: BillVM[];
  userVec: number[] | null;
  hasCompass: boolean;
  axisFilter: Set<number>;
  onToggleAxis: (i: number) => void;
  onPreset: (p: FloorPreset) => void;
};

function alignText(pct: number | null): string {
  return pct == null ? "—" : `${pct}%`;
}

export default function FloorView({ vms, userVec, hasCompass, axisFilter, onToggleAxis, onPreset }: Props) {
  const mineRadar = userVec ? radarPoints(userVec) : null;
  const perStage = STAGES.map((_, i) => vms.filter((v) => v.stage === i));
  const maxCount = Math.max(1, ...perStage.map((l) => l.length));
  const recent = [...vms]
    .sort((a, b) => actionMs(b) - actionMs(a))
    .slice(0, 7);

  const savedViews: { key: FloorPreset; label: string; n: number; accent?: boolean }[] = [
    { key: "committee", label: "In committee", n: perStage[1].length },
    { key: "passed", label: "Passed a chamber", n: perStage[2].length + perStage[3].length },
    { key: "enacted", label: "Enacted this Congress", n: perStage[4].length, accent: true },
    { key: "clash", label: "Furthest from your compass", n: hasCompass ? vms.filter((v) => (v.alignPct ?? 100) < 50).length : 0 },
  ];

  return (
    <div className="flex flex-col border border-[var(--border)] bg-[var(--bg)]" data-testid="bills-view-floor">
      {/* B1. Distribution strip (real per-stage counts — no stage-change history exists). */}
      <div className="flex flex-wrap items-center gap-5 border-b border-[var(--fg)] bg-[var(--bg-elev)] px-[26px] py-[18px]">
        <span className="flex-none text-[11px] font-bold uppercase tracking-[0.14em] text-[var(--muted)]">
          Where the corpus sits · by stage
        </span>
        <div className="mx-3 flex flex-1 items-end gap-1.5" style={{ minWidth: 200 }}>
          {perStage.map((list, i) => (
            <div key={i} className="flex flex-1 flex-col items-center gap-1.5">
              <span
                className="w-full"
                style={{ height: `${8 + (list.length / maxCount) * 34}px`, background: STAGES[i].color }}
              />
              <span className="text-[9px] font-bold uppercase tracking-[0.08em] text-[var(--muted)]">
                {list.length}
              </span>
            </div>
          ))}
        </div>
        <span className="flex-none text-xs text-[var(--muted)]">{vms.length} bills tracked</span>
      </div>

      {/* B2. Body */}
      <div className="grid grid-cols-1 lg:grid-cols-[236px_1fr_306px] lg:items-stretch">
        {/* Left rail */}
        <div className="flex flex-col gap-[22px] border-b border-[var(--border)] bg-[var(--bg-elev)] px-[22px] py-6 lg:border-b-0 lg:border-r">
          <div className="flex flex-col gap-3">
            <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]">Your compass</span>
            {mineRadar ? (
              <svg viewBox="0 0 100 100" className="h-[158px] w-full" aria-hidden>
                <polygon points="50,4 90,27 90,73 50,96 10,73 10,27" fill="none" stroke="var(--border)" />
                <polygon points="50,27 70,38.5 70,61.5 50,73 30,61.5 30,38.5" fill="none" stroke="var(--border)" strokeDasharray="2 3" />
                <polygon
                  points={mineRadar}
                  fill="color-mix(in oklab, var(--federal) 16%, transparent)"
                  stroke="var(--federal)"
                  strokeWidth={2.5}
                />
              </svg>
            ) : (
              <Link
                to="/quizzes"
                className="border border-dashed border-[var(--border)] p-4 text-center text-[13px] leading-relaxed text-[var(--muted)] no-underline hover:border-[var(--accent)]"
              >
                Take the values quiz to plot your compass here.
              </Link>
            )}
          </div>

          <div className="flex flex-col gap-[13px]">
            <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]">Filter by axis</span>
            {AXES.map((ax, i) => {
              const v = userVec?.[i] ?? 0;
              const pos = ((v + 1) / 2) * 100;
              const strong = Math.abs(v) >= 0.4;
              const on = axisFilter.has(i);
              return (
                <button
                  key={ax.key}
                  type="button"
                  onClick={() => onToggleAxis(i)}
                  className="flex flex-col gap-1.5 text-left"
                  data-testid={`floor-axis-${ax.key}`}
                >
                  <div className="flex justify-between text-[11px] font-bold text-[var(--fg)]">
                    <span style={{ color: on ? "var(--accent)" : undefined }}>{ax.name}</span>
                    <span
                      className="text-[10px] uppercase tracking-[0.1em]"
                      style={{ color: strong ? "var(--accent)" : "var(--muted)" }}
                    >
                      {userVec ? (strong ? "Strong" : "Mixed") : "—"}
                    </span>
                  </div>
                  <div className="relative h-1 bg-[var(--border)]">
                    {userVec && (
                      <span
                        className="absolute -top-[3px] h-2.5 w-2.5 rounded-full bg-[var(--accent)]"
                        style={{ left: `calc(${pos}% - 5px)` }}
                      />
                    )}
                  </div>
                </button>
              );
            })}
          </div>

          <div className="flex flex-col gap-[9px] border-t border-[var(--border)] pt-[18px]">
            <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]">Saved views</span>
            {savedViews.map((v) => (
              <button
                key={v.key}
                type="button"
                onClick={() => onPreset(v.key)}
                className="flex justify-between text-[13px] hover:opacity-70"
                style={{ color: v.accent ? "var(--accent)" : "var(--fg)" }}
              >
                <span>{v.label}</span>
                <span className="text-[var(--muted)]">{v.n}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Board */}
        <div className="grid grid-cols-2 gap-px border-b border-[var(--border)] bg-[var(--border)] md:grid-cols-3 lg:grid-cols-5 lg:border-b-0 lg:border-r">
          {STAGES.map((s, i) => {
            const list = perStage[i];
            const shown = list.slice(0, 4);
            return (
              <div key={s.label} className="flex flex-col bg-[var(--bg)]">
                <div className="h-1" style={{ background: s.color }} />
                <div className="flex items-baseline justify-between border-b border-[var(--border)] px-3.5 pb-[11px] pt-[15px]">
                  <span className="text-[10px] font-bold uppercase tracking-[0.11em] text-[var(--fg)]">{s.label}</span>
                  <span className="display text-[19px]" style={{ color: s.color }}>
                    {list.length}
                  </span>
                </div>
                <div className="flex flex-col gap-2.5 p-3">
                  {shown.map((vm) => (
                    <BoardCard key={vm.bill.id} vm={vm} />
                  ))}
                  {list.length > shown.length && (
                    <span className="px-0.5 py-1 text-[11px] font-bold uppercase tracking-[0.08em] text-[var(--accent)]">
                      + {list.length - shown.length} more
                    </span>
                  )}
                  {list.length === 0 && (
                    <span className="px-0.5 py-1 text-[11px] text-[var(--muted)]">None</span>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* Activity feed */}
        <div className="flex flex-col gap-4 bg-[var(--bg-elev)] px-5 py-[22px]">
          <div className="flex items-center gap-1.5 border-b-2 border-[var(--fg)] pb-2">
            <span className="h-[7px] w-[7px] animate-pulse rounded-full bg-[var(--accent)]" />
            <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--fg)]">Latest activity</span>
          </div>
          {recent.map((vm) => (
            <Link
              key={vm.bill.id}
              to={`/bills/${vm.bill.id}`}
              className="flex gap-[11px] border-b border-[var(--border)] pb-[13px] no-underline"
            >
              <span className="w-[42px] flex-none pt-0.5 text-[10px] font-bold tracking-[0.06em] text-[var(--muted)]">
                {fmtDateShort(vm.bill.latestActionDate)}
              </span>
              <div className="flex flex-col gap-1">
                <span
                  className="text-[10px] font-bold uppercase tracking-[0.1em]"
                  style={{ color: stageColor(vm.stage) }}
                >
                  {vm.bill.identifier.split(" · ")[0]} · {stageLabel(vm.stage)}
                </span>
                <span className="text-[13px] leading-[1.4] text-[var(--fg-soft)] [text-wrap:pretty] line-clamp-2">
                  {vm.bill.teaser}
                </span>
              </div>
            </Link>
          ))}
          <div className="flex flex-col gap-2 border border-[var(--fg)] p-[15px]">
            <span className="display text-[17px] leading-[1.15] text-[var(--fg)]">
              {hasCompass ? "Re-rank the whole board by your compass" : "Score your compass to rank every bill"}
            </span>
            <Link to={hasCompass ? "/settings" : "/quizzes"}>
              <Button variant="primary" size="sm">
                {hasCompass ? "Refine my compass" : "Take the quiz"}
              </Button>
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

function BoardCard({ vm }: { vm: BillVM }) {
  const b = vm.bill;
  return (
    <Link
      to={`/bills/${b.id}`}
      className="flex flex-col gap-2 border border-[var(--border)] bg-[var(--bg-elev)] p-3 no-underline transition hover:border-[var(--fg)]"
      data-testid={`board-${b.externalId}`}
    >
      <div className="flex items-center gap-[7px]">
        <span className="h-[9px] w-[9px] flex-none rounded-full" style={{ background: alignmentColor(vm.alignPct) }} />
        <span className="text-[10px] font-bold uppercase tracking-[0.1em] text-[var(--muted)]">
          {b.identifier.split(" · ")[0]}
        </span>
        <span className="ml-auto text-[10px] font-bold text-[var(--fg)]">{alignText(vm.alignPct)}</span>
      </div>
      <span className="display text-[15px] leading-[1.16] text-[var(--fg)] [text-wrap:pretty] line-clamp-2">
        {b.shortTitle || b.title}
      </span>
      <div className="relative h-[3px] bg-[var(--border)]">
        <span
          className="absolute inset-y-0 left-0"
          style={{ width: `${(vm.positioned / AXES.length) * 100}%`, background: stageColor(vm.stage) }}
        />
      </div>
      <div className="flex flex-col gap-0.5 text-[10px] text-[var(--muted)]">
        <span className="whitespace-nowrap">
          {vm.positioned} of {AXES.length} values
        </span>
        <MiniLabel vm={vm} />
      </div>
    </Link>
  );
}

function MiniLabel({ vm }: { vm: BillVM }) {
  const d = fmtDateShort(vm.bill.latestActionDate);
  return <span className="whitespace-nowrap">{d ? `acted ${d}` : "no action yet"}</span>;
}

function actionMs(vm: BillVM): number {
  const t = new Date(vm.bill.latestActionDate ?? vm.bill.introducedDate).getTime();
  return Number.isNaN(t) ? 0 : t;
}
