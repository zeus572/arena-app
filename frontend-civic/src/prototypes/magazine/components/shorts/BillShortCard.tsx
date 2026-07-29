import { Link } from "react-router-dom";
import type { BillSummary } from "@/api/bills";
import { AXES, stageIndex, stageLabel } from "../../pages/bills/model";
import { EphemeralReaction } from "./EphemeralReaction";
import { ShortCardShell } from "./ShortCardShell";

/**
 * Full-viewport Shorts card for a real bill before Congress.
 *
 * A bill is a fact card, not a reflective one: identifier, sponsor, where it is in the
 * pipeline, what it does, and which values it pushes on. It deliberately does NOT show
 * the reader's own alignment — the list endpoint doesn't carry a compass, and computing
 * one client-side for a card the reader is scrolling past would be a stronger claim than
 * the data supports. The deep link into the bill page is where alignment lives.
 */
export function BillShortCard({ bill }: { bill: BillSummary }) {
  const stage = stageIndex(bill.status);
  const axes = topAxes(bill);

  return (
    <ShortCardShell>
      <div className="my-4 flex flex-1 flex-col justify-center" data-testid="short-bill">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          In Congress · {bill.identifier}
        </p>

        <h2 className="display mt-3 text-2xl leading-snug">
          {bill.shortTitle ?? bill.title}
        </h2>

        <p className="mt-3 text-sm text-[var(--muted)]">
          {bill.sponsor}
          {bill.party ? ` (${bill.party})` : ""} · {stageLabel(stage)}
        </p>

        <p className="mt-4 text-base leading-relaxed text-[var(--fg-soft)]">{bill.teaser}</p>

        {axes.length > 0 && (
          <div className="mt-5" data-testid="short-bill-axes">
            <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              Pushes on
            </p>
            <ul className="mt-2 flex flex-wrap gap-2">
              {axes.map((axis) => (
                <li
                  key={axis.key}
                  className="border border-[var(--border)] px-3 py-1.5 text-xs font-semibold"
                >
                  {axis.name} → {axis.side}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>

      <div className="mt-4">
        <EphemeralReaction
          prompt="Would you want this to pass?"
          options={[
            { key: "yes", label: "Yes" },
            { key: "no", label: "No" },
          ]}
          testId="short-bill-react"
        />
        <Link
          to={`/bills/${bill.id}`}
          className="mt-3 block text-right text-sm font-semibold text-[var(--accent)] hover:underline"
          data-testid="short-bill-open"
        >
          See it against your compass →
        </Link>
      </div>
    </ShortCardShell>
  );
}

/**
 * The axes this bill pushes hardest on, as name + which pole. Positions too close to
 * centre are dropped — "Authority → Centralized" on a 0.04 score reads as a claim the
 * synthesis didn't make.
 */
const NEUTRAL_BAND = 0.15;
const MAX_AXES = 3;

type AxisChip = { key: string; name: string; side: string };

function topAxes(bill: BillSummary): AxisChip[] {
  return (bill.axes ?? [])
    .filter((a) => Math.abs(a.score) >= NEUTRAL_BAND)
    .sort((a, b) => Math.abs(b.score) * b.confidence - Math.abs(a.score) * a.confidence)
    .flatMap<AxisChip>((a) => {
      // A bill can carry any of the backend's 15 axes; only the six the Explore pages
      // name are renderable here, and an unnamed key is skipped rather than shown raw.
      const def = AXES.find((x) => x.key === a.axisKey);
      return def ? [{ key: a.axisKey, name: def.name, side: a.score >= 0 ? def.high : def.low }] : [];
    })
    .slice(0, MAX_AXES);
}
