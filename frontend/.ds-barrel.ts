// Scoped converter entry for design-sync (Debate Arena DS).
// Committed, dot-prefixed at frontend/ root → outside tsconfig.app's `include: ["src"]`.
// Re-exports exactly the design-system components (+ the preview provider) so the
// esbuild bundle stays scoped to the DS instead of pulling in main.tsx / index.css /
// `@import 'tailwindcss'` (which esbuild can't process) and the app pages.
export { Button } from "@/components/ui/button";
export { AgentAvatar } from "@/components/agent-avatar";
export { IdeologyBadge } from "@/components/ideology-badge";
export { ThemeToggle } from "@/components/theme-toggle";
export { BreakingTicker } from "@/components/BreakingTicker";
export { DidYouKnowCard } from "@/components/DidYouKnowCard";
export { RollingNumber } from "@/components/RollingNumber";
export { DebateBackdrop } from "@/components/debate-backdrop";
export { ForkDebateDialog } from "@/components/fork-debate-dialog";
export { MatchupIntro } from "@/components/matchup-intro";
export { Navbar } from "@/components/navbar";
export {
  LoveMeter,
  HotSeatHeader,
  LaughterMeter,
  TweetHeader,
  RapidFireBanner,
  LongformHeader,
  CommonGroundHeader,
  RoastStageHeader,
} from "@/components/format-layouts";
export { DesignProvider } from "./.ds-provider";
