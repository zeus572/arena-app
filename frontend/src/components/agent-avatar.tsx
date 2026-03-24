import { cn } from "@/lib/utils";
import { type AgentColor, AVATAR_COLORS } from "@/lib/agent-colors";

interface AgentAvatarProps {
  agent: { name: string; avatar?: string; color?: AgentColor };
  size?: "sm" | "md" | "lg" | "xl";
}

const SIZE_MAP = {
  sm: "h-7 w-7 text-[10px]",
  md: "h-9 w-9 text-xs",
  lg: "h-12 w-12 text-sm",
  xl: "h-16 w-16 text-base",
};

function getInitials(name: string): string {
  return name
    .split(" ")
    .map((w) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

export function AgentAvatar({ agent, size = "md" }: AgentAvatarProps) {
  return (
    <div
      className={cn(
        "shrink-0 rounded-full flex items-center justify-center font-bold",
        SIZE_MAP[size],
        AVATAR_COLORS[agent.color ?? "progressive"]
      )}
      aria-label={agent.name}
    >
      {agent.avatar || getInitials(agent.name)}
    </div>
  );
}
