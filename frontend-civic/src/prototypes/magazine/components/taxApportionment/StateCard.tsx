import type { StateProfile } from "@/taxModel/engine";
import { pct } from "./format";

/** Selected state's headline stats + stored `notes` prose (§7.5). */
export function StateCard({ profile }: { profile: StateProfile }) {
  const stats = [
    { label: "Income tax", value: profile.incomeSummary },
    { label: "Combined sales", value: pct(profile.salesRate) },
    { label: "Eff. property rate", value: pct(profile.propRate) },
  ];

  return (
    <div
      className="border border-[var(--border)] bg-[var(--bg-elev)] p-6"
      data-testid="tax-state-card"
    >
      <div className="flex items-center gap-3">
        <span className="text-3xl" aria-hidden>{profile.glyph}</span>
        <h3 className="display text-3xl">{profile.name}</h3>
      </div>
      <dl className="mt-5 grid gap-4 sm:grid-cols-3">
        {stats.map((s) => (
          <div key={s.label} className="border-l-2 border-[var(--state)] pl-3">
            <dt className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              {s.label}
            </dt>
            <dd className="mt-1 text-base font-semibold">{s.value}</dd>
          </div>
        ))}
      </dl>
      <p className="mt-5 text-sm leading-relaxed text-[var(--fg-soft)]">{profile.notes}</p>
    </div>
  );
}
