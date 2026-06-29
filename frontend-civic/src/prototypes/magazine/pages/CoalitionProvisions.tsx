import { useEffect, useMemo, useState, type ComponentType } from "react";
import { Link } from "react-router-dom";
import {
  Handshake, RefreshCw, Award, Clock,
  ChevronDown, GitFork, CheckCircle2, Skull, Sparkles,
} from "lucide-react";
import clsx from "clsx";
import {
  listProvisions,
  seedCoalition,
  getMe,
  composeCircles,
  type ProvisionSummary,
  type Me,
} from "@/api/coalition";
import { localityLabel } from "@/api/profile";
import { Button } from "../components/Button";

function stateColor(state: string): string {
  switch (state) {
    case "Passed": return "text-emerald-600";
    case "Forked": return "text-amber-600";
    case "Died": return "text-[var(--muted)]";
    default: return "text-[var(--accent)]";
  }
}

// A bill is "closed" once its coalition resolves: it either passed (a spanning
// coalition formed), forked (split into two governable answers), or died (no
// bridge before the deadline). Everything else — Birth / Open / Contested /
// NearCoalition — is still live and belongs in the open section.
const CLOSED_STATES = ["Passed", "Forked", "Died"] as const;
type ClosedState = (typeof CLOSED_STATES)[number];

function isClosed(state: string): state is ClosedState {
  return (CLOSED_STATES as readonly string[]).includes(state);
}

// Visual language for closed outcomes, so passed / forked / dead read at a
// glance without opening the bill. Passed is the success case (emerald), forked
// is a partial win (amber), and died is greyed back so it recedes.
const CLOSED_STYLE: Record<ClosedState, {
  label: string;
  Icon: ComponentType<{ size?: number; className?: string }>;
  card: string;
  accent: string;
  dead: boolean;
}> = {
  Passed: {
    label: "Passed",
    Icon: CheckCircle2,
    card: "border-emerald-200 border-l-4 border-l-emerald-500 bg-emerald-50/40 hover:border-emerald-400",
    accent: "text-emerald-600",
    dead: false,
  },
  Forked: {
    label: "Forked",
    Icon: GitFork,
    card: "border-amber-200 border-l-4 border-l-amber-500 bg-amber-50/40 hover:border-amber-400",
    accent: "text-amber-600",
    dead: false,
  },
  Died: {
    label: "Died",
    Icon: Skull,
    card: "border-dashed border-l-4 border-l-[var(--line)] opacity-70 grayscale hover:opacity-100 hover:grayscale-0",
    accent: "text-[var(--muted)]",
    dead: true,
  },
};

function breadthScore(p: ProvisionSummary): number {
  return p.totalBuckets > 0 ? p.coveredBuckets / p.totalBuckets : 0;
}

function difficultyBadge(difficulty: string) {
  const map: Record<string, string> = {
    Narrow: "bg-emerald-100 text-emerald-700",
    Moderate: "bg-amber-100 text-amber-700",
    Wide: "bg-rose-100 text-rose-700",
  };
  return (
    <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ${map[difficulty] ?? "bg-[var(--line)] text-[var(--muted)]"}`}>
      {difficulty}
    </span>
  );
}

// A provision closes on its deadline (the 7-day rolling window). Show a friendly
// countdown so users can see when a coalition is leaving; the absolute date is on hover.
function Deadline({ deadline }: { deadline: string | null }) {
  if (!deadline) return null;
  const end = new Date(deadline);
  const ms = end.getTime();
  if (Number.isNaN(ms)) return null;

  const remaining = ms - Date.now();
  const absolute = end.toLocaleDateString(undefined, { month: "short", day: "numeric" });

  let text: string;
  let urgent = false;
  if (remaining <= 0) {
    text = "Closed";
  } else {
    const hours = remaining / 3_600_000;
    if (hours < 24) {
      const h = Math.max(1, Math.ceil(hours));
      text = `Closes in ${h}h`;
      urgent = true;
    } else {
      const days = Math.ceil(hours / 24);
      text = `Closes in ${days} day${days === 1 ? "" : "s"}`;
      urgent = days <= 2;
    }
  }

  return (
    <span
      className={`flex items-center gap-1 font-semibold ${urgent ? "text-rose-600" : "text-[var(--muted)]"}`}
      title={`Closes ${absolute}`}
      data-testid="provision-deadline"
    >
      <Clock size={12} /> {text}
    </span>
  );
}

// A slim standing snapshot: lead with the Circle (the user's cohort ladder rung —
// the one consistent identity label), then the two live currencies. Circle
// *standings* (gap, promote/relegate) and the fuller skill breakdown live on the
// Cohort page, so we deliberately don't duplicate them here.
function RecordCard({ me }: { me: Me }) {
  return (
    <div className="rounded-2xl border border-[var(--line)] p-5">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-lg font-semibold"><Award size={18} className="text-[var(--accent)]" /> Your standing</h2>
        <span className="inline-flex items-center rounded-full bg-[var(--accent)]/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.1em] text-[var(--accent)]">
          {me.circleName ? `${me.circleName} Circle` : "Unassigned"}
        </span>
      </div>

      {/* Two clocks: daily reasoning XP vs scarce coalition currency */}
      <div className="mt-4 grid grid-cols-2 gap-3">
        <div className="rounded-xl border border-[var(--line)] p-3">
          <p className="text-[10px] uppercase tracking-wider text-[var(--muted)]">Daily reasoning XP</p>
          <p className="text-2xl font-bold">{me.todayReasoning}
            <span className="text-sm font-normal text-[var(--muted)]">/{me.dailyReasoningCap} today</span>
          </p>
          <p className="text-[10px] text-[var(--muted)]">{me.reasoningXp} all-time · diminishing returns</p>
        </div>
        <div className="rounded-xl border border-[var(--line)] p-3">
          <p className="text-[10px] uppercase tracking-wider text-[var(--muted)]">Coalition (scarce)</p>
          <p className="text-2xl font-bold text-[var(--accent)]">{me.scarcePoints}</p>
          <p className="text-[10px] text-[var(--muted)]">uncapped · breadth premium</p>
        </div>
      </div>
    </div>
  );
}

// Shared metadata row (locality, difficulty, governance, distance, breadth,
// deadline) used by both open and closed bill cards.
function ProvisionMeta({ p }: { p: ProvisionSummary }) {
  return (
    <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-[var(--muted)]">
      {p.locality && (
        <span
          className="rounded-full bg-[var(--accent)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-white"
          data-testid={`provision-local-badge-${p.locality}`}
        >
          Local · {localityLabel(p.locality)}
        </span>
      )}
      {difficultyBadge(p.difficulty)}
      <span className="rounded-full bg-[var(--line)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
        {p.governance ? "governance" : "culture"}
      </span>
      <span>distance {(p.distance * 100).toFixed(0)}%</span>
      <span>breadth {p.coveredBuckets}/{p.totalBuckets}</span>
      <Deadline deadline={p.deadline} />
    </div>
  );
}

// An open (still-live) bill card.
function OpenProvisionRow({ p }: { p: ProvisionSummary }) {
  return (
    <li>
      <Link
        to={`/coalition/${p.id}`}
        className="block rounded-2xl border border-[var(--line)] p-4 transition hover:border-[var(--accent)]"
        data-testid="open-provision"
      >
        <div className="flex items-center justify-between gap-3">
          <h4 className="flex items-center gap-2 font-semibold">
            <Handshake size={16} className="text-[var(--accent)]" /> {p.title}
          </h4>
          <span className={`text-xs font-semibold uppercase tracking-wider ${stateColor(p.state)}`}>{p.state}</span>
        </div>
        {/* The title is often too terse to convey the actual issue; surface the
            neutral proposition text so the core question reads at a glance. */}
        {p.neutralText && (
          <p className="mt-1.5 line-clamp-2 text-sm text-[var(--fg-soft)]">{p.neutralText}</p>
        )}
        <ProvisionMeta p={p} />
      </Link>
    </li>
  );
}

// A closed bill card, styled by outcome so dead and successful coalitions are
// visually distinct. `compact` is used for the collapsed-section highlights.
function ClosedProvisionRow({ p, compact = false }: { p: ProvisionSummary; compact?: boolean }) {
  const style = CLOSED_STYLE[p.state as ClosedState] ?? CLOSED_STYLE.Died;
  const { Icon } = style;
  return (
    <li>
      <Link
        to={`/coalition/${p.id}`}
        className={clsx("block rounded-2xl border transition", compact ? "p-3" : "p-4", style.card)}
        data-testid="closed-provision"
        data-state={p.state}
      >
        <div className="flex items-center justify-between gap-3">
          <h4 className={clsx("flex items-center gap-2 font-semibold", style.dead && "text-[var(--muted)]")}>
            <Icon size={16} className={style.accent} /> {p.title}
          </h4>
          <span className={`flex items-center gap-1 text-xs font-semibold uppercase tracking-wider ${style.accent}`}>
            {style.label}
          </span>
        </div>
        {!compact && <ProvisionMeta p={p} />}
      </Link>
    </li>
  );
}

// Collapsible "Closed" section. Starts collapsed but surfaces a few interesting
// outcomes (the broadest coalitions that passed or forked) so the wins stay
// visible without expanding the whole archive.
function ClosedSection({ items }: { items: ProvisionSummary[] }) {
  const [open, setOpen] = useState(false);

  const counts = useMemo(() => ({
    passed: items.filter((p) => p.state === "Passed").length,
    forked: items.filter((p) => p.state === "Forked").length,
    died: items.filter((p) => p.state === "Died").length,
  }), [items]);

  // "Interesting" = the broadest coalitions that actually reached an answer.
  const highlights = useMemo(
    () => items
      .filter((p) => p.state === "Passed" || p.state === "Forked")
      .sort((a, b) => breadthScore(b) - breadthScore(a))
      .slice(0, 3),
    [items],
  );

  if (items.length === 0) return null;

  return (
    <div data-testid="closed-section">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full items-center justify-between gap-3 text-left"
        data-testid="closed-toggle"
      >
        <h3 className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
          Closed
          <span className="flex items-center gap-2 text-[10px] font-semibold normal-case tracking-normal">
            <span className="text-emerald-600">{counts.passed} passed</span>
            <span className="text-amber-600">{counts.forked} forked</span>
            <span className="text-[var(--muted)]">{counts.died} died</span>
          </span>
        </h3>
        <ChevronDown size={18} className={clsx("shrink-0 text-[var(--muted)] transition-transform", open && "rotate-180")} />
      </button>

      {/* Collapsed: tease the best outcomes. Expanded: the full archive. */}
      {!open && highlights.length > 0 && (
        <div className="mt-3" data-testid="closed-highlights">
          <p className="mb-2 flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-[var(--muted)]">
            <Sparkles size={12} className="text-[var(--accent)]" /> Notable outcomes
          </p>
          <ul className="grid gap-2">
            {highlights.map((p) => <ClosedProvisionRow key={p.id} p={p} compact />)}
          </ul>
        </div>
      )}

      {open && (
        <ul className="mt-3 grid gap-3">
          {items.map((p) => <ClosedProvisionRow key={p.id} p={p} />)}
        </ul>
      )}
    </div>
  );
}

export default function CoalitionProvisions() {
  const [items, setItems] = useState<ProvisionSummary[]>([]);
  const [me, setMe] = useState<Me | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [seeding, setSeeding] = useState(false);

  function load() {
    void Promise.all([listProvisions(), getMe()])
      .then(([p, m]) => { setItems(p); setMe(m); })
      .finally(() => setLoaded(true));
  }
  useEffect(load, []);

  const openItems = useMemo(() => items.filter((p) => !isClosed(p.state)), [items]);
  const closedItems = useMemo(() => items.filter((p) => isClosed(p.state)), [items]);

  async function reseed() {
    setSeeding(true);
    try { await seedCoalition(); await composeCircles(); load(); } finally { setSeeding(false); }
  }

  return (
    <section data-testid="coalition-page">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">Coalitions</p>
        <h1 className="display mt-1 text-4xl">Bridge the spectrum</h1>
        <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
          Take a position, propose a carve-out, and co-sign the version that pulls a cross-spectrum
          coalition together before the deadline. Your record and circle reward breadth and
          bridging — not volume. (Circle standings live on the Cohort page.)
        </p>
      </header>

      {import.meta.env.DEV && (
        <div className="mt-6 flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={reseed} disabled={seeding}>
            <RefreshCw size={14} className={seeding ? "animate-spin" : ""} /> Seed / recompose demo (dev)
          </Button>
        </div>
      )}

      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]">Loading…</p>
      ) : (
        <div className="mt-8 grid gap-6">
          {me && <RecordCard me={me} />}

          {items.length === 0 ? (
            <p className="py-8 text-sm text-[var(--muted)]">No bills right now. Check back soon.</p>
          ) : (
            <>
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">Open bills</h3>
                {openItems.length === 0 ? (
                  <p className="mt-2 py-6 text-sm text-[var(--muted)]">No open bills right now. Check back soon.</p>
                ) : (
                  <ul className="mt-2 grid gap-3">
                    {openItems.map((p) => <OpenProvisionRow key={p.id} p={p} />)}
                  </ul>
                )}
              </div>

              <ClosedSection items={closedItems} />
            </>
          )}
        </div>

      )}
    </section>
  );
}
