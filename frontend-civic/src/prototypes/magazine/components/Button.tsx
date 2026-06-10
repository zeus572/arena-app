import { forwardRef } from "react";

/**
 * The one canonical button for the magazine UI. Before this, every page hand-wrote its own
 * `rounded-full bg-[var(--accent)] px-5 py-2 …` string, so padding drifted (7+ variants) and the
 * pills looked oversized against the 18px theme base. Sizes here are tuned to that base.
 *
 * Variants:
 *  - primary   filled accent (default call-to-action)
 *  - secondary accent outline
 *  - ghost     neutral outline (low-emphasis actions like Copy)
 *  - danger    neutral outline that reds on hover (Revoke / Leave)
 * Sizes: md (default) and sm (compact inline actions).
 */
export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
export type ButtonSize = "sm" | "md";

type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
  fullWidth?: boolean;
};

const base =
  "inline-flex items-center justify-center gap-1.5 rounded-full font-semibold transition disabled:opacity-50 disabled:pointer-events-none [&_svg]:h-3.5 [&_svg]:w-3.5";

const sizes: Record<ButtonSize, string> = {
  sm: "px-2 py-0.5 text-[11px]",
  md: "px-3 py-1 text-xs",
};

const variants: Record<ButtonVariant, string> = {
  primary: "bg-[var(--accent)] text-white hover:opacity-90",
  secondary: "border border-[var(--accent)] text-[var(--accent)] hover:bg-[var(--accent)]/5",
  ghost: "border border-[var(--border)] text-[var(--fg-soft)] hover:border-[var(--accent)]",
  danger: "border border-[var(--border)] text-[var(--fg-soft)] hover:border-red-400 hover:text-red-600",
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = "primary", size = "md", fullWidth = false, className = "", type = "button", ...rest },
  ref,
) {
  const classes = [base, sizes[size], variants[variant], fullWidth ? "w-full" : "", className]
    .filter(Boolean)
    .join(" ");
  return <button ref={ref} type={type} className={classes} {...rest} />;
});
