import { useEffect, useState } from "react";
import { getNextElection, type Election, type ElectionScope } from "@/api/elections";

type CountdownTimerProps = {
  scope: ElectionScope;
  region?: string;
  testId?: string;
};

type Parts = {
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
};

function diffParts(target: Date, now: Date): Parts {
  const ms = Math.max(0, target.getTime() - now.getTime());
  const totalSeconds = Math.floor(ms / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return { days, hours, minutes, seconds };
}

function pad(n: number): string {
  return n.toString().padStart(2, "0");
}

function dateLabel(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}

export function CountdownTimer({ scope, region, testId }: CountdownTimerProps) {
  const [election, setElection] = useState<Election | null | undefined>(undefined);
  const [now, setNow] = useState<Date>(() => new Date());

  useEffect(() => {
    let cancelled = false;
    void getNextElection({ scope, region }).then((e) => {
      if (!cancelled) setElection(e ?? null);
    });
    return () => {
      cancelled = true;
    };
  }, [scope, region]);

  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(id);
  }, []);

  if (election === undefined) {
    return (
      <section
        className="my-10 border border-[var(--border)] bg-[var(--bg-elev)] p-6"
        data-testid={testId ?? `countdown-${scope.toLowerCase()}`}
      >
        <p className="text-xs uppercase tracking-[0.3em] text-[var(--muted)]">
          Next {scope.toLowerCase()} election
        </p>
        <p className="mt-2 text-sm text-[var(--muted)]">Loading…</p>
      </section>
    );
  }

  if (election === null) {
    return (
      <section
        className="my-10 border border-[var(--border)] bg-[var(--bg-elev)] p-6"
        data-testid={testId ?? `countdown-${scope.toLowerCase()}`}
      >
        <p className="text-xs uppercase tracking-[0.3em] text-[var(--muted)]">
          Next {scope.toLowerCase()} election
        </p>
        <p className="mt-2 text-sm text-[var(--muted)]">
          No upcoming election on file.
        </p>
      </section>
    );
  }

  const target = new Date(election.scheduledAt);
  const { days, hours, minutes, seconds } = diffParts(target, now);

  return (
    <section
      className="my-10 border border-[var(--border)] bg-[var(--bg-elev)] p-6"
      data-testid={testId ?? `countdown-${scope.toLowerCase()}`}
    >
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Next {scope.toLowerCase()} election
      </p>
      <h2
        className="display mt-2 text-3xl"
        data-testid={`${testId ?? `countdown-${scope.toLowerCase()}`}-name`}
      >
        {election.name}
      </h2>
      <p className="mt-1 text-sm text-[var(--fg-soft)]">
        {dateLabel(election.scheduledAt)}
      </p>

      <dl
        className="mt-5 grid max-w-md grid-cols-4 gap-3 text-center"
        data-testid={`${testId ?? `countdown-${scope.toLowerCase()}`}-clock`}
      >
        <CountdownCell label="days" value={days} testId="cd-days" />
        <CountdownCell label="hrs" value={hours} testId="cd-hours" pad />
        <CountdownCell label="min" value={minutes} testId="cd-minutes" pad />
        <CountdownCell label="sec" value={seconds} testId="cd-seconds" pad />
      </dl>
    </section>
  );
}

function CountdownCell({
  label,
  value,
  testId,
  pad: padded,
}: {
  label: string;
  value: number;
  testId: string;
  pad?: boolean;
}) {
  return (
    <div className="rounded border border-[var(--border)] bg-[var(--bg)] py-3">
      <dt className="text-[10px] uppercase tracking-[0.2em] text-[var(--muted)]">
        {label}
      </dt>
      <dd
        className="display mt-1 text-2xl tabular-nums"
        data-testid={testId}
      >
        {padded ? pad(value) : value}
      </dd>
    </div>
  );
}
