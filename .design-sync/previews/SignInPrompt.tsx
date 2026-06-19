import { SignInPrompt } from "frontend-civic";

// Canonical: the full sign-in wall shown when a signed-out visitor hits an
// action that needs an account (e.g. starting a campaign).
export const Default = () => (
  <div style={{ maxWidth: 420 }}>
    <SignInPrompt
      title="Sign in to manage one of these candidates"
      message="Pick a candidate, run their campaign week by week, respond to the news, and try to win the race. Your progress saves to your account."
    />
  </div>
);

// Compact variant — used inline on the Participate page beneath the answers.
export const Compact = () => (
  <div style={{ maxWidth: 420 }}>
    <SignInPrompt
      compact
      title="Sign in to save your position"
      message="Pick your answers above — then sign in to save a version and co-sign bills."
    />
  </div>
);

// Full vs compact side by side so the padding/type-scale axis is visible.
export const SizeComparison = () => (
  <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 420 }}>
    <SignInPrompt
      title="Sign in to start a league"
      message="Leagues need an account so your friends can find you on the leaderboard. Create one — it's free."
    />
    <SignInPrompt compact title="Sign in to view this league" />
  </div>
);
