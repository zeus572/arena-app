// Entry barrel for /design-sync (referenced via --entry). Scopes the importable
// bundle to exactly the magazine design-system components — without this, the
// converter's synth mode would bundle every src file (pulling in pages, main.tsx
// → index.css → the unprocessed `@import 'tailwindcss'`, and app-only deps).
// Also re-exports the preview provider so cfg.provider can reach it. Not part of
// the app build (dot-prefixed; outside the app's tsconfig include globs).
export * from "./src/prototypes/magazine/components/Button";
export * from "./src/prototypes/magazine/components/ValueChip";
export * from "./src/prototypes/magazine/components/PullQuote";
export * from "./src/prototypes/magazine/components/DisclaimerBadge";
export * from "./src/prototypes/magazine/components/Term";
export * from "./src/prototypes/magazine/components/CandidateAvatar";
export * from "./src/prototypes/magazine/components/CountdownTimer";
export * from "./src/prototypes/magazine/components/CoverStory";
export * from "./src/prototypes/magazine/components/CampaignPostCard";
export * from "./src/prototypes/magazine/components/SharePreviewCard";
export * from "./src/prototypes/magazine/components/SignInPrompt";
export * from "./src/prototypes/magazine/components/BudgetFactCard";
export * from "./src/prototypes/magazine/components/NavDropdown";
export { default as Flyout } from "./src/prototypes/magazine/components/Flyout";
export * from "./src/prototypes/magazine/components/BottomTabs";
export * from "./src/prototypes/magazine/components/MobileMenu";
export * from "./src/prototypes/magazine/components/PlayReadToggle";
export * from "./src/prototypes/magazine/components/taxApportionment/CaveatGrid";
export * from "./src/prototypes/magazine/components/taxApportionment/HouseholdCalculator";
export * from "./src/prototypes/magazine/components/taxApportionment/PonderSection";
export * from "./src/prototypes/magazine/components/taxApportionment/ScalingTable";
export * from "./src/prototypes/magazine/components/taxApportionment/SplitBar";
export * from "./src/prototypes/magazine/components/taxApportionment/StateCard";
export * from "./src/prototypes/magazine/components/taxApportionment/StatePicker";
export { DesignProvider } from "./.ds-provider";
