import { useLocation } from "react-router-dom";
import { LogIn } from "lucide-react";
import { ButtonLink } from "./Button";

/**
 * A sign-in promotion card shown where a signed-out visitor hits an action that needs an account.
 * It links to /login (and /register) with a redirect back to the current page so the user lands
 * right where they left off after authenticating.
 */
export function SignInPrompt({
  title = "Sign in to play",
  message = "Create a free account to manage a campaign and save your progress.",
  compact = false,
}: {
  title?: string;
  message?: string;
  compact?: boolean;
}) {
  const location = useLocation();
  const redirect = encodeURIComponent(location.pathname + location.search);

  return (
    <div
      className={`border border-[var(--border)] bg-[var(--bg-elev)] ${compact ? "p-4" : "p-6"} text-center`}
      data-testid="sign-in-prompt"
    >
      <LogIn className="mx-auto h-6 w-6 text-[var(--accent)]" />
      <p className={`mt-2 font-semibold text-[var(--fg)] ${compact ? "text-base" : "text-lg"}`}>{title}</p>
      <p className="mt-1 text-sm text-[var(--fg-soft)]">{message}</p>
      <div className="mt-4 flex items-center justify-center gap-2">
        <ButtonLink
          to={`/login?redirect=${redirect}`}
          data-testid="sign-in-prompt-login"
        >
          Sign in
        </ButtonLink>
        <ButtonLink
          to={`/register?redirect=${redirect}`}
          data-testid="sign-in-prompt-register"
          variant="ghost"
        >
          Create account
        </ButtonLink>
      </div>
    </div>
  );
}
