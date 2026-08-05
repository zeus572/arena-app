import { useEffect, useState } from "react";
import { getRoomActors, type RoomActor, type RoomActors } from "@/api/rooms";

const TIER_DEFINITION: Record<RoomActor["tier"], string> = {
  Decides: "Can make the decision happen, or stop it.",
  Shapes: "Can change the terms, but not the outcome.",
  Constrained: "Affected by it, with limited ability to move it.",
};

const TYPE_COLOR: Record<string, string> = {
  Committee: "var(--federal)",
  GovernmentBody: "var(--federal)",
  Agency: "var(--federal)",
  Court: "var(--federal)",
  ElectedOfficial: "var(--federal)",
  Country: "var(--state)",
  InternationalOrganization: "var(--state)",
  Military: "var(--state)",
};

/**
 * People & Power (design 1i).
 *
 * Actors are tiered by leverage over a NAMED decision, and the decision is selectable —
 * changing it re-sorts. Actors with no role for the chosen decision keep their default
 * row rather than disappearing, because a re-sort that silently drops actors would
 * misrepresent who is involved.
 *
 * The actor card answers five questions in a fixed order, and "says it wants" is always a
 * sourced quote or filing with a date. Never inferred motive — that rule is enforced by a
 * publish gate on the backend, and the UI shows the date so the reader can judge staleness.
 */
export function RoomActorMap({ slug, initial }: { slug: string; initial: RoomActors }) {
  const [actors, setActors] = useState<RoomActors>(initial);
  const [decision, setDecision] = useState<string>("");
  const [selected, setSelected] = useState<RoomActor | null>(null);

  useEffect(() => {
    let alive = true;
    if (!decision) {
      setActors(initial);
      return;
    }
    getRoomActors(slug, decision)
      .then((next) => alive && setActors(next))
      .catch(() => {
        /* Keep the current tiering rather than emptying the section. */
      });
    return () => {
      alive = false;
    };
  }, [slug, decision, initial]);

  const total =
    actors.decides.length + actors.shapes.length + actors.constrained.length;

  if (total === 0) return null;

  return (
    <section className="border-b border-[var(--border)] py-8" data-testid="room-actors">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-[11px] uppercase tracking-[0.2em] text-[var(--accent)]">
            People &amp; power
          </p>
          <h2 className="mt-2 text-[28px] md:text-[40px]">Who can act</h2>
        </div>

        {actors.availableDecisions.length > 0 && (
          <label className="flex flex-col gap-1 text-[12px] text-[var(--muted)]">
            Sorting by leverage over
            <select
              className="border border-[var(--border)] bg-[var(--bg-elev)] px-3 py-2 text-[13px] text-[var(--fg)]"
              value={decision}
              onChange={(e) => setDecision(e.target.value)}
              data-testid="actor-decision-select"
            >
              <option value="">This room overall</option>
              {actors.availableDecisions.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      <div className="mt-6 flex flex-col">
        {(["Decides", "Shapes", "Constrained"] as const).map((tier) => {
          const rows =
            tier === "Decides"
              ? actors.decides
              : tier === "Shapes"
                ? actors.shapes
                : actors.constrained;
          if (rows.length === 0) return null;

          return (
            <div
              key={tier}
              className="flex flex-col gap-4 border-t border-[var(--border)] py-5 md:flex-row md:gap-6"
              data-testid="actor-tier"
              data-tier={tier}
            >
              <div
                className={[
                  "w-full flex-none p-4 md:w-[132px]",
                  tier === "Decides"
                    ? "bg-[var(--fg)] text-[var(--bg)]"
                    : "bg-[var(--bg-inset)]",
                ].join(" ")}
              >
                <p className="text-[13px] font-bold uppercase tracking-[0.16em]">{tier}</p>
                <p className="mt-2 text-[12px] opacity-80">{TIER_DEFINITION[tier]}</p>
              </div>

              <div className="grid flex-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {rows.map((a) => (
                  <button
                    key={a.id}
                    type="button"
                    onClick={() => setSelected(selected?.id === a.id ? null : a)}
                    className="border border-[var(--border)] bg-[var(--bg-elev)] p-4 text-left"
                    data-testid="actor-card"
                  >
                    <span className="flex items-center gap-2">
                      <span
                        aria-hidden
                        className="inline-block h-[9px] w-[9px]"
                        style={{ background: TYPE_COLOR[a.actorType] ?? "var(--muted)" }}
                      />
                      <span className="text-[10px] uppercase tracking-[0.2em] text-[var(--muted)]">
                        {a.actorType}
                      </span>
                    </span>
                    <span className="mt-2 block text-[16px] font-semibold">{a.name}</span>
                    <span className="mt-1 block text-[13px] text-[var(--fg-soft)]">
                      {a.leverageStatement}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {selected && (
        <div
          className="mt-6 border-l-2 border-[var(--fg)] bg-[var(--bg-inset)] p-5"
          data-testid="actor-detail"
        >
          <h3 className="text-[19px]">{selected.name}</h3>
          <dl className="mt-4 flex flex-col">
            <Row term="Role here" value={selected.roleHere} />
            <Row term="Actual power" value={selected.actualPower} />
            <Row
              term="Says it wants"
              value={
                selected.statedWants
                  ? `${selected.statedWants}${
                      selected.statedWantsAsOf
                        ? ` (${new Date(selected.statedWantsAsOf).toLocaleDateString()})`
                        : ""
                    }`
                  : "Nothing on the record."
              }
            />
            <Row term="Constrained by" value={selected.constrainedBy} />
            <Row
              term="Appears in"
              value={`${selected.appearanceCount} place${selected.appearanceCount === 1 ? "" : "s"} in this graph`}
            />
          </dl>
          <p className="mt-4 text-[12px] text-[var(--muted)]">
            "Says it wants" is always a quote or a filing, with a date. We never infer what
            an actor wants from what it does.
          </p>
        </div>
      )}
    </section>
  );
}

function Row({ term, value }: { term: string; value: string }) {
  return (
    <div className="flex flex-col gap-1 border-t border-[var(--border)] py-3 md:flex-row md:gap-6">
      <dt className="w-[140px] flex-none text-[11px] uppercase tracking-[0.16em] text-[var(--muted)]">
        {term}
      </dt>
      <dd className="text-[15px]">{value || "—"}</dd>
    </div>
  );
}
