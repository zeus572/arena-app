import { AgentAvatar } from "frontend";

// The four sizes (initials fall back from a missing image).
export const Sizes = () => (
  <div style={{ display: "flex", gap: 16, alignItems: "center" }}>
    <AgentAvatar agent={{ name: "Ada Liberty", color: "libertarian" }} size="sm" />
    <AgentAvatar agent={{ name: "Ada Liberty", color: "libertarian" }} size="md" />
    <AgentAvatar agent={{ name: "Ada Liberty", color: "libertarian" }} size="lg" />
    <AgentAvatar agent={{ name: "Ada Liberty", color: "libertarian" }} size="xl" />
  </div>
);

// One disc per ideological lane — the colour token encodes the agent's leaning.
export const Leanings = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
    <AgentAvatar agent={{ name: "Progressive Pat", color: "progressive" }} size="lg" />
    <AgentAvatar agent={{ name: "Green Gaia", color: "green" }} size="lg" />
    <AgentAvatar agent={{ name: "Connor Vale", color: "conservative" }} size="lg" />
    <AgentAvatar agent={{ name: "Citizen Joe", color: "citizen" }} size="lg" />
    <AgentAvatar agent={{ name: "Wild Card", color: "wildcard" }} size="lg" />
    <AgentAvatar agent={{ name: "Abe Lincoln", color: "historical" }} size="lg" />
  </div>
);

// An explicit emoji avatar overrides the initials.
export const EmojiAvatar = () => (
  <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
    <AgentAvatar agent={{ name: "Robo Pundit", avatar: "🤖", color: "commentator" }} size="lg" />
    <AgentAvatar agent={{ name: "Star Speaker", avatar: "⭐", color: "celebrity" }} size="lg" />
  </div>
);
