import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button, ButtonLink } from "../components/Button";
import { getMyProfile, type Profile, type AxisScore } from "@/api/profile";
import { buildReceipt } from "@/api/receipts";

function AxisBar({ axis }: { axis: AxisScore }) {
  // Score is in [-1, 1]. Map to left percentage of the bar's marker:
  // -1 → 0%, 0 → 50%, +1 → 100%
  const markerLeft = `${50 + axis.score * 50}%`;
  const hasData = axis.supportingAnswerCount > 0;

  return (
    <div
      className="border-t border-[var(--border)] pt-5"
      data-testid={`axis-${axis.axisKey}`}
      data-score={axis.score.toFixed(3)}
      data-supporting={axis.supportingAnswerCount}
    >
      <div className="flex items-baseline justify-between">
        <p className="display text-lg font-semibold">{axis.axisName}</p>
        <p className="text-xs uppercase tracking-wider text-[var(--muted)]">
          {hasData
            ? `${axis.supportingAnswerCount} answer${axis.supportingAnswerCount === 1 ? "" : "s"}`
            : "No data yet"}
        </p>
      </div>
      <div className="mt-3 flex items-center justify-between text-xs text-[var(--muted)]">
        <span>{axis.lowLabel}</span>
        <span>{axis.highLabel}</span>
      </div>
      <div className="relative mt-2 h-2 w-full bg-[var(--bg-elev)]">
        <div className="absolute left-1/2 top-0 h-full w-px bg-[var(--border)]" />
        {hasData && (
          <div
            className="absolute top-1/2 h-4 w-4 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white bg-[var(--accent)]"
            style={{ left: markerLeft }}
            data-testid={`axis-marker-${axis.axisKey}`}
          />
        )}
      </div>
    </div>
  );
}

export default function MagazineProfile() {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [buildingReceipt, setBuildingReceipt] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    void getMyProfile()
      .then(setProfile)
      .finally(() => setLoaded(true));
  }, []);

  async function generateReceipt() {
    setBuildingReceipt(true);
    try {
      const r = await buildReceipt();
      navigate(`/receipt/${r.id}`);
    } finally {
      setBuildingReceipt(false);
    }
  }

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading your profile…
      </p>
    );
  }

  if (!profile) {
    return (
      <p className="py-12 text-base text-[var(--muted)]">
        Your profile is not available right now.
      </p>
    );
  }

  const hasAnswers = profile.answerCount > 0;
  const top = profile.archetypeBlend[0];

  return (
    <article className="mx-auto max-w-3xl" data-testid="profile">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mt-8 text-center">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Your Civic Compass
        </p>
        <h1 className="display mt-3 text-5xl">
          {hasAnswers ? "This is what you've told us so far." : "Start the questions to build your profile."}
        </h1>
        <p className="mt-4 text-sm text-[var(--fg-soft)]">
          {hasAnswers
            ? `Based on ${profile.answerCount} answer${profile.answerCount === 1 ? "" : "s"}. Your profile will keep evolving.`
            : "No party labels. Just choices."}
        </p>
        {!hasAnswers && (
          <div className="mt-6">
            <ButtonLink
              to="/onboarding"
              data-testid="start-onboarding-link"
            >
              Start the questions
            </ButtonLink>
          </div>
        )}
        <p className="mt-6 text-xs text-[var(--muted)]">
          Looking for your name, email, or locality?{" "}
          <Link
            to="/settings"
            className="font-semibold text-[var(--accent)] underline"
            data-testid="compass-to-settings"
          >
            Profile &amp; settings
          </Link>
        </p>
      </header>

      {hasAnswers && top && (
        <section
          className="mt-12 border border-[var(--border)] bg-[var(--bg-elev)] p-8"
          data-testid="top-archetype"
        >
          <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
            Your strongest tendency
          </p>
          <p
            className="display mt-2 text-3xl"
            data-testid="top-archetype-name"
          >
            {top.name}
          </p>
          <p
            className="mt-2 text-sm uppercase tracking-wider text-[var(--accent)]"
            data-testid="top-archetype-percent"
          >
            {Math.round(top.percent)}% match
          </p>
          <p className="mt-4 text-base leading-relaxed text-[var(--fg-soft)]">
            {top.description}
          </p>
          <p className="mt-4 text-xs italic text-[var(--muted)]">
            Archetypes are tendencies, not labels. This will shift as you keep
            answering.
          </p>
        </section>
      )}

      <section className="mt-14">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Your axes
        </p>
        <h2 className="display mt-2 text-3xl">Where you sit on each axis</h2>
        <div className="mt-8 space-y-6">
          {profile.axes.map((a) => (
            <AxisBar key={a.axisKey} axis={a} />
          ))}
        </div>
      </section>

      {hasAnswers && profile.archetypeBlend.length > 1 && (
        <section className="mt-14">
          <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
            Other tendencies in the mix
          </p>
          <ul
            className="mt-4 space-y-2"
            data-testid="archetype-blend-list"
          >
            {profile.archetypeBlend.slice(1, 5).map((b) => (
              <li
                key={b.archetypeKey}
                className="flex items-baseline justify-between border-b border-[var(--border)] py-2"
                data-testid={`archetype-${b.archetypeKey}`}
              >
                <span className="display text-lg">{b.name}</span>
                <span className="text-xs uppercase tracking-wider text-[var(--muted)]">
                  {Math.round(b.percent)}%
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {hasAnswers && (
        <section className="mt-16 text-center">
          <p className="text-sm text-[var(--fg-soft)]">
            Want a plain-English summary of what we learned today?
          </p>
          <Button
            onClick={generateReceipt}
            disabled={buildingReceipt}
            data-testid="generate-receipt"
            className="mt-3"
          >
            {buildingReceipt ? "Building…" : "Generate Values Receipt"}
          </Button>
        </section>
      )}
    </article>
  );
}
