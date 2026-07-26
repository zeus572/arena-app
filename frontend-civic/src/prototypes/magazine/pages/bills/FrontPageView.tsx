import { Link } from "react-router-dom";
import { ButtonLink } from "../../components/Button";
import { AlignmentRing, MiniCompass } from "./svg";
import {
  AXES,
  fmtDate,
  fmtDateShort,
  leanPole,
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
  sortLabel: string;
};

function alignText(pct: number | null): string {
  return pct == null ? "—" : `${pct}%`;
}

export default function FrontPageView({ vms, userVec, hasCompass, sortLabel }: Props) {
  if (vms.length === 0) return <EmptyCorpus />;

  const mineRadar = userVec ? radarPoints(userVec) : null;
  const hero = vms[0];
  const grid = vms.slice(0, 8);
  const recent = [...vms]
    .sort((a, b) => actionMs(b) - actionMs(a))
    .slice(0, 2);

  // "Off your radar": genuine outliers — furthest from the user, or (no compass)
  // the least-mapped bills. Reason is computed, never editorial.
  const serendipity = pickSerendipity(vms, hasCompass);

  return (
    <div className="flex flex-col gap-[22px]" data-testid="bills-view-front">
      {/* A1. Ticker — real recent legislative actions, fade-masked overflow. */}
      <Ticker vms={vms} />

      {/* A2. Hero + rail */}
      <div className="grid grid-cols-1 gap-[26px] lg:grid-cols-[1fr_372px] lg:items-stretch">
        <HeroCard hero={hero} mineRadar={mineRadar} sortLabel={sortLabel} />

        <div className="flex flex-col gap-[14px]">
          <div className="border-b-2 border-[var(--fg)] pb-[7px] text-[11px] font-bold uppercase tracking-[0.16em] text-[var(--fg)]">
            Latest to move
          </div>
          {recent.map((vm) => (
            <RecentCard key={vm.bill.id} vm={vm} />
          ))}
          <CompassStatus hasCompass={hasCompass} userVec={userVec} corpus={vms.length} />
        </div>
      </div>

      {/* A3. Bill grid */}
      <div className="mt-1.5 flex items-baseline justify-between border-b-2 border-[var(--fg)] pb-[7px]">
        <span className="text-[11px] font-bold uppercase tracking-[0.16em] text-[var(--fg)]">
          All bills · sorted by {sortLabel.toLowerCase()}
        </span>
        <span className="text-xs text-[var(--muted)]">
          Showing {Math.min(8, vms.length)} of {vms.length}
        </span>
      </div>
      <div
        className="grid grid-cols-1 gap-px border border-[var(--border)] bg-[var(--border)] sm:grid-cols-2 lg:grid-cols-4"
        data-testid="bills-grid"
      >
        {grid.map((vm) => (
          <GridCard key={vm.bill.id} vm={vm} />
        ))}
      </div>

      {/* A4. Serendipity */}
      {serendipity.length > 0 && (
        <div className="flex flex-col gap-[30px] border border-[var(--border)] border-l-4 border-l-[var(--accent)] bg-[var(--bg-elev)] px-[26px] py-[22px] md:flex-row md:items-center">
          <div className="flex max-w-[220px] flex-col gap-1.5">
            <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[var(--accent)]">Off your radar</span>
            <span className="display text-[22px] leading-tight text-[var(--fg)]">
              {hasCompass ? "Bills that pull against you" : "Wide-open, barely mapped"}
            </span>
          </div>
          <div className="grid flex-1 grid-cols-1 gap-[22px] sm:grid-cols-3">
            {serendipity.map(({ vm, why }) => (
              <Link
                key={vm.bill.id}
                to={`/bills/${vm.bill.id}`}
                className="flex flex-col gap-1.5 border-l border-[var(--border)] pl-[18px] no-underline"
                data-testid={`serendipity-${vm.bill.externalId}`}
              >
                <span className="text-[10px] font-bold uppercase tracking-[0.12em] text-[var(--muted)]">
                  {vm.bill.identifier.split(" · ")[0]} · {why}
                </span>
                <span className="display text-[17px] leading-tight text-[var(--fg)]">
                  {vm.bill.shortTitle || vm.bill.title}
                </span>
                <span className="text-xs text-[var(--fg-soft)]">
                  {alignText(vm.alignPct)} aligned · {stageLabel(vm.stage)}
                </span>
              </Link>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

/* ---------------------------------------------------------------- ticker */

function Ticker({ vms }: { vms: BillVM[] }) {
  const items = [...vms]
    .sort((a, b) => actionMs(b) - actionMs(a))
    .slice(0, 8)
    .map((vm) => `${vm.bill.identifier.split(" · ")[0]} · ${stageLabel(vm.stage)} · ${fmtDateShort(vm.bill.latestActionDate)}`);
  return (
    <div
      className="flex items-center gap-[14px] overflow-x-auto border-y border-[var(--border)] py-[9px] text-[11px] font-bold uppercase tracking-[0.12em] text-[var(--fg-soft)] [-ms-overflow-style:none] [scrollbar-width:none]"
      style={{
        WebkitMaskImage: "linear-gradient(90deg, #000 calc(100% - 64px), transparent 100%)",
        maskImage: "linear-gradient(90deg, #000 calc(100% - 64px), transparent 100%)",
      }}
    >
      <span className="flex flex-none items-center gap-1.5 text-[var(--accent)]">
        <span className="h-[7px] w-[7px] flex-none animate-pulse rounded-full bg-[var(--accent)]" />
        Live
      </span>
      {items.map((t, i) => (
        <span key={i} className="flex flex-none items-center gap-[14px]">
          <span className="text-[var(--muted)]">·</span>
          <span className="whitespace-nowrap">{t}</span>
        </span>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ hero */

function HeroCard({ hero, mineRadar, sortLabel }: { hero: BillVM; mineRadar: string | null; sortLabel: string }) {
  const b = hero.bill;
  return (
    <div className="flex flex-col gap-[18px] border border-[var(--fg)] bg-[var(--bg-elev)] px-8 py-[30px]">
      <div className="flex flex-wrap items-center gap-2.5">
        <span className="bg-[var(--accent)] px-2 py-1 text-[10px] font-bold uppercase tracking-[0.16em] text-white">
          {sortLabel}
        </span>
        <span className="text-[11px] font-bold uppercase tracking-[0.16em] text-[var(--accent)]">
          {b.identifier.split(" · ")[0]}
        </span>
        <span
          className="ml-auto text-[11px] font-bold uppercase tracking-[0.14em]"
          style={{ color: stageColor(hero.stage) }}
        >
          {stageLabel(hero.stage)}
        </span>
      </div>
      <h2 className="display text-[46px] leading-[1.02] tracking-[-0.025em] text-[var(--fg)] [text-wrap:pretty]">
        {b.shortTitle || b.title}
      </h2>
      <p className="max-w-[56ch] text-[17px] leading-[1.55] text-[var(--fg-soft)] [text-wrap:pretty]">{b.teaser}</p>

      {/* Metrics band */}
      <div className="flex flex-col items-start gap-[30px] border-y border-[var(--border)] py-[18px] md:flex-row md:items-center">
        <AlignmentRing pct={hero.alignPct} />
        <MiniCompass radar={hero.radar} userRadar={mineRadar} size={118} billStroke={2} />
        <div className="flex flex-1 flex-col gap-2.5 self-stretch">
          {hero.axisScores.map((s, i) => {
            const unit = ((s + 1) / 2) * 100;
            const lo = Math.min(50, unit);
            const w = Math.abs(unit - 50);
            const dim = !hero.hasAxis[i];
            return (
              <div key={AXES[i].key} className="flex items-center gap-3" style={{ opacity: dim ? 0.45 : 1 }}>
                <span className="w-[118px] flex-none text-[10px] font-bold uppercase tracking-[0.1em] text-[var(--muted)]">
                  {AXES[i].name}
                </span>
                <span className="relative h-[5px] flex-1 bg-[var(--border)]">
                  <span
                    className="absolute bottom-0 top-0 bg-[var(--accent)]"
                    style={{ left: `${lo}%`, width: `${w}%` }}
                  />
                </span>
                <span className="w-[104px] flex-none text-right text-[11px] text-[var(--fg-soft)]">
                  {dim ? "—" : leanPole(i, s)}
                </span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Stage rail */}
      <div className="flex items-center gap-0">
        {STAGES.map((s, i) => {
          const on = hero.stage >= 0 && i <= hero.stage;
          return (
            <div key={s.label} className="flex flex-1 flex-col gap-1.5">
              <div className="h-1.5" style={{ background: on ? s.color : "var(--border)" }} />
              <span
                className="text-[10px] font-bold uppercase tracking-[0.1em]"
                style={{ color: on ? "var(--fg)" : "var(--muted)" }}
              >
                {s.label}
              </span>
            </div>
          );
        })}
      </div>

      {/* Actions */}
      <div className="mt-0.5 flex flex-wrap items-center gap-4">
        <ButtonLink to={`/bills/${b.id}`} variant="primary" data-testid="hero-read">
          Read the breakdown
        </ButtonLink>
        <ButtonLink to={`/bills/${b.id}`} variant="secondary">
          Compare to my compass
        </ButtonLink>
        <span className="ml-auto text-[13px] text-[var(--muted)]">
          {b.sponsor}
          {b.party ? ` [${b.party}]` : ""}
        </span>
      </div>
    </div>
  );
}

/* --------------------------------------------------------------- rail cards */

function RecentCard({ vm }: { vm: BillVM }) {
  const b = vm.bill;
  return (
    <Link
      to={`/bills/${b.id}`}
      className="flex items-start gap-4 border border-[var(--border)] bg-[var(--bg-elev)] p-[18px] no-underline transition hover:border-[var(--fg)]"
    >
      <div className="flex min-w-[58px] flex-none flex-col items-center border-r border-[var(--border)] pr-4">
        <span className="display text-[15px] leading-tight text-[var(--accent)]">{fmtDateShort(b.latestActionDate)}</span>
        <span className="text-[9px] font-bold uppercase tracking-[0.14em] text-[var(--muted)]">acted</span>
      </div>
      <div className="flex flex-col gap-[7px]">
        <span
          className="text-[10px] font-bold uppercase tracking-[0.14em]"
          style={{ color: stageColor(vm.stage) }}
        >
          {b.identifier.split(" · ")[0]} · {stageLabel(vm.stage)}
        </span>
        <span className="display text-[19px] leading-[1.15] text-[var(--fg)] [text-wrap:pretty]">
          {b.shortTitle || b.title}
        </span>
        <span className="text-xs text-[var(--muted)]">
          {alignText(vm.alignPct)} aligned · {vm.positioned} of {AXES.length} values mapped
        </span>
      </div>
    </Link>
  );
}

function CompassStatus({
  hasCompass,
  userVec,
  corpus,
}: {
  hasCompass: boolean;
  userVec: number[] | null;
  corpus: number;
}) {
  const mapped = userVec ? userVec.filter((v) => Math.abs(v) > 0.001).length : 0;
  return (
    <div className="flex flex-col gap-2.5 border border-[var(--fg)] bg-[var(--fg)] p-5 text-[var(--bg)]">
      <span className="text-[10px] font-bold uppercase tracking-[0.16em] opacity-65">Your compass</span>
      {hasCompass ? (
        <>
          <span className="display text-[30px] leading-[1.05]">
            Reading {corpus} bills against {mapped} of {AXES.length} axes
          </span>
          <span className="text-[13px] leading-[1.5] opacity-75">
            Every alignment score on this page is measured against the axes you've scored. Refine your compass and the
            whole page re-ranks.
          </span>
          <Link
            to="/settings"
            className="text-xs font-bold uppercase tracking-[0.1em] text-[var(--accent)] no-underline"
          >
            Refine your compass →
          </Link>
        </>
      ) : (
        <>
          <span className="display text-[30px] leading-[1.05]">You haven't scored your compass yet</span>
          <span className="text-[13px] leading-[1.5] opacity-75">
            Answer a short set of questions and every bill here gets ranked by how it sits against your own values.
          </span>
          <Link
            to="/quizzes"
            className="text-xs font-bold uppercase tracking-[0.1em] text-[var(--accent)] no-underline"
            data-testid="front-take-quiz"
          >
            Take the values quiz →
          </Link>
        </>
      )}
    </div>
  );
}

/* --------------------------------------------------------------- grid card */

function GridCard({ vm }: { vm: BillVM }) {
  const b = vm.bill;
  const alignN = vm.alignPct ?? 0;
  return (
    <Link
      to={`/bills/${b.id}`}
      className="group flex min-h-[236px] flex-col gap-[11px] bg-[var(--bg-elev)] p-[19px] no-underline transition hover:bg-[color-mix(in_oklab,var(--accent)_4%,var(--bg-elev))]"
      data-testid={`bill-row-${b.externalId}`}
    >
      <div className="flex items-center justify-between">
        <span
          className="text-[10px] font-bold uppercase tracking-[0.14em]"
          style={{ color: stageColor(vm.stage) }}
        >
          {b.identifier.split(" · ")[0]}
        </span>
        <span className="text-[10px] font-bold uppercase tracking-[0.1em] text-[var(--muted)]">
          {vm.positioned} value{vm.positioned === 1 ? "" : "s"}
        </span>
      </div>
      <span className="display text-xl leading-[1.14] text-[var(--fg)] [text-wrap:pretty] group-hover:text-[var(--accent)]">
        {b.shortTitle || b.title}
      </span>
      <span className="text-[13px] leading-[1.45] text-[var(--fg-soft)] [text-wrap:pretty] line-clamp-3">{b.teaser}</span>
      <div className="mt-auto flex flex-col gap-[9px]">
        <div className="flex items-center gap-2">
          <MiniCompass radar={vm.radar} size={34} />
          <div className="flex flex-1 flex-col gap-1">
            <div className="flex justify-between text-[11px] font-bold uppercase tracking-[0.08em] text-[var(--fg)]">
              <span>{alignText(vm.alignPct)} aligned</span>
              <span className="text-[var(--accent)]">
                {vm.positioned}/{AXES.length} mapped
              </span>
            </div>
            <div className="relative h-1 bg-[var(--border)]">
              <span
                className="absolute inset-y-0 left-0 bg-[var(--fg)]"
                style={{ width: `${vm.alignPct == null ? (vm.positioned / AXES.length) * 100 : alignN}%` }}
              />
            </div>
          </div>
        </div>
        <div className="flex items-center justify-between border-t border-[var(--border)] pt-2 text-[11px] text-[var(--muted)]">
          <span>{stageLabel(vm.stage)}</span>
          <span>{fmtDate(b.latestActionDate) || "—"}</span>
        </div>
      </div>
    </Link>
  );
}

/* ------------------------------------------------------------------ misc */

function EmptyCorpus() {
  return (
    <p className="py-12 text-base text-[var(--muted)]" data-testid="bills-empty">
      No bills match these filters. Try widening your alignment threshold or clearing the axis chips.
    </p>
  );
}

function actionMs(vm: BillVM): number {
  const t = new Date(vm.bill.latestActionDate ?? vm.bill.introducedDate).getTime();
  return Number.isNaN(t) ? 0 : t;
}

function pickSerendipity(vms: BillVM[], hasCompass: boolean): { vm: BillVM; why: string }[] {
  if (hasCompass) {
    return [...vms]
      .filter((v) => v.distance != null)
      .sort((a, b) => (b.distance ?? 0) - (a.distance ?? 0))
      .slice(0, 3)
      .map((vm) => ({ vm, why: vm.alignPct != null && vm.alignPct < 40 ? "Clashes with you" : "Splits your axes" }));
  }
  return [...vms]
    .sort((a, b) => a.positioned - b.positioned)
    .slice(0, 3)
    .map((vm) => ({ vm, why: "Barely mapped yet" }));
}
