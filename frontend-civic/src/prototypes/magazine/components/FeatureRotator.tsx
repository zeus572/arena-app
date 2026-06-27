import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ChevronLeft, ChevronRight, Pause, Play } from "lucide-react";
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

// How long each card stays before the conveyor advances by one.
const ROTATE_MS = 7000;

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
 * The feature tile at the top of the magazine home, a rotating conveyor. The pool
 * always leads with the election countdown (so the e2e countdown checks still pass
 * on first paint), then Campaign Manager, then folds in "Did you know?" budget
 * facts, an in-box civics quiz, and a random-state tax fact as their data loads.
 *
 * A single full-width card is shown at a time — wide enough that content-heavy
 * cards (budget facts, the quiz) get room to breathe instead of being squeezed
 * into a half-width column. The tile auto-advances by one every ROTATE_MS, and
 * pauses while the user hovers/focuses the region or has an interactive card (the
 * quiz) mid-answer. Arrows and dots drive it manually.
 */
export function FeatureRotator({ budgetFacts, featuredCampaign }: Props) {
  const [quizQuestions, setQuizQuestions] = useState<QuizQuestion[]>([]);
  const [taxStates, setTaxStates] = useState<StateProfile[]>([]);
  const [offset, setOffset] = useState(0);
  const [hovered, setHovered] = useState(false);
  const [locked, setLocked] = useState(false);
  // User-driven pause via the play/pause toggle, distinct from the transient
  // hover/quiz-answer pauses so it persists until the user resumes.
  const [paused, setPaused] = useState(false);

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
  // A single tile rotates as soon as there's more than one card to cycle through.
  const canRotate = n > 1;

  useEffect(() => {
    if (!canRotate) return;
    const id = window.setInterval(() => {
      if (!hovered && !locked && !paused) setOffset((o) => o + 1);
    }, ROTATE_MS);
    return () => window.clearInterval(id);
  }, [canRotate, hovered, locked, paused]);

  // Read the index with modulo so a shrinking/growing pool never goes out of range.
  const currentIdx = ((offset % n) + n) % n;
  const current = cards[currentIdx];

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
    <div className="my-10" data-testid="feature-rotator">
      {/* Hover/focus-pause is scoped to the card only — so interacting with the
          controls below (Play, Next, dots) never counts as "hovering" and never
          keeps a just-resumed rotation suppressed. */}
      <div
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
        onFocusCapture={() => setHovered(true)}
        onBlurCapture={() => setHovered(false)}
      >
        <div key={current.key} className="feature-slot">
          {current.render(offset)}
        </div>
      </div>

      {canRotate && (
        <div className="mt-4 flex flex-col items-center gap-2" data-testid="feature-rotator-controls">
          <div className="flex items-center justify-center gap-3">
            <button type="button" onClick={() => step(-1)} aria-label="Previous features" className={chevronClass}>
              <ChevronLeft className="h-4 w-4" />
            </button>
            <div className="flex items-center gap-1.5">
              {cards.map((c, i) => {
                const active = i === currentIdx;
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
          <button
            type="button"
            onClick={() => setPaused((p) => !p)}
            aria-label={paused ? "Resume auto-rotation" : "Pause auto-rotation"}
            aria-pressed={paused}
            className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[var(--fg-soft)] transition hover:text-[var(--accent)]"
            data-testid="feature-rotator-pause"
          >
            {paused ? <Play className="h-3.5 w-3.5" /> : <Pause className="h-3.5 w-3.5" />}
            {paused ? "Play" : "Pause"}
          </button>
        </div>
      )}
    </div>
  );
}
