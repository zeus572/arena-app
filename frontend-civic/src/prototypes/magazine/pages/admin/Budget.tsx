import { Coins } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { getAdminBudget } from "@/api/adminBudget";
import { useAdminData, AdminStates } from "./common";

function ago(iso: string | null): string {
  if (!iso) return "never";
  const ms = Date.now() - new Date(iso).getTime();
  if (ms < 0) return "just now";
  const h = Math.floor(ms / 3_600_000);
  if (h >= 24) return `${Math.floor(h / 24)}d ago`;
  if (h >= 1) return `${h}h ago`;
  const m = Math.floor(ms / 60_000);
  return m >= 1 ? `${m}m ago` : "just now";
}

export default function Budget() {
  const { isAuthenticated, isLoading } = useAuth();
  const { data, status } = useAdminData(() => getAdminBudget(), !isLoading && isAuthenticated);

  if (status !== "ok" || !data) return <AdminStates status={status} testid="admin-budget" />;

  const rows = [...data.candidates].sort((a, b) => b.postsLast24h - a.postsLast24h || b.postsTotal - a.postsTotal);

  return (
    <section data-testid="admin-budget-page">
      <header>
        <h1 className="display text-3xl md:text-4xl">Candidate post budget</h1>
        <p className="mt-3 max-w-prose text-[var(--fg-soft)]">
          AI candidate post volume — a proxy for LLM spend. Watch the last-24h column and
          intensity-5 spikes for runaway generation.
        </p>
        <div className="mt-4 flex flex-wrap gap-4 text-xs uppercase tracking-wider text-[var(--muted)]">
          <span data-testid="admin-budget-total">{data.totalPosts} posts all-time</span>
          <span data-testid="admin-budget-24h">{data.postsLast24h} in last 24h</span>
        </div>
      </header>

      <div className="mt-8 overflow-x-auto border border-[var(--border)]">
        <table className="w-full min-w-[560px] border-collapse text-sm" data-testid="admin-budget-table">
          <thead>
            <tr className="border-b border-[var(--border)] text-left text-[11px] uppercase tracking-wider text-[var(--muted)]">
              <th className="p-2 font-medium">Candidate</th>
              <th className="p-2 text-right font-medium">Last 24h</th>
              <th className="p-2 text-right font-medium">Intensity 5 (24h)</th>
              <th className="p-2 text-right font-medium">Total</th>
              <th className="p-2 text-right font-medium">Last post</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((c) => (
              <tr key={c.candidateId} className="border-b border-[var(--border)] last:border-0" data-testid={`admin-budget-row-${c.slug}`}>
                <td className="p-2 font-medium">{c.name}</td>
                <td className="p-2 text-right tabular-nums">{c.postsLast24h}</td>
                <td className={`p-2 text-right tabular-nums ${c.intensity5Last24h > 0 ? "font-semibold text-[var(--state)]" : "text-[var(--muted)]"}`}>
                  {c.intensity5Last24h}
                </td>
                <td className="p-2 text-right tabular-nums">{c.postsTotal}</td>
                <td className="p-2 text-right text-[var(--muted)]">{ago(c.lastPostAt)}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={5} className="p-4 text-center text-[var(--muted)]">
                  <Coins size={16} className="mx-auto mb-1" /> No candidate posts yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
