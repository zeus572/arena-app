import { useState } from "react";
import { AlertTriangle, Check, X } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import {
  approveDailyPuzzle,
  getAdminDailyBalance,
  getAdminDailyPuzzles,
  rejectDailyPuzzle,
  type AdminDailyPuzzle,
} from "@/api/adminDaily";
import { Button } from "../../components/Button";
import { useAdminData, AdminStates } from "./common";

/**
 * The daily-games review queue.
 *
 * Fork and Time Machine generate into Draft because a bad puzzle in either is publicly
 * visible and can read as partisan. Without someone working this queue those two games
 * never go live at all — so this page is load-bearing, not a nicety.
 *
 * Also shows the bank-balance audit: a puzzle bank is an editorial position whether or
 * not anyone intended one, so drift needs to be visible.
 */
export default function AdminDaily() {
  const { isAuthenticated } = useAuth();
  const [filter, setFilter] = useState<string>("Draft");
  const [busyId, setBusyId] = useState<string | null>(null);

  const puzzles = useAdminData<AdminDailyPuzzle[]>(
    () => getAdminDailyPuzzles(filter === "All" ? undefined : filter),
    isAuthenticated,
  );
  const balance = useAdminData(() => getAdminDailyBalance(), isAuthenticated);

  const act = async (id: string, approve: boolean) => {
    setBusyId(id);
    try {
      await (approve ? approveDailyPuzzle(id) : rejectDailyPuzzle(id));
      puzzles.reload();
      balance.reload();
    } finally {
      setBusyId(null);
    }
  };

  if (puzzles.status !== "ok") {
    return <AdminStates status={puzzles.status} testid="admin-daily" />;
  }

  const rows = puzzles.data ?? [];
  const bal = balance.data;
  const smallerShare = bal ? Math.round(bal.magnitudeSmallerShare * 100) : 0;
  const skewed = bal ? bal.magnitudeSmallerShare < 0.45 || bal.magnitudeSmallerShare > 0.55 : false;

  return (
    <section data-testid="admin-daily">
      <div className="flex flex-wrap items-center gap-2">
        {["Draft", "Live", "Retired", "All"].map((s) => (
          <Button
            key={s}
            size="sm"
            variant={filter === s ? "primary" : "ghost"}
            onClick={() => {
              setFilter(s);
              puzzles.reload();
            }}
            data-testid={`admin-daily-filter-${s}`}
          >
            {s}
          </Button>
        ))}
      </div>

      {rows.length === 0 ? (
        <p className="py-10 text-sm text-[var(--muted)]" data-testid="admin-daily-empty">
          Nothing in this state.
        </p>
      ) : (
        <ul className="mt-5 space-y-3">
          {rows.map((p) => (
            <li
              key={p.id}
              className="border border-[var(--border)] bg-[var(--bg-elev)] p-4"
              data-testid={`admin-daily-row-${p.id}`}
            >
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <span className="text-sm font-semibold">
                  {p.kind} #{p.edition}
                </span>
                <span className="text-xs uppercase tracking-wider text-[var(--muted)]">
                  {p.puzzleDate} · {p.status} · {p.generationSource} · {p.plays} plays
                </span>
              </div>

              {/* Reviewers need the answer key — that's the point of the queue. */}
              <pre className="mt-3 max-h-64 overflow-auto whitespace-pre-wrap break-words border border-[var(--border)] bg-[var(--bg)] p-3 text-xs text-[var(--fg-soft)]">
                {JSON.stringify(p.payload, null, 2)}
              </pre>

              {p.status === "Draft" && (
                <div className="mt-3 flex gap-2">
                  <Button
                    size="sm"
                    variant="positive"
                    disabled={busyId === p.id}
                    onClick={() => void act(p.id, true)}
                    data-testid={`admin-daily-approve-${p.id}`}
                  >
                    <Check /> Approve
                  </Button>
                  <Button
                    size="sm"
                    variant="danger"
                    disabled={busyId === p.id}
                    onClick={() => void act(p.id, false)}
                    data-testid={`admin-daily-reject-${p.id}`}
                  >
                    <X /> Reject
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {bal && (
        <section className="mt-10" data-testid="admin-daily-balance">
          <h2 className="display text-2xl">Bank balance</h2>
          <p className="mt-1 text-sm text-[var(--fg-soft)]">
            A bank leaning one way is an editorial position whether or not anyone meant it.
          </p>

          <div className="mt-4 border border-[var(--border)] bg-[var(--bg-elev)] p-4">
            <p className="text-sm">
              Magnitudes that are <strong>smaller than you'd think</strong>:{" "}
              <span className={skewed ? "font-semibold text-amber-600" : "font-semibold"}>
                {smallerShare}%
              </span>{" "}
              of {bal.magnitudeTotal} ({bal.magnitudeSmallerCount})
            </p>
            {skewed && (
              <p className="mt-2 flex items-center gap-1.5 text-xs font-semibold text-amber-600">
                <AlertTriangle size={13} /> Outside the 45–55% target — the bank is arguing a
                thesis.
              </p>
            )}
            {bal.staleMagnitudeKeys.length > 0 && (
              <p className="mt-2 text-xs text-[var(--muted)]">
                Needs re-verification: {bal.staleMagnitudeKeys.join(", ")}
              </p>
            )}
          </div>

          {Object.keys(bal.forkAxisCounts).length > 0 && (
            <div className="mt-3 border border-[var(--border)] bg-[var(--bg-elev)] p-4">
              <p className="text-xs uppercase tracking-wider text-[var(--muted)]">
                Fork axes, last 30 days
              </p>
              <ul className="mt-2 space-y-1 text-sm">
                {Object.entries(bal.forkAxisCounts)
                  .sort((a, b) => b[1] - a[1])
                  .map(([axis, count]) => (
                    <li key={axis} className="flex justify-between">
                      <span>{axis}</span>
                      <span className="tabular-nums text-[var(--muted)]">{count}</span>
                    </li>
                  ))}
              </ul>
            </div>
          )}
        </section>
      )}
    </section>
  );
}
