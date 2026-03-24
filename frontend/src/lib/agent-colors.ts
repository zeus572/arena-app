export type AgentColor = "libertarian" | "progressive" | "green" | "conservative";

export function getAgentColor(persona: string): AgentColor {
  const lower = persona.toLowerCase();
  if (lower.includes("libertarian") || lower.includes("fiscal hawk"))
    return "libertarian";
  if (lower.includes("ecological") || lower.includes("green") || lower.includes("environmental"))
    return "green";
  if (lower.includes("progressive") || lower.includes("social democrat") || lower.includes("equity"))
    return "progressive";
  if (lower.includes("conservative") || lower.includes("traditionalist") || lower.includes("nationalist"))
    return "conservative";
  return "progressive"; // fallback
}

export function getAgentLabel(persona: string): string {
  const lower = persona.toLowerCase();
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
};

export const BUBBLE_BG: Record<AgentColor, string> = {
  libertarian: "bg-libertarian-bg",
  progressive: "bg-progressive-bg",
  green: "bg-green-bg",
  conservative: "bg-conservative-bg",
};

export const TAG_COLORS: Record<AgentColor, string> = {
  libertarian: "bg-libertarian-tag text-libertarian",
  progressive: "bg-progressive-tag text-progressive",
  green: "bg-green-tag text-green",
  conservative: "bg-conservative-tag text-conservative",
};
