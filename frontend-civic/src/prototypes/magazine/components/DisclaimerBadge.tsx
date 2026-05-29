import { Bot } from "lucide-react";
import { cn } from "@/lib/cn";

/**
 * Trust signal shown on every Virtual Candidate appearance. These candidates
 * are fictional AI simulations — the badge must be present wherever a
 * candidate is rendered so no one mistakes them for real people.
 */
export function DisclaimerBadge({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full border border-indigo-300 bg-indigo-50 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-indigo-700",
        className,
      )}
      data-testid="fictional-disclaimer"
      title="This is an AI-generated fictional candidate, not a real person."
    >
      <Bot className="h-3 w-3" />
      AI simulation · Fictional
    </span>
  );
}
