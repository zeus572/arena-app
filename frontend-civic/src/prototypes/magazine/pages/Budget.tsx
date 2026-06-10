import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button, ButtonLink } from "../components/Button";
import {
  completeBudgetSession,
  getBudgetCategories,
  getCurrentBudgetSession,
  setBudgetAllocations,
  startBudgetSession,
  type BudgetCategory,
  type BudgetSession,
} from "@/api/budget";

const TARGET = 100;

export default function MagazineBudget() {
  const [categories, setCategories] = useState<BudgetCategory[]>([]);
  const [session, setSession] = useState<BudgetSession | null>(null);
  const [points, setPoints] = useState<Record<string, number>>({});
  const [loaded, setLoaded] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    void Promise.all([getBudgetCategories(), getCurrentBudgetSession()])
      .then(async ([cats, current]) => {
        setCategories(cats);
        const s = current ?? (await startBudgetSession());
        setSession(s);
        const initial: Record<string, number> = {};
        for (const c of cats) {
          const found = s.allocations.find((a) => a.categoryKey === c.key);
          initial[c.key] = found?.points ?? 0;
        }
        setPoints(initial);
      })
      .finally(() => setLoaded(true));
  }, []);

  const total = useMemo(
    () => Object.values(points).reduce((sum, n) => sum + n, 0),
    [points],
  );
  const delta = total - TARGET;

  function adjust(key: string, value: number) {
    setError(null);
    const clamped = Math.max(0, Math.min(100, Math.round(value)));
    setPoints((p) => ({ ...p, [key]: clamped }));
  }

  async function submit() {
    if (!session) return;
    if (total !== TARGET) {
      setError(
        delta > 0
          ? `You're ${delta} points over. Trim something to make 100.`
          : `You're ${-delta} points short. Add ${-delta} more somewhere.`,
      );
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const allocations = categories.map((c) => ({
        categoryKey: c.key,
        points: points[c.key] ?? 0,
      }));
      await setBudgetAllocations(session.id, allocations);
      await completeBudgetSession(session.id);
      setCompleted(true);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading the budget…
      </p>
    );
  }

  if (completed) {
    return (
      <article
        className="mx-auto max-w-3xl py-16 text-center"
        data-testid="budget-done"
      >
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Budget locked in
        </p>
        <h1 className="display mt-3 text-5xl">Your priorities are saved.</h1>
        <p className="mt-6 text-lg leading-relaxed text-[var(--fg-soft)]">
          Your Civic Compass will reflect how you spent these 100 points.
        </p>
        <div className="mt-8 flex justify-center gap-4">
          <Button
            onClick={() => navigate("/profile")}
            data-testid="see-profile"
          >
            See your updated profile
          </Button>
          <ButtonLink to="/" variant="ghost">
            Back to the issue
          </ButtonLink>
        </div>
      </article>
    );
  }

  return (
    <article className="mx-auto max-w-3xl" data-testid="budget">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mt-8">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Build your federal budget
        </p>
        <h1 className="display mt-3 text-4xl md:text-5xl">
          Spend 100 points across {categories.length} areas.
        </h1>
        <p className="mt-4 text-base leading-relaxed text-[var(--fg-soft)]">
          There's no right answer. What you cut and what you fund tells us
          something about what you value.
        </p>
      </header>

      <div
        className="sticky top-4 z-10 mt-8 border border-[var(--border)] bg-[var(--bg)] p-4 shadow-sm"
        data-testid="budget-total"
        data-total={total}
      >
        <div className="flex items-baseline justify-between">
          <p className="display text-2xl">
            <span data-testid="total-points">{total}</span>
            <span className="text-base text-[var(--muted)]"> / {TARGET}</span>
          </p>
          <p
            className={`text-sm font-semibold ${
              delta === 0
                ? "text-[var(--accent)]"
                : "text-[var(--muted)]"
            }`}
            data-testid="budget-status"
          >
            {delta === 0
              ? "Exactly 100 — ready to lock in."
              : delta > 0
                ? `Over by ${delta}. Trim something.`
                : `Short by ${-delta}. Add more.`}
          </p>
        </div>
      </div>

      <section className="mt-8 space-y-5" data-testid="categories">
        {categories.map((c) => (
          <div
            key={c.key}
            className="border-t border-[var(--border)] pt-4"
            data-testid={`category-${c.key}`}
          >
            <div className="flex items-baseline justify-between">
              <div>
                <p className="display text-lg font-semibold">{c.name}</p>
                <p className="text-xs text-[var(--fg-soft)]">{c.description}</p>
              </div>
              <input
                type="number"
                min={0}
                max={100}
                value={points[c.key] ?? 0}
                onChange={(e) => adjust(c.key, Number(e.target.value))}
                data-testid={`points-${c.key}`}
                className="w-24 border border-[var(--border)] bg-[var(--bg-elev)] px-3 py-2 text-right text-lg"
              />
            </div>
            <input
              type="range"
              min={0}
              max={100}
              step={5}
              value={points[c.key] ?? 0}
              onChange={(e) => adjust(c.key, Number(e.target.value))}
              data-testid={`slider-${c.key}`}
              className="mt-2 w-full accent-[var(--accent)]"
            />
          </div>
        ))}
      </section>

      {error && (
        <p
          className="mt-6 border border-[var(--accent)] bg-[var(--bg-elev)] p-3 text-sm text-[var(--accent)]"
          data-testid="budget-error"
        >
          {error}
        </p>
      )}

      <div className="mt-10 flex items-center justify-between">
        <p className="text-xs text-[var(--muted)]">
          {delta === 0
            ? "Looks good. Lock in your budget to update your profile."
            : "Get to exactly 100 to submit."}
        </p>
        <Button
          onClick={submit}
          disabled={submitting || delta !== 0}
          data-testid="submit-button"
        >
          {submitting ? "Saving…" : "Lock in budget"}
        </Button>
      </div>
    </article>
  );
}
