import type { ClaimStatus } from "@/api/rooms";

/**
 * The evidence mark (design 1m) — a small square whose FILL PATTERN encodes one of the
 * eight claim statuses.
 *
 * The hard requirement is "never colour alone": every mark must be distinguishable in
 * greyscale, because a reader who cannot tell Disputed from Confirmed has lost the entire
 * point of the ledger. So the vocabulary is built from fill height, hatching, border style
 * and a slash — colour is a reinforcement, never the signal.
 *
 * Outside inline prose the mark is always accompanied by the status WORD. Inline it stands
 * alone visually but still announces itself to assistive technology.
 */

export type EvidenceMarkSize = "inline" | "default" | "large";

const SIZES: Record<EvidenceMarkSize, number> = {
  inline: 11,
  default: 14,
  large: 16,
};

/** The human-facing word. Never abbreviate these — they are the accessible label. */
export const STATUS_WORD: Record<ClaimStatus, string> = {
  Confirmed: "Confirmed",
  StronglySupported: "Strongly supported",
  PlausibleButUnresolved: "Plausible but unresolved",
  Disputed: "Disputed",
  Unsupported: "Unsupported",
  False: "False",
  Outdated: "Outdated",
  Prediction: "Prediction",
};

/** One line of plain language per status, from design 1m's table. */
export const STATUS_MEANING: Record<ClaimStatus, string> = {
  Confirmed: "Multiple independent sources, or a primary document.",
  StronglySupported: "Good evidence, no contradiction, not independently confirmed.",
  PlausibleButUnresolved: "Could be true; the evidence that would settle it does not exist yet.",
  Disputed: "Credible sources directly contradict each other.",
  Unsupported: "Circulating with no evidence behind it.",
  False: "Evidence shows it is not true.",
  Outdated: "Was accurate; something changed.",
  Prediction: "A statement about the future, not a fact.",
};

function markStyle(status: ClaimStatus, px: number): React.CSSProperties {
  const base: React.CSSProperties = {
    width: px,
    height: px,
    display: "inline-block",
    flex: "none",
    boxSizing: "border-box",
  };

  switch (status) {
    case "Confirmed":
      // Solid. The only fully-filled mark.
      return { ...base, background: "var(--fg)" };

    case "StronglySupported":
      // Filled to 75% from the bottom — reads as "nearly there" without colour.
      return {
        ...base,
        background: `linear-gradient(to top, var(--fg) 75%, var(--border) 75%)`,
      };

    case "PlausibleButUnresolved":
      // Under half full, muted, with a hairline so the empty part still reads as a mark.
      return {
        ...base,
        background: `linear-gradient(to top, oklch(60% .01 50) 45%, transparent 45%)`,
        border: "1px solid var(--muted)",
      };

    case "Disputed":
      // Split vertically. In greyscale the two halves differ in lightness, and the hard
      // centre edge is itself the signal.
      return {
        ...base,
        background: `linear-gradient(to right, var(--federal) 0 50%, var(--state) 50% 100%)`,
      };

    case "Unsupported":
      // 45-degree hatch — obviously "nothing behind it" with no colour at all.
      return {
        ...base,
        border: "1px solid var(--muted)",
        background:
          "repeating-linear-gradient(45deg, var(--border) 0 2px, transparent 2px 4px)",
      };

    case "False":
      // Outline with a single slash. The slash is the whole message.
      return {
        ...base,
        border: "1px solid var(--fg)",
        background:
          "linear-gradient(to top left, transparent calc(50% - 0.5px), var(--fg) calc(50% - 0.5px), var(--fg) calc(50% + 0.5px), transparent calc(50% + 0.5px))",
      };

    case "Outdated":
      // Dashed border: the shape is provisional.
      return {
        ...base,
        border: "1px dashed var(--muted)",
        background: "oklch(96% .005 50)",
      };

    case "Prediction":
      // Dotted border — distinct from Outdated's dashes at these sizes.
      return {
        ...base,
        border: "1px dotted var(--accent)",
        background: "oklch(95% .03 40)",
      };
  }
}

export interface EvidenceMarkProps {
  status: ClaimStatus;
  size?: EvidenceMarkSize;
  /**
   * Render the status word beside the mark. Design 1m requires the word in every
   * non-inline context, so this defaults to on and must be switched off deliberately.
   */
  withWord?: boolean;
  /** Extra context for screen readers, e.g. "four assessments". */
  detail?: string;
  className?: string;
}

export function EvidenceMark({
  status,
  size = "default",
  withWord = true,
  detail,
  className,
}: EvidenceMarkProps) {
  const px = SIZES[size];
  const word = STATUS_WORD[status];

  // "disputed claim, four assessments — open evidence" is the announcement design 1m asks
  // for. The mark is focusable so a keyboard user can reach the explanation.
  const label = detail ? `${word} claim, ${detail}` : `${word} claim`;

  return (
    <span
      className={["inline-flex items-center gap-2 align-middle", className]
        .filter(Boolean)
        .join(" ")}
      data-testid="evidence-mark"
      data-status={status}
    >
      <span
        style={markStyle(status, px)}
        role="img"
        aria-label={label}
        title={`${word} — ${STATUS_MEANING[status]}`}
        tabIndex={0}
      />
      {withWord && (
        <span
          className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]"
          data-testid="evidence-mark-word"
        >
          {word}
        </span>
      )}
    </span>
  );
}

/**
 * The full eight-status key (design 1m). Rendered on the Sources & Methodology section so
 * the vocabulary is explained somewhere the reader can find it.
 */
export function EvidenceMarkLegend() {
  const all = Object.keys(STATUS_WORD) as ClaimStatus[];

  return (
    <ul className="flex flex-col gap-3" data-testid="evidence-legend">
      {all.map((status) => (
        <li key={status} className="flex items-start gap-3">
          <span className="mt-[3px]">
            <EvidenceMark status={status} withWord={false} size="large" />
          </span>
          <span className="text-[14px] leading-snug">
            <span className="font-semibold">{STATUS_WORD[status]}</span>
            <span className="text-[var(--fg-soft)]"> — {STATUS_MEANING[status]}</span>
          </span>
        </li>
      ))}
    </ul>
  );
}
