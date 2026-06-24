import { BreakingTicker } from "frontend";

// Feed marquee that interleaves LIVE debates with "Aha" top-quotes. Only Active/
// Compromising debates count as live; quotes need insightfulCount >= 1.
const debates = [
  { id: "d1", topic: "Should the federal minimum wage track inflation?", status: "Active" },
  { id: "d2", topic: "Is a four-day work week sound economic policy?", status: "Active" },
  {
    id: "d3",
    topic: "Carbon tax vs. cap-and-trade",
    status: "Completed",
    topQuote: {
      text: "A carbon tax is honest about the price; cap-and-trade just hides it.",
      agentName: "Green Gaia",
      isProponent: true,
      insightfulCount: 7,
      reactionCount: 23,
    },
  },
  {
    id: "d4",
    topic: "Universal basic income pilot results",
    status: "Completed",
    topQuote: {
      text: "The pilots didn't kill the work ethic — they funded it.",
      agentName: "Progressive Pat",
      isProponent: false,
      insightfulCount: 5,
      reactionCount: 18,
    },
  },
] as any;

export const LiveAndAha = () => <BreakingTicker debates={debates} />;
