export type AgentColor = "libertarian" | "progressive" | "green" | "conservative" | "citizen" | "wildcard" | "commentator";

export function getAgentColor(persona: string): AgentColor {
  const lower = persona.toLowerCase();
  // Commentator agents
  if (lower.includes("commentator") || lower.includes("commentary booth"))
    return "commentator";
  // Wildcard agents
  if (lower.includes("satirist") || lower.includes("comedian") || lower.includes("chaos agent") || lower.includes("wildcard") || lower.includes("crashed this debate"))
    return "wildcard";
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
  return "citizen"; // fallback — better than assuming a political alignment
}

export function getAgentLabel(persona: string): string {
  const lower = persona.toLowerCase();
  if (lower.includes("commentator") || lower.includes("commentary booth"))
    return "Commentator";
  if (lower.includes("satirist") || lower.includes("comedian") || lower.includes("chaos agent") || lower.includes("wildcard") || lower.includes("crashed this debate"))
    return "Wildcard";
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
};

export const BUBBLE_BG: Record<AgentColor, string> = {
  libertarian: "bg-libertarian-bg",
  progressive: "bg-progressive-bg",
  green: "bg-green-bg",
  conservative: "bg-conservative-bg",
  citizen: "bg-citizen-bg",
  wildcard: "bg-wildcard-bg",
  commentator: "bg-commentator-bg",
};

export const TAG_COLORS: Record<AgentColor, string> = {
  libertarian: "bg-libertarian-tag text-libertarian",
  progressive: "bg-progressive-tag text-progressive",
  green: "bg-green-tag text-green",
  conservative: "bg-conservative-tag text-conservative",
  citizen: "bg-citizen-tag text-citizen",
  wildcard: "bg-wildcard-tag text-wildcard",
  commentator: "bg-commentator-tag text-commentator",
};
