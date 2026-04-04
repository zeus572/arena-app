export type AgentColor = "libertarian" | "progressive" | "green" | "conservative" | "citizen" | "wildcard" | "commentator" | "celebrity" | "historical";

export function getAgentColor(persona: string, agentType?: string | null): AgentColor {
  // Agent type based detection
  if (agentType === "celebrity") return "celebrity";
  if (agentType === "historical") return "historical";

  const lower = persona.toLowerCase();
  // Commentator agents
  if (lower.includes("commentator") || lower.includes("commentary booth"))
    return "commentator";
  // Wildcard agents
  if (lower.includes("satirist") || lower.includes("comedian") || lower.includes("chaos agent") || lower.includes("wildcard") || lower.includes("crashed this debate"))
    return "wildcard";
  // Celebrity/historical detection from persona keywords
  if (lower.includes("president of the united states") || lower.includes("senator from vermont") || lower.includes("u.s. representative") || lower.includes("governor of florida") || lower.includes("ambassador"))
    return "celebrity";
  if (lower.includes("declaration of independence") || lower.includes("continental army") || lower.includes("constitutional convention") || lower.includes("gettysburg") || lower.includes("civil rights leader") || lower.includes("trust-busting") || lower.includes("new deal") || lower.includes("federalist papers"))
    return "historical";
  // Everyday citizen agents
  if (lower.includes("everyday") || lower.includes("working citizen") || lower.includes("lived experience") || lower.includes("single mom") || lower.includes("blue-collar") || lower.includes("small business owner") || lower.includes("retired veteran") || lower.includes("first-gen immigrant"))
    return "citizen";
  // Ideological agents
  if (lower.includes("libertarian") || lower.includes("fiscal hawk"))
    return "libertarian";
  if (lower.includes("ecological") || lower.includes("green") || lower.includes("environmental"))
    return "green";
  if (lower.includes("progressive") || lower.includes("social democrat") || lower.includes("equity"))
    return "progressive";
  if (lower.includes("conservative") || lower.includes("traditionalist") || lower.includes("nationalist"))
    return "conservative";
  return "citizen";
}

export function getAgentLabel(persona: string, agentType?: string | null): string {
  if (agentType === "celebrity") return "Celebrity";
  if (agentType === "historical") return "Historical";

  const lower = persona.toLowerCase();
  if (lower.includes("commentator") || lower.includes("commentary booth"))
    return "Commentator";
  if (lower.includes("satirist") || lower.includes("comedian") || lower.includes("chaos agent") || lower.includes("wildcard") || lower.includes("crashed this debate"))
    return "Wildcard";
  if (lower.includes("president of the united states") || lower.includes("senator from vermont") || lower.includes("u.s. representative") || lower.includes("governor of florida") || lower.includes("ambassador"))
    return "Celebrity";
  if (lower.includes("declaration of independence") || lower.includes("continental army") || lower.includes("constitutional convention") || lower.includes("gettysburg") || lower.includes("civil rights leader") || lower.includes("trust-busting") || lower.includes("new deal") || lower.includes("federalist papers"))
    return "Historical";
  if (lower.includes("everyday") || lower.includes("working citizen") || lower.includes("lived experience") || lower.includes("single mom") || lower.includes("blue-collar") || lower.includes("small business owner") || lower.includes("retired veteran") || lower.includes("first-gen immigrant"))
    return "Citizen";
  if (lower.includes("libertarian")) return "Libertarian";
  if (lower.includes("ecological") || lower.includes("green")) return "Ecologist";
  if (lower.includes("social democrat")) return "Social Democrat";
  if (lower.includes("progressive")) return "Progressive";
  if (lower.includes("traditionalist") || lower.includes("conservative")) return "Conservative";
  return "Independent";
}

export const AVATAR_COLORS: Record<AgentColor, string> = {
  libertarian: "bg-libertarian text-white",
  progressive: "bg-progressive text-white",
  green: "bg-green text-white",
  conservative: "bg-conservative text-white",
  citizen: "bg-citizen text-white",
  wildcard: "bg-wildcard text-white",
  commentator: "bg-commentator text-white",
  celebrity: "bg-amber-600 text-white",
  historical: "bg-stone-600 text-white",
};

export const BUBBLE_BG: Record<AgentColor, string> = {
  libertarian: "bg-libertarian-bg",
  progressive: "bg-progressive-bg",
  green: "bg-green-bg",
  conservative: "bg-conservative-bg",
  citizen: "bg-citizen-bg",
  wildcard: "bg-wildcard-bg",
  commentator: "bg-commentator-bg",
  celebrity: "bg-amber-50 dark:bg-amber-950/30",
  historical: "bg-stone-50 dark:bg-stone-950/30",
};

export const TAG_COLORS: Record<AgentColor, string> = {
  libertarian: "bg-libertarian-tag text-libertarian",
  progressive: "bg-progressive-tag text-progressive",
  green: "bg-green-tag text-green",
  conservative: "bg-conservative-tag text-conservative",
  citizen: "bg-citizen-tag text-citizen",
  wildcard: "bg-wildcard-tag text-wildcard",
  commentator: "bg-commentator-tag text-commentator",
  celebrity: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400",
  historical: "bg-stone-100 text-stone-700 dark:bg-stone-900/40 dark:text-stone-400",
};

export const FORMAT_LABELS: Record<string, { label: string; color: string }> = {
  standard: { label: "STANDARD", color: "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400" },
  common_ground: { label: "COMMON GROUND", color: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400" },
  tweet: { label: "TWEET BATTLE", color: "bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-400" },
  rapid_fire: { label: "RAPID FIRE", color: "bg-orange-100 text-orange-700 dark:bg-orange-900/40 dark:text-orange-400" },
  longform: { label: "LONGFORM", color: "bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-400" },
  roast: { label: "ROAST BATTLE", color: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400" },
  town_hall: { label: "TOWN HALL", color: "bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-400" },
};
