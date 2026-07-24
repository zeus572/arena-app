import { MapPin, Activity, Layers, Ban } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { getEngagement, type FeatureStat } from "@/api/engagement";
import { useAdminData, AdminStates } from "./common";

function ago(iso: string | null): string {
  if (!iso) return "never";
  const ms = Date.now() - new Date(iso).getTime();
  if (ms < 0) return "just now";
  const d = Math.floor(ms / 86_400_000);
  if (d >= 1) return `${d}d ago`;
  const h = Math.floor(ms / 3_600_000);
  if (h >= 1) return `${h}h ago`;
  const m = Math.floor(ms / 60_000);
  return m >= 1 ? `${m}m ago` : "just now";
}

function Bar({ value, max }: { value: number; max: number }) {
  const pct = max > 0 ? Math.min(100, Math.round((value / max) * 100)) : 0;
  return (
    <div className="h-2 w-full rounded bg-[var(--border)]">
      <div className="h-full rounded bg-[var(--accent)] transition-[width] duration-500" style={{ width: `${pct}%` }} />
    </div>
  );
}

function Tile({ label, value, hint }: { label: string; value: number | string; hint?: string }) {
  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-4">
      <p className="text-2xl font-semibold tabular-nums">{value}</p>
      <p className="mt-0.5 text-xs uppercase tracking-wider text-[var(--muted)]">{label}</p>
      {hint && <p className="mt-1 text-[11px] text-[var(--muted)]">{hint}</p>}
    </div>
  );
}

function FeatureRow({ f, denom }: { f: FeatureStat; denom: number }) {
  const dead = f.users === 0;
  return (
    <div className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-1 py-2" data-testid={`engagement-feature-${f.key}`}>
      <div className="flex items-baseline justify-between gap-3">
        <span className={dead ? "text-sm text-[var(--muted)]" : "text-sm font-medium"}>{f.label}</span>
        {dead ? (
          <span className="shrink-0 text-[10px] font-semibold uppercase tracking-wider text-[var(--state)]">not engaged</span>
        ) : (
          <span className="shrink-0 text-[11px] text-[var(--muted)]">{f.activeLong} active · last {ago(f.lastAt)}</span>
        )}
      </div>
      <span className="row-span-2 self-center text-right text-sm font-semibold tabular-nums">
        {f.users}
        <span className="ml-1 text-[11px] font-normal text-[var(--muted)]">
          {f.events !== f.users ? `/ ${f.events} ev` : "user" + (f.users === 1 ? "" : "s")}
        </span>
      </span>
      <Bar value={f.users} max={denom} />
    </div>
  );
}

export default function Engagement() {
  const { isAuthenticated, isLoading } = useAuth();
  const { data, status } = useAdminData(() => getEngagement(), !isLoading && isAuthenticated);

  if (status !== "ok" || !data) return <AdminStates status={status} testid="engagement" />;

  const { summary, features, areas, byState, breadth, untracked } = data;
  const denom = Math.max(1, summary.profiles, ...features.map((f) => f.users));
  const breadthMax = Math.max(1, ...breadth.map((b) => b.users));

  return (
    <section data-testid="engagement-page">
      <header>
        <h1 className="display text-3xl md:text-4xl">Where people are engaged</h1>
        <p className="mt-3 max-w-prose text-[var(--fg-soft)]">
          Distinct real users per feature (anonymous and agent activity excluded), how recently
          they acted, and how it breaks down by locality. The drop-off between rows is where people
          aren’t engaged. Counts only — no individual is identified.
        </p>
      </header>

      <div className="mt-8 grid grid-cols-2 gap-3 md:grid-cols-5" data-testid="engagement-summary">
        <Tile label="Civic profiles" value={summary.profiles} hint="onboarded base" />
        <Tile label="Engaged users" value={summary.engagedUsers} hint="any feature, ever" />
        <Tile label={`Active ${data.longWindowDays}d`} value={summary.activeUsersLong} />
        <Tile label={`Active ${data.shortWindowDays}d`} value={summary.activeUsersShort} />
        <Tile label="Anon events" value={summary.anonymousEvents} hint="excluded from counts" />
      </div>

      <div className="mt-12">
        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
          <Layers size={16} className="text-[var(--accent)]" /> Feature funnel
        </h2>
        <div className="mt-4 grid gap-5">
          {areas.map((area) => {
            const rows = features.filter((f) => f.area === area.area);
            return (
              <div key={area.area} className="border border-[var(--border)] p-4" data-testid={`engagement-area-${area.area}`}>
                <div className="flex items-baseline justify-between gap-3 border-b border-[var(--border)] pb-2">
                  <h3 className="text-sm font-semibold">{area.area}</h3>
                  <span className="text-[11px] uppercase tracking-wider text-[var(--muted)]">
                    {area.users} user{area.users === 1 ? "" : "s"} · {area.activeLong} active {data.longWindowDays}d
                  </span>
                </div>
                <div className="mt-1 divide-y divide-[var(--border)]">
                  {rows.map((f) => (
                    <FeatureRow key={f.key} f={f} denom={denom} />
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="mt-12">
        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
          <MapPin size={16} className="text-[var(--accent)]" /> By locality
        </h2>
        <div className="mt-4 overflow-x-auto border border-[var(--border)]">
          <table className="w-full min-w-[560px] border-collapse text-sm" data-testid="engagement-by-state">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-[11px] uppercase tracking-wider text-[var(--muted)]">
                <th className="p-2 font-medium">State</th>
                <th className="p-2 text-right font-medium">Profiles</th>
                <th className="p-2 text-right font-medium">Engaged</th>
                {areas.map((a) => (
                  <th key={a.area} className="p-2 text-right font-medium">{a.area}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {byState.map((s) => (
                <tr key={s.state} className="border-b border-[var(--border)] last:border-0">
                  <td className="p-2 font-semibold uppercase">{s.state}</td>
                  <td className="p-2 text-right tabular-nums">{s.profiles}</td>
                  <td className="p-2 text-right tabular-nums">{s.engagedUsers}</td>
                  {areas.map((a) => {
                    const n = s.byArea[a.area] ?? 0;
                    return (
                      <td key={a.area} className={`p-2 text-right tabular-nums ${n === 0 ? "text-[var(--muted)]" : ""}`}>{n}</td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="mt-2 text-[11px] text-[var(--muted)]">
          Locality is the user’s self-reported state (CA/WA/MD supported; “national” = unset).
          “unknown” = engaged users with no Civic profile. The debate app carries no locality.
        </p>
      </div>

      <div className="mt-12">
        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
          <Activity size={16} className="text-[var(--accent)]" /> Engagement breadth
        </h2>
        <p className="mt-1 text-xs text-[var(--muted)]">How many feature areas each engaged user touches.</p>
        <div className="mt-4 grid gap-2" data-testid="engagement-breadth">
          {breadth.map((b) => (
            <div key={b.areasTouched} className="grid grid-cols-[6rem_1fr_2.5rem] items-center gap-3">
              <span className="text-xs text-[var(--muted)]">{b.areasTouched} area{b.areasTouched === 1 ? "" : "s"}</span>
              <Bar value={b.users} max={breadthMax} />
              <span className="text-right text-sm font-semibold tabular-nums">{b.users}</span>
            </div>
          ))}
        </div>
      </div>

      {untracked.length > 0 && (
        <div className="mt-12 border border-dashed border-[var(--border)] bg-[var(--state-soft)] p-4" data-testid="engagement-untracked">
          <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-[var(--muted)]">
            <Ban size={16} className="text-[var(--state)]" /> Not measurable yet
          </h2>
          <ul className="mt-3 grid gap-2">
            {untracked.map((u) => (
              <li key={u.key} className="text-sm">
                <span className="font-semibold">{u.label}</span>
                <span className="text-[var(--fg-soft)]"> — {u.note}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="mt-12 text-xs text-[var(--muted)]">
        Generated {new Date(data.generatedAt).toLocaleString()}. Admin-only · aggregate counts · live from the database.
      </p>
    </section>
  );
}
