import { Link } from "react-router-dom";
import type { StateProfile } from "@/taxModel/engine";

/* A surprising tax fact about a single (rotating) state, drawn from the same state
   profiles that power the "Who gets your tax dollar?" calculator. Show-info-then-link:
   the prose `notes` are the hook, the CTA sends people to the full calculator. */
export function StateTaxFactCard({ state }: { state: StateProfile }) {
  return (
    <article
      className="flex h-full flex-col border border-[var(--border)] bg-[var(--bg-elev)] p-6"
      data-testid="feature-tax-fact"
    >
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Did you know? · State taxes
      </p>
      <h2 className="display mt-2 text-2xl">
        <span aria-hidden className="mr-1">{state.glyph}</span> {state.name}
      </h2>
      <p className="mt-1 text-sm font-semibold text-[var(--fg-soft)]">
        Income tax: {state.incomeSummary}
      </p>

      <p className="mt-3 text-sm leading-relaxed text-[var(--fg)]">{state.notes}</p>

      <dl className="mt-4 flex gap-6 text-sm">
        <div>
          <dt className="text-[10px] uppercase tracking-[0.2em] text-[var(--muted)]">Sales tax</dt>
          <dd className="display text-xl tabular-nums">{(state.salesRate * 100).toFixed(2)}%</dd>
        </div>
        <div>
          <dt className="text-[10px] uppercase tracking-[0.2em] text-[var(--muted)]">Property (eff.)</dt>
          <dd className="display text-xl tabular-nums">{(state.propRate * 100).toFixed(2)}%</dd>
        </div>
      </dl>

      <div className="mt-auto pt-4">
        <Link
          to="/briefings/who-gets-your-tax-dollar"
          className="inline-flex items-center gap-1 text-sm font-semibold uppercase tracking-wider text-[var(--accent)] hover:underline"
          data-testid="feature-tax-fact-link"
        >
          See where your tax dollar goes →
        </Link>
      </div>
    </article>
  );
}
