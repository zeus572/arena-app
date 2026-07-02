import { Link } from "react-router-dom";
import type { BudgetFact } from "@/api/budgetFacts";
import { BudgetFactFeatureCard } from "../featureCards/BudgetFactFeatureCard";
import { EphemeralReaction } from "./EphemeralReaction";
import { ShortCardShell } from "./ShortCardShell";

/**
 * Full-viewport Shorts card for a "did you know?" budget fact. Reuses the existing
 * BudgetFactFeatureCard (two true-but-conflicting perspectives across a "BUT"
 * hinge) for the body, then adds an ephemeral gut-check and a deep-link into the
 * tax-apportionment explainer.
 */
export function BudgetFactShortCard({ fact }: { fact: BudgetFact }) {
  return (
    <ShortCardShell>
      <div className="my-4 flex flex-1 flex-col justify-center">
        <BudgetFactFeatureCard fact={fact} />
      </div>

      <div className="mt-4">
        <EphemeralReaction
          prompt="Did that surprise you?"
          options={[
            { key: "surprised", label: "Surprised" },
            { key: "knew", label: "Knew it" },
          ]}
          testId="short-budget-react"
        />
        <Link
          to="/briefings/who-gets-your-tax-dollar"
          className="mt-3 block text-right text-sm font-semibold text-[var(--accent)] hover:underline"
          data-testid="short-budget-open"
        >
          See where your taxes go →
        </Link>
      </div>
    </ShortCardShell>
  );
}
