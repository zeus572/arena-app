import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Handshake, Clock } from "lucide-react";
import { listProvisions, type ProvisionSummary } from "@/api/coalition";

// A provision is "open" until its coalition resolves (passed / forked / died);
// only open bills can still be weighed in on, so the enticer only ever shows those.
const CLOSED_STATES = ["Passed", "Forked", "Died"];

function deadlineLabel(deadline: string | null): { text: string; urgent: boolean } | null {
  if (!deadline) return null;
  const ms = new Date(deadline).getTime() - Date.now();
  if (Number.isNaN(ms) || ms <= 0) return null;
  const hours = ms / 3_600_000;
  if (hours < 24) return { text: `Closes in ${Math.max(1, Math.ceil(hours))}h`, urgent: true };
  const days = Math.ceil(hours / 24);
  return { text: `Closes in ${days} day${days === 1 ? "" : "s"}`, urgent: days <= 2 };
}

/* An enticer toward an open coalition bill, replacing the old static "Concept of the
   day" section. It picks one live, still-decidable provision — preferring the one
   closing soonest, then the widest gap to bridge — and frames it as a question the
   reader can go weigh in on. Renders nothing if no open bills exist. */
export function CoalitionQuestionCard() {
  const [provision, setProvision] = useState<ProvisionSummary | null>(null);

  useEffect(() => {
    void listProvisions()
      .then((all) => {
        const open = all.filter((p) => !CLOSED_STATES.includes(p.state));
        if (open.length === 0) {
          setProvision(null);
          return;
        }
        // Soonest deadline first (most urgent), then widest gap to bridge.
        const pick = [...open].sort((a, b) => {
          const da = a.deadline ? new Date(a.deadline).getTime() : Infinity;
          const db = b.deadline ? new Date(b.deadline).getTime() : Infinity;
          if (da !== db) return da - db;
          return b.gapWidth - a.gapWidth;
        })[0];
        setProvision(pick);
      })
      .catch(() => {});
  }, []);

  if (!provision) return null;

  const deadline = deadlineLabel(provision.deadline);

  return (
    <section className="mt-16" data-testid="coalition-question">
      <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
        A question splitting the room
      </p>
      <Link
        to={`/coalition/${provision.id}`}
        className="group mt-4 block border border-[var(--accent)] bg-[var(--accent)]/5 p-8 transition hover:bg-[var(--accent)]/10"
        data-testid="coalition-question-link"
      >
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          <Handshake className="h-4 w-4" /> Coalitions · Open bill
        </p>
        <h2 className="display mt-2 text-3xl text-[var(--fg)] group-hover:text-[var(--accent)]">
          {provision.title}
        </h2>
        <div className="mt-4 flex flex-wrap items-center gap-2 text-xs">
          <span className="rounded-full bg-[var(--accent)]/10 px-2 py-0.5 font-semibold uppercase tracking-wider text-[var(--accent)]">
            {provision.difficulty}
          </span>
          <span className="text-[var(--muted)]">
            {provision.governance ? "governance" : "culture"}
          </span>
          <span className="text-[var(--muted)]">
            breadth {provision.coveredBuckets}/{provision.totalBuckets}
          </span>
          {deadline && (
            <span
              className={`flex items-center gap-1 font-semibold ${deadline.urgent ? "text-rose-600" : "text-[var(--muted)]"}`}
            >
              <Clock className="h-3 w-3" /> {deadline.text}
            </span>
          )}
        </div>
        <p className="mt-4 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] group-hover:text-[var(--accent)]">
          Take a position and bridge the spectrum →
        </p>
      </Link>
    </section>
  );
}
