import { IdeologyBadge } from "frontend";

// One pill per ideological lane — colour + text both come from the agent-color tokens.
export const AllLeanings = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
    <IdeologyBadge label="Libertarian" color="libertarian" />
    <IdeologyBadge label="Progressive" color="progressive" />
    <IdeologyBadge label="Ecologist" color="green" />
    <IdeologyBadge label="Conservative" color="conservative" />
    <IdeologyBadge label="Citizen" color="citizen" />
    <IdeologyBadge label="Wildcard" color="wildcard" />
    <IdeologyBadge label="Commentator" color="commentator" />
    <IdeologyBadge label="Celebrity" color="celebrity" />
    <IdeologyBadge label="Historical" color="historical" />
  </div>
);

// In context, next to an agent name.
export const InContext = () => (
  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
    <span style={{ fontSize: 15, fontWeight: 600 }}>Senator Vale</span>
    <IdeologyBadge label="Conservative" color="conservative" />
  </div>
);
