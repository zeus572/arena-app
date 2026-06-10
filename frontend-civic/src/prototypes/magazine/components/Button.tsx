import { forwardRef } from "react";
import { Link, type LinkProps } from "react-router-dom";

/**
 * The one canonical button for the magazine UI. Before this, every page hand-wrote its own
 * `rounded-full bg-[var(--accent)] px-5 py-2 …` string, so padding drifted (10+ variants) and the
 * pills looked oversized against the theme type. All button sizing lives here now — to resize every
 * button in the app, edit the `sizes` map below; nothing else.
 *
 * Variants:
 *  - primary    filled accent (default call-to-action)
 *  - secondary  accent outline
 *  - positive   filled emerald (affirmative actions like Co-sign / Accept)
 *  - ghost      neutral outline (low-emphasis actions like Copy)
 *  - danger     neutral outline that reds on hover (Revoke / Decline / Leave)
 *  - link       borderless text button (Cancel / inline toggles)
 * Sizes: md (default) and sm (compact inline actions). Pass `fullWidth` for block CTAs.
 *
 * Sizes are ~20% tighter than the app's historical default and tuned to the magazine type scale.
 */
export type ButtonVariant = "primary" | "secondary" | "positive" | "ghost" | "danger" | "link";
export type ButtonSize = "sm" | "md";

type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
  fullWidth?: boolean;
};

const base =
  "inline-flex items-center justify-center gap-1.5 rounded-full font-semibold transition disabled:opacity-50 disabled:pointer-events-none";

const sizes: Record<ButtonSize, string> = {
  sm: "px-2 py-0.5 text-[11px] [&_svg]:h-3 [&_svg]:w-3",
  md: "px-3.5 py-1 text-[13px] [&_svg]:h-3.5 [&_svg]:w-3.5",
};

const variants: Record<ButtonVariant, string> = {
  primary: "bg-[var(--accent)] text-white hover:opacity-90",
  secondary: "border border-[var(--accent)] text-[var(--accent)] hover:bg-[var(--accent)]/5",
  positive: "bg-emerald-600 text-white hover:opacity-90",
  ghost: "border border-[var(--border)] text-[var(--fg-soft)] hover:border-[var(--accent)]",
  danger: "border border-[var(--border)] text-[var(--fg-soft)] hover:border-red-400 hover:text-red-600",
  link: "text-[var(--muted)] hover:text-[var(--accent)]",
};

/** Build the canonical button class string. Use for elements that can't be a <Button>/<ButtonLink>. */
export function buttonClasses(opts: { variant?: ButtonVariant; size?: ButtonSize; fullWidth?: boolean; className?: string } = {}) {
  const { variant = "primary", size = "md", fullWidth = false, className = "" } = opts;
  return [base, sizes[size], variants[variant], fullWidth ? "w-full" : "", className].filter(Boolean).join(" ");
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = "primary", size = "md", fullWidth = false, className = "", type = "button", ...rest },
  ref,
) {
  return <button ref={ref} type={type} className={buttonClasses({ variant, size, fullWidth, className })} {...rest} />;
});

type ButtonLinkProps = LinkProps & {
  variant?: ButtonVariant;
  size?: ButtonSize;
  fullWidth?: boolean;
};

/** A react-router <Link> that looks exactly like a <Button>. For navigational CTAs. */
export function ButtonLink({ variant = "primary", size = "md", fullWidth = false, className = "", ...rest }: ButtonLinkProps) {
  return <Link className={buttonClasses({ variant, size, fullWidth, className })} {...rest} />;
}
