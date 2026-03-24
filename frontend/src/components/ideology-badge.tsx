import { cn } from "@/lib/utils";
import { type AgentColor, TAG_COLORS } from "@/lib/agent-colors";

interface IdeologyBadgeProps {
  label: string;
  color: AgentColor;
}

export function IdeologyBadge({ label, color }: IdeologyBadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide",
        TAG_COLORS[color]
      )}
    >
      {label}
    </span>
  );
}
