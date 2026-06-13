import { Link } from "react-router-dom";

// The civic payoff (§7). Four questions, shipped verbatim; also seeded into the
// Values Profile as `issue_specific` items on the federalism/fiscal axes
// (backend-civic/Seed/questions.json, externalId q-tax-apportionment-*).
const PONDER_QUESTIONS = [
  "If Washington collects most of the money, should it also decide most of how it's spent — or send more back with no strings?",
  "A bridge in one state is funded by taxpayers in all fifty. When is that fair burden-sharing, and when is it one state's project on everyone's bill?",
  "Low earners often pay a larger share to state and local government. Does that change who should fund what?",
  "Would you trade a lower federal tax for a higher state one, keeping more decisions local — even if your state raises less overall?",
];

export function PonderSection() {
  return (
    <section
      className="-mx-4 bg-[var(--fg)] px-4 py-12 text-[var(--bg)] md:-mx-8 md:px-12 md:py-16"
      data-testid="tax-ponder"
    >
      <div className="mx-auto max-w-3xl">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--bg)]/60">
          Now the judgment
        </p>
        <h2 className="display mt-3 text-4xl text-[var(--bg)] md:text-5xl">
          The arithmetic is settled. The split is yours to argue.
        </h2>
        <ol className="mt-8 space-y-6">
          {PONDER_QUESTIONS.map((q, i) => (
            <li key={i} className="flex gap-4">
              <span className="display text-2xl text-[var(--state)]">{i + 1}</span>
              <p className="text-lg leading-relaxed text-[var(--bg)]/90">{q}</p>
            </li>
          ))}
        </ol>
        <Link
          to="/profile"
          className="mt-10 inline-block border border-[var(--bg)]/40 px-5 py-2.5 text-sm font-semibold uppercase tracking-wider text-[var(--bg)] transition hover:bg-[var(--bg)] hover:text-[var(--fg)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-[var(--state)]"
          data-testid="tax-ponder-profile-link"
        >
          See how this shapes your values profile →
        </Link>
      </div>
    </section>
  );
}
