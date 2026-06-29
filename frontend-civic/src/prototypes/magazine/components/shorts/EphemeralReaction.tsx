import { useState } from "react";
import { Check } from "lucide-react";
import { cn } from "@/lib/cn";

export type EphemeralOption = { key: string; label: string };

/**
 * A lightweight, one-tap "quick opinion" for cards that have no server-side
 * opinion endpoint (think-deeper questions, budget facts). The pick lives only in
 * local component state — there's deliberately no fabricated tally and no network
 * call; it's a fast, honest gut-check that complements the "go deeper" link.
 */
export function EphemeralReaction({
  prompt,
  options,
  testId,
}: {
  prompt: string;
  options: [EphemeralOption, EphemeralOption];
  testId?: string;
}) {
  const [picked, setPicked] = useState<string | null>(null);

  return (
    <div data-testid={testId}>
      <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
        {picked ? "Noted — just for you" : prompt}
      </p>
      <div className="mt-2 grid grid-cols-2 gap-2">
        {options.map((opt) => {
          const active = picked === opt.key;
          return (
            <button
              key={opt.key}
              type="button"
              onClick={() => setPicked(opt.key)}
              aria-pressed={active}
              data-testid={`${testId}-${opt.key}`}
              className={cn(
                "inline-flex items-center justify-center gap-1.5 rounded-full border px-4 py-3 text-sm font-semibold transition",
                active
                  ? "border-[var(--accent)] bg-[var(--accent)] text-white"
                  : "border-[var(--border)] text-[var(--fg-soft)] hover:border-[var(--accent)]",
                picked && !active && "opacity-50",
              )}
            >
              {active && <Check className="h-4 w-4" />}
              {opt.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
