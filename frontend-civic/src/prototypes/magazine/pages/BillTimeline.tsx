import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Check, Circle, CircleDashed } from "lucide-react";
import { getBillTimeline, type BillTimelineStep } from "@/api/billTimeline";

function StepIcon({ status }: { status: BillTimelineStep["status"] }) {
  if (status === "Done") return <Check className="h-4 w-4 text-emerald-600" />;
  if (status === "Current") return <Circle className="h-4 w-4 text-[var(--accent)]" />;
  return <CircleDashed className="h-4 w-4 text-[var(--muted)]" />;
}

export default function MagazineBillTimeline() {
  const [steps, setSteps] = useState<BillTimelineStep[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    void getBillTimeline()
      .then(setSteps)
      .finally(() => setLoaded(true));
  }, []);

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="timeline-loading">
        Loading the timeline…
      </p>
    );
  }

  if (steps.length === 0) {
    return (
      <p className="py-12 text-base text-[var(--muted)]" data-testid="timeline-empty">
        The bill timeline isn't published yet.
      </p>
    );
  }

  const current = steps.find((s) => s.status === "Current");
  const nextAfterCurrent = current
    ? steps[steps.findIndex((s) => s.id === current.id) + 1]
    : undefined;

  return (
    <article data-testid="magazine-bill-timeline">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to the issue
      </Link>

      <header className="mt-6">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Process diagram
        </p>
        <h1 className="display mt-2 text-5xl leading-tight">
          How a bill becomes law
        </h1>
        <p className="mt-4 text-lg leading-relaxed text-[var(--fg-soft)]">
          Follow a single bill — the Student Data Privacy Modernization Act —
          through the major steps. Most bills don't make it past committee.
          Here's where this one is.
        </p>
      </header>

      <section className="mt-10">
        <ol
          className="relative space-y-6 border-l-2 border-[var(--border)] pl-8"
          data-testid="timeline-steps"
        >
          {steps.map((s) => {
            const isCurrent = s.status === "Current";
            return (
              <li
                key={s.id}
                className="relative"
                data-testid={`timeline-step-${s.externalId}`}
                data-status={s.status}
              >
                <span className="absolute -left-[2.4rem] flex h-7 w-7 items-center justify-center rounded-full border-2 border-[var(--border)] bg-[var(--bg)]">
                  <StepIcon status={s.status} />
                </span>
                <div
                  className={`border bg-[var(--bg-elev)] p-5 ${
                    isCurrent
                      ? "border-[var(--accent)]"
                      : "border-[var(--border)]"
                  }`}
                >
                  <div className="flex items-baseline justify-between gap-3">
                    <h3 className="display text-2xl">{s.label}</h3>
                    <span className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                      {s.branch}
                    </span>
                  </div>
                  <p className="mt-2 text-base leading-relaxed text-[var(--fg-soft)]">
                    {s.description}
                  </p>
                  {isCurrent && (
                    <p className="mt-3 text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
                      Where this bill is now
                    </p>
                  )}
                </div>
              </li>
            );
          })}
        </ol>
      </section>

      {current && (
        <section
          className="mt-10 border border-[var(--accent)] bg-[var(--bg-elev)] p-6"
          data-testid="timeline-whats-next"
        >
          <p className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
            What happens next
          </p>
          <p className="mt-2 text-lg font-semibold">
            After "{current.label}"
            {nextAfterCurrent ? `, the bill moves to ${nextAfterCurrent.label}.` : "."}
          </p>
          {nextAfterCurrent && (
            <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">
              {nextAfterCurrent.description}
            </p>
          )}
          <div className="mt-4">
            <Link
              to="/briefings/congress-student-data-privacy-bill"
              className="inline-block rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white"
            >
              See the briefing →
            </Link>
          </div>
        </section>
      )}

      <section className="mt-12 border-t border-[var(--border)] pt-6">
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          Why this matters
        </p>
        <p className="mt-3 text-base leading-relaxed">
          Most news stories talk about "the bill" without saying where it
          actually is in the process. Two bills with the same headline can be in
          very different places. This timeline is the same for every federal
          bill — once you know it, you can place any headline.
        </p>
      </section>
    </article>
  );
}
