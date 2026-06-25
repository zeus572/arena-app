import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { BudgetFact } from "@/api/budgetFacts";
import type { CivicCampaignSummary } from "@/api/campaignManager";
import { getQuizQuestions, type QuizQuestion } from "@/api/quiz";
import { getTaxStates } from "@/api/taxModel";
import type { StateProfile } from "@/taxModel/engine";
import { STATE_PROFILES } from "@/taxModel/engine/stateProfiles";
import { CountdownTimer } from "./CountdownTimer";
import { CampaignFeatureCard } from "./featureCards/CampaignFeatureCard";
import { BudgetFactFeatureCard } from "./featureCards/BudgetFactFeatureCard";
import { StateTaxFactCard } from "./featureCards/StateTaxFactCard";
import { QuizFeatureCard } from "./featureCards/QuizFeatureCard";

// How long each pair of cards stays before the conveyor advances by one.
const ROTATE_MS = 9000;

type FeatureCard = {
  key: string;
  /** `seed` lets content cards vary which item they show as the rotation advances. */
  render: (seed: number) => ReactNode;
};

type Props = {
  budgetFacts: BudgetFact[];
  featuredCampaign: CivicCampaignSummary | null;
};

/**
 * The two feature tiles at the top of the magazine home, turned into a rotating
 * conveyor. The pool always leads with the election countdown + Campaign Manager
 * (so the home looks identical on first paint, and the e2e countdown checks still
 * pass), then folds in "Did you know?" budget facts, an in-box civics quiz, and a
 * random-state tax fact as their data loads.
 *
 * Two consecutive cards are shown at a time. The window auto-advances by one every
 * ROTATE_MS, and pauses while the user hovers/focuses the region or has an
 * interactive card (the quiz) mid-answer. Arrows and dots drive it manually.
 */
export function FeatureRotator({ budgetFacts, featuredCampaign }: Props) {
  const [quizQuestions, setQuizQuestions] = useState<QuizQuestion[]>([]);
  const [taxStates, setTaxStates] = useState<StateProfile[]>([]);
  const [offset, setOffset] = useState(0);
  const [hovered, setHovered] = useState(false);
  const [locked, setLocked] = useState(false);

  useEffect(() => {
    void getQuizQuestions(8)
      .then(setQuizQuestions)
      .catch(() => {});
    // Fall back to the bundled state set so the tax fact still works offline.
    void getTaxStates()
      .then((s) => setTaxStates(s.length > 0 ? s : STATE_PROFILES))
      .catch(() => setTaxStates(STATE_PROFILES));
  }, []);

  const cards = useMemo<FeatureCard[]>(() => {
    const list: FeatureCard[] = [
      {
        key: "countdown",
        render: () => (
          <CountdownTimer scope="National" testId="countdown-national" className="h-full" />
        ),
      },
      {
        key: "campaign",
        render: () => <CampaignFeatureCard featuredCampaign={featuredCampaign} />,
      },
    ];
    if (budgetFacts.length > 0) {
      list.push({
        key: "budget",
        render: (seed) => <BudgetFactFeatureCard fact={budgetFacts[seed % budgetFacts.length]} />,
      });
    }
    if (quizQuestions.length > 0) {
      list.push({
        key: "quiz",
        render: () => <QuizFeatureCard questions={quizQuestions} onLockChange={setLocked} />,
      });
    }
    if (taxStates.length > 0) {
      list.push({
        key: "tax",
        // Step by a prime so successive appearances surface different states.
        render: (seed) => <StateTaxFactCard state={taxStates[(seed * 7) % taxStates.length]} />,
      });
    }
    return list;
  }, [budgetFacts, featuredCampaign, quizQuestions, taxStates]);

  const n = cards.length;
  // With only the two anchor cards there's nothing to rotate to; render them static.
  const canRotate = n > 2;

  useEffect(() => {
    if (!canRotate) return;
    const id = window.setInterval(() => {
      if (!hovered && !locked) setOffset((o) => o + 1);
    }, ROTATE_MS);
    return () => window.clearInterval(id);
  }, [canRotate, hovered, locked]);

  // Read the window with modulo so a shrinking/growing pool never goes out of range.
  const leftIdx = ((offset % n) + n) % n;
  const rightIdx = (leftIdx + 1) % n;
  const left = cards[leftIdx];
  const right = cards[rightIdx];

  const jumpTo = (i: number) => {
    setLocked(false);
    setOffset(i);
  };
  const step = (delta: number) => {
    setLocked(false);
    setOffset((o) => o + delta);
  };

  const chevronClass =
    "flex h-7 w-7 items-center justify-center rounded-full border border-[var(--border)] text-[var(--fg-soft)] transition hover:border-[var(--accent)] hover:text-[var(--accent)]";

  return (
    <div
      className="my-10"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onFocusCapture={() => setHovered(true)}
      onBlurCapture={() => setHovered(false)}
      data-testid="feature-rotator"
    >
      <div className="grid items-stretch gap-4 md:grid-cols-2">
        <div key={`L-${left.key}`} className="feature-slot h-full">
          {left.render(offset)}
        </div>
        {n > 1 && (
          <div key={`R-${right.key}`} className="feature-slot h-full">
            {right.render(offset + 1)}
          </div>
        )}
      </div>

      {canRotate && (
        <div
          className="mt-4 flex items-center justify-center gap-3"
          data-testid="feature-rotator-controls"
        >
          <button type="button" onClick={() => step(-1)} aria-label="Previous features" className={chevronClass}>
            <ChevronLeft className="h-4 w-4" />
          </button>
          <div className="flex items-center gap-1.5">
            {cards.map((c, i) => {
              const active = i === leftIdx || i === rightIdx;
              return (
                <button
                  key={c.key}
                  type="button"
                  onClick={() => jumpTo(i)}
                  aria-label={`Show ${c.key} feature`}
                  aria-current={active}
                  className={
                    active
                      ? "h-1.5 w-5 rounded-full bg-[var(--accent)] transition-all"
                      : "h-1.5 w-1.5 rounded-full bg-[var(--border)] transition-all hover:bg-[var(--muted)]"
                  }
                />
              );
            })}
          </div>
          <button type="button" onClick={() => step(1)} aria-label="Next features" className={chevronClass}>
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      )}
    </div>
  );
}
