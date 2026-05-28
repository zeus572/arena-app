import { cn } from "@/lib/cn";

export function ValueChip({
  label,
  selected = false,
  onClick,
}: {
  label: string;
  selected?: boolean;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "rounded-full border-2 px-5 py-2 text-base font-semibold transition",
        selected
          ? "border-[var(--accent)] bg-[var(--accent)] text-white"
          : "border-[var(--border)] bg-[var(--bg-elev)] text-[var(--fg)] hover:border-[var(--accent)]",
      )}
    >
      {label}
    </button>
  );
}
