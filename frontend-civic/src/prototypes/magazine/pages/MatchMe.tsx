import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { type CandidateMatches, type CandidateMatchItem, getCandidateMatches } from "@/api/campaign";
import { CandidateAvatar } from "../components/CandidateAvatar";
import { DisclaimerBadge } from "../components/DisclaimerBadge";

function MatchRow({ item }: { item: CandidateMatchItem }) {
  const pct = Math.round(item.score * 100);
  return (
    <li className="flex items-start gap-3 border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="match-row">
      <CandidateAvatar candidate={item.candidate} size={48} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <Link
            to={`/candidates/${item.candidate.slug}`}
            className="font-semibold text-[var(--fg)] hover:text-[var(--accent)]"
          >
            {item.candidate.name}
          </Link>
          <span className="text-sm text-[var(--muted)]">{item.candidate.party}</span>
          <span className="ml-auto text-sm font-semibold text-[var(--accent)]">{pct}% match</span>
        </div>
        <p className="mt-1 text-sm leading-relaxed text-[var(--fg-soft)]">{item.reason}</p>
      </div>
    </li>
  );
}

function Group({ title, blurb, items }: { title: string; blurb: string; items: CandidateMatchItem[] }) {
  if (items.length === 0) return null;
  return (
    <section className="mt-8">
      <h2 className="display text-2xl">{title}</h2>
      <p className="mt-1 text-sm text-[var(--muted)]">{blurb}</p>
      <ul className="mt-4 space-y-3">
        {items.map((m) => (
          <MatchRow key={m.candidate.id} item={m} />
        ))}
      </ul>
    </section>
  );
}

export default function MatchMe() {
  const [matches, setMatches] = useState<CandidateMatches | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    void getCandidateMatches()
      .then(setMatches)
      .finally(() => setLoaded(true));
  }, []);

  return (
    <div data-testid="match-page">
      <div className="flex items-center gap-2">
        <h1 className="display text-4xl">Match me with candidates</h1>
        <DisclaimerBadge />
      </div>
      <p className="mt-2 max-w-2xl text-sm text-[var(--muted)]">
        Based on your Civic Compass. We show the candidates who align with you — and, on purpose, the
        ones who'd challenge you. A good match isn't an echo.
      </p>

      {!loaded && (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Comparing values…
        </p>
      )}

      {loaded && matches && !matches.hasProfile && (
        <div className="mt-8 border border-[var(--border)] bg-[var(--bg-elev)] p-8 text-center" data-testid="no-profile">
          <p className="display text-2xl">Build your Civic Compass first.</p>
          <p className="mt-2 text-sm text-[var(--fg-soft)]">
            Answer ten quick questions and we'll match you against the field — no party labels.
          </p>
          <Link
            to="/onboarding"
            className="mt-5 inline-block rounded-full bg-[var(--accent)] px-6 py-3 text-sm font-semibold text-white"
          >
            Start the questions
          </Link>
        </div>
      )}

      {loaded && matches?.hasProfile && (
        <div data-testid="match-results">
          <Group
            title="Your closest matches"
            blurb="These candidates line up with your values most often."
            items={matches.topMatches}
          />
          <Group
            title="A productive challenge"
            blurb="Shares a value you hold strongly — but argues toward a different conclusion."
            items={matches.productiveChallenges}
          />
          <Group
            title="Surprising agreements"
            blurb="You'd expect to clash, yet you line up on a few specific things."
            items={matches.surprisingAgreements}
          />
        </div>
      )}
    </div>
  );
}
