import { useEffect, useMemo, useState } from "react";
import { ButtonLink, Button } from "../../components/Button";
import { getBill, type BillDetail } from "@/api/bills";
import { MiniCompass } from "./svg";
import {
  AXES,
  radarPoints,
  stageColor,
  stageLabel,
  STAGES,
  type BillVM,
} from "./model";

type Props = {
  vms: BillVM[];
  userVec: number[] | null;
  hasCompass: boolean;
};

const unit = (score: number) => (score + 1) / 2;

function alignText(pct: number | null): string {
  return pct == null ? "—" : `${pct}%`;
}

export default function FieldView({ vms, userVec, hasCompass }: Props) {
  const [xAxis, setXAxis] = useState(0); // Government role
  const [yAxis, setYAxis] = useState(5); // Time horizon
  const [selectedId, setSelectedId] = useState<string | null>(vms[0]?.bill.id ?? null);

  // Keep a valid selection as the corpus changes under filtering.
  useEffect(() => {
    if (!selectedId || !vms.some((v) => v.bill.id === selectedId)) {
      setSelectedId(vms[0]?.bill.id ?? null);
    }
  }, [vms, selectedId]);

  const selected = vms.find((v) => v.bill.id === selectedId) ?? null;
  const mineRadar = userVec ? radarPoints(userVec) : null;

  const dots = vms.map((vm) => ({
    vm,
    x: unit(vm.axisScores[xAxis]) * 100,
    y: (1 - unit(vm.axisScores[yAxis])) * 100,
    size: 12 + Math.min(20, vm.positioned * 3),
  }));

  const me = userVec ? { x: unit(userVec[xAxis]) * 100, y: (1 - unit(userVec[yAxis])) * 100 } : null;

  // Histogram of the corpus on the X axis (14 bins across -1..+1).
  const hist = useMemo(() => {
    const bins = new Array(14).fill(0);
    for (const vm of vms) {
      const b = Math.min(13, Math.max(0, Math.floor(unit(vm.axisScores[xAxis]) * 14)));
      bins[b] += 1;
    }
    const max = Math.max(1, ...bins);
    return bins.map((n) => ({ h: Math.round((n / max) * 100), hot: n >= max * 0.8 }));
  }, [vms, xAxis]);

  const nearest = useMemo(() => {
    if (!selected) return [];
    return vms
      .filter((v) => v.bill.id !== selected.bill.id)
      .map((v) => ({
        vm: v,
        d:
          (unit(v.axisScores[xAxis]) - unit(selected.axisScores[xAxis])) ** 2 +
          (unit(v.axisScores[yAxis]) - unit(selected.axisScores[yAxis])) ** 2,
      }))
      .sort((a, b) => a.d - b.d)
      .slice(0, 3)
      .map((x) => x.vm);
  }, [vms, selected, xAxis, yAxis]);

  const surprise = () => {
    if (vms.length === 0) return;
    // Weight toward the least-mapped, furthest bills — genuine discovery.
    const ranked = [...vms].sort(
      (a, b) => (b.distance ?? 0) - (a.distance ?? 0) + (b.positioned < a.positioned ? -0.01 : 0.01),
    );
    const pick = ranked[Math.min(ranked.length - 1, 2 + (vms.length % 3))] ?? ranked[0];
    setSelectedId(pick.bill.id);
  };

  return (
    <div className="flex flex-col border border-[var(--border)] bg-[var(--bg)]" data-testid="bills-view-field">
      {/* C1. Axis bar */}
      <div className="flex flex-wrap items-center gap-[18px] border-b border-[var(--fg)] bg-[var(--bg-elev)] px-[26px] py-[18px]">
        <div className="flex flex-wrap items-center gap-2.5">
          <AxisPicker label="X" value={xAxis} onChange={setXAxis} />
          <AxisPicker label="Y" value={yAxis} onChange={setYAxis} />
        </div>
        <div className="flex flex-wrap items-center gap-4 md:ml-auto">
          {STAGES.map((s) => (
            <span
              key={s.label}
              className="inline-flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-[0.1em] text-[var(--muted)]"
            >
              <span className="h-[9px] w-[9px] rounded-full" style={{ background: s.color }} />
              {s.label}
            </span>
          ))}
        </div>
      </div>

      {/* C2. Body */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_330px] lg:items-stretch">
        {/* Plot */}
        <div className="flex flex-col gap-3 border-b border-[var(--border)] px-[30px] py-[26px] lg:border-b-0 lg:border-r">
          <EdgeCaption>{AXES[yAxis].high}</EdgeCaption>
          <div className="flex items-stretch gap-3">
            <SideCaption side="left">{AXES[xAxis].low}</SideCaption>
            <div className="relative h-[440px] flex-1 overflow-hidden border border-[var(--fg)] bg-[var(--bg-elev)] md:h-[588px]">
              {/* grid + crosshair */}
              <div
                className="absolute inset-0 opacity-55"
                style={{
                  backgroundImage:
                    "linear-gradient(var(--border) 1px, transparent 1px), linear-gradient(90deg, var(--border) 1px, transparent 1px)",
                  backgroundSize: "12.5% 12.5%",
                }}
              />
              <div className="absolute inset-x-0 top-1/2 border-t border-dashed border-[var(--muted)] opacity-70" />
              <div className="absolute inset-y-0 left-1/2 border-l border-dashed border-[var(--muted)] opacity-70" />

              {/* user neighborhood */}
              {me && (
                <>
                  <div
                    className="absolute h-[180px] w-[180px] -translate-x-1/2 -translate-y-1/2 rounded-full border border-[var(--accent)] opacity-35"
                    style={{ left: `${me.x}%`, top: `${me.y}%` }}
                  />
                  <div
                    className="absolute -translate-x-1/2 -translate-y-1/2"
                    style={{ left: `${me.x}%`, top: `${me.y}%` }}
                  >
                    <span className="block h-[15px] w-[15px] rounded-full border-[3px] border-[var(--bg-elev)] bg-[var(--accent)] shadow-[0_0_0_1px_var(--accent)]" />
                  </div>
                  <div
                    className="absolute text-[10px] font-bold uppercase tracking-[0.14em] text-[var(--accent)]"
                    style={{ left: `calc(${me.x}% + 12px)`, top: `calc(${me.y}% - 22px)` }}
                  >
                    You
                  </div>
                </>
              )}

              {/* dots */}
              {dots.map(({ vm, x, y, size }) => {
                const isSel = vm.bill.id === selectedId;
                return (
                  <button
                    key={vm.bill.id}
                    type="button"
                    onClick={() => setSelectedId(vm.bill.id)}
                    aria-label={vm.bill.shortTitle || vm.bill.title}
                    className="absolute -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-[var(--bg-elev)] transition-transform hover:scale-110 motion-reduce:transition-none"
                    style={{
                      left: `${x}%`,
                      top: `${y}%`,
                      width: size,
                      height: size,
                      background: stageColor(vm.stage),
                      zIndex: isSel ? 3 : 1,
                    }}
                  />
                );
              })}

              {/* selection ring + tooltip */}
              {selected && (
                <>
                  <div
                    className="pointer-events-none absolute h-[46px] w-[46px] -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-[var(--fg)]"
                    style={{ left: `${unit(selected.axisScores[xAxis]) * 100}%`, top: `${(1 - unit(selected.axisScores[yAxis])) * 100}%` }}
                  />
                  <div
                    className="pointer-events-none absolute z-[4] flex max-w-[230px] flex-col gap-1 bg-[var(--fg)] px-[11px] py-2 text-[var(--bg)]"
                    style={{
                      left: `calc(${unit(selected.axisScores[xAxis]) * 100}% + 16px)`,
                      top: `calc(${(1 - unit(selected.axisScores[yAxis])) * 100}% + 8px)`,
                    }}
                  >
                    <span className="text-[9px] font-bold uppercase tracking-[0.14em] opacity-70">
                      {selected.bill.identifier.split(" · ")[0]} · {stageLabel(selected.stage)}
                    </span>
                    <span className="display text-[15px] leading-[1.15]">
                      {selected.bill.shortTitle || selected.bill.title}
                    </span>
                  </div>
                </>
              )}
            </div>
            <SideCaption side="right">{AXES[xAxis].high}</SideCaption>
          </div>
          <EdgeCaption>{AXES[yAxis].low}</EdgeCaption>

          {/* Histogram footer */}
          <div className="mt-1 flex flex-col items-stretch gap-5 border-t border-[var(--border)] pt-4 md:flex-row md:items-end">
            <div className="flex flex-1 flex-col gap-1.5">
              <span className="text-[10px] font-bold uppercase tracking-[0.14em] text-[var(--muted)]">
                Where the {vms.length} bills sit on {AXES[xAxis].name}
              </span>
              <div className="flex h-11 items-end gap-0.5">
                {hist.map((h, i) => (
                  <span
                    key={i}
                    className="flex-1"
                    style={{ height: `${Math.max(4, h.h)}%`, background: h.hot ? "var(--accent)" : "var(--fg-soft)" }}
                  />
                ))}
              </div>
            </div>
            <div className="flex items-center gap-3">
              <Button variant="secondary" onClick={surprise}>
                Surprise me
              </Button>
              {selected && (
                <ButtonLink to={`/bills/${selected.bill.id}`} variant="primary">
                  Open the bill
                </ButtonLink>
              )}
            </div>
          </div>
        </div>

        {/* Detail panel */}
        <DetailPanel selected={selected} hasCompass={hasCompass} mineRadar={mineRadar} nearest={nearest} onSelect={setSelectedId} />
      </div>
    </div>
  );
}

/* --------------------------------------------------------------- detail panel */

function DetailPanel({
  selected,
  hasCompass,
  mineRadar,
  nearest,
  onSelect,
}: {
  selected: BillVM | null;
  hasCompass: boolean;
  mineRadar: string | null;
  nearest: BillVM[];
  onSelect: (id: string) => void;
}) {
  const [detail, setDetail] = useState<BillDetail | null>(null);

  useEffect(() => {
    setDetail(null);
    if (!selected) return;
    let alive = true;
    void getBill(selected.bill.id).then((d) => {
      if (alive) setDetail(d);
    });
    return () => {
      alive = false;
    };
  }, [selected]);

  if (!selected) {
    return (
      <div className="flex items-center justify-center bg-[var(--bg-elev)] p-6 text-sm text-[var(--muted)]">
        Select a bill in the field to inspect it.
      </div>
    );
  }

  const b = selected.bill;
  // The axis this bill pulls hardest against the user on (highest-confidence tension).
  const clashIdx = selected.axisAlignment.findIndex((a) => a === "tension");

  // "What's in it": real per-axis rationale from the lazily-fetched detail.
  const planks = (detail?.axes ?? [])
    .filter((a) => a.rationale)
    .slice(0, 3)
    .map((a) => ({
      text: a.rationale,
      tag: `${a.axisName} · leans ${a.billScore >= 0 ? a.highLabel : a.lowLabel}`,
      color:
        a.alignment === "tension" ? "var(--state)" : a.alignment === "aligned" ? "var(--accent)" : "var(--federal)",
    }));

  return (
    <div className="flex flex-col gap-4 bg-[var(--bg-elev)] px-[22px] py-6" data-testid="field-detail">
      <span className="text-[10px] font-bold uppercase tracking-[0.16em]" style={{ color: stageColor(selected.stage) }}>
        {b.identifier.split(" · ")[0]} · {stageLabel(selected.stage)} · {selected.positioned} of {AXES.length} values
      </span>
      <span className="display text-[25px] leading-[1.1] text-[var(--fg)] [text-wrap:pretty]">
        {b.shortTitle || b.title}
      </span>
      <p className="text-sm leading-[1.5] text-[var(--fg-soft)] [text-wrap:pretty]">{b.teaser}</p>

      {/* Comparison band */}
      <div className="flex items-center gap-3.5 border-y border-[var(--border)] py-3.5">
        <MiniCompass radar={selected.radar} userRadar={mineRadar} size={74} billStroke={2.5} />
        <div className="flex flex-col gap-0.5">
          <span className="display text-[30px] leading-none text-[var(--fg)]">{alignText(selected.alignPct)}</span>
          <span className="text-[11px] font-bold uppercase tracking-[0.1em] text-[var(--muted)]">Aligned with you</span>
          {hasCompass && clashIdx >= 0 && (
            <span className="mt-1 text-xs text-[var(--fg-soft)]">
              Clashes on <strong className="text-[var(--accent)]">{AXES[clashIdx].name}</strong>
            </span>
          )}
          {!hasCompass && <span className="mt-1 text-xs text-[var(--muted)]">Score your compass to compare</span>}
        </div>
      </div>

      {/* What's in it */}
      <div className="flex flex-col gap-[9px]">
        <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]">What's in it</span>
        {planks.length > 0 ? (
          planks.map((p, i) => (
            <div key={i} className="flex flex-col gap-0.5 border-l-2 pl-3" style={{ borderColor: p.color }}>
              <span className="text-sm leading-[1.4] text-[var(--fg)] [text-wrap:pretty]">{p.text}</span>
              <span className="text-[11px] font-bold uppercase tracking-[0.08em]" style={{ color: p.color }}>
                {p.tag}
              </span>
            </div>
          ))
        ) : (
          <span className="text-[13px] text-[var(--muted)]">
            {detail ? "No per-axis rationale recorded for this bill." : "Loading the breakdown…"}
          </span>
        )}
      </div>

      {/* Nearest */}
      {nearest.length > 0 && (
        <div className="flex flex-col gap-[9px] border-t border-[var(--border)] pt-3.5">
          <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]">
            Nearest in the field
          </span>
          {nearest.map((vm) => (
            <button
              key={vm.bill.id}
              type="button"
              onClick={() => onSelect(vm.bill.id)}
              className="flex items-baseline gap-2.5 text-left"
            >
              <span className="h-2 w-2 flex-none rounded-full" style={{ background: stageColor(vm.stage) }} />
              <span className="text-[13px] leading-[1.3] text-[var(--fg-soft)] [text-wrap:pretty] line-clamp-1">
                {vm.bill.shortTitle || vm.bill.title}
              </span>
              <span className="ml-auto flex-none text-[11px] font-bold text-[var(--muted)]">{alignText(vm.alignPct)}</span>
            </button>
          ))}
        </div>
      )}

      <div className="mt-auto flex flex-col gap-[9px] pt-2">
        <ButtonLink to={`/bills/${b.id}`} variant="primary" fullWidth>
          Open the full bill
        </ButtonLink>
        <span className="text-center text-xs text-[var(--muted)]">
          {b.sponsor}
          {b.party ? ` [${b.party}]` : ""}
        </span>
      </div>
    </div>
  );
}

/* --------------------------------------------------------------- small bits */

function AxisPicker({ label, value, onChange }: { label: string; value: number; onChange: (i: number) => void }) {
  return (
    <label className="flex items-center gap-2">
      <span className="text-[10px] font-bold uppercase tracking-[0.14em] text-[var(--muted)]">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="border border-[var(--fg)] bg-[var(--bg-elev)] px-3 py-2 text-[13px] font-semibold text-[var(--fg)]"
        data-testid={`field-axis-${label.toLowerCase()}`}
      >
        {AXES.map((a, i) => (
          <option key={a.key} value={i}>
            {a.name}
          </option>
        ))}
      </select>
    </label>
  );
}

function EdgeCaption({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-center text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]">
      {children}
    </div>
  );
}

function SideCaption({ side, children }: { side: "left" | "right"; children: React.ReactNode }) {
  return (
    <div
      className="flex items-center justify-center text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--muted)]"
      style={{ writingMode: "vertical-rl", transform: side === "left" ? "rotate(180deg)" : undefined }}
    >
      {children}
    </div>
  );
}
