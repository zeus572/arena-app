import { LoveMeter } from "frontend";

// Common Ground format: fill = agreement turns (60%) + positive reactions (40%).
const building = [
  { type: "Agreement", reactions: { like: 3, insightful: 2 } },
  { type: "Agreement", reactions: { like: 2 } },
  { type: "Argument", reactions: { insightful: 1 } },
] as any;

// Partway: a couple of agreements plus warm reactions.
export const Building = () => <LoveMeter turns={building} totalTurns={6} />;

// Full bar trips the "Common Ground Reached" banner.
const reached = [
  { type: "Agreement", reactions: { like: 2, insightful: 1 } },
  { type: "Agreement", reactions: { like: 2, insightful: 1 } },
  { type: "Agreement", reactions: { like: 2, insightful: 1 } },
  { type: "Agreement", reactions: { like: 2, insightful: 1 } },
  { type: "Agreement", reactions: { like: 2, insightful: 1 } },
] as any;

export const CommonGroundReached = () => <LoveMeter turns={reached} totalTurns={5} />;
