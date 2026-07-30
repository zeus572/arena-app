import type { BillDetail } from "@/api/bills";

/**
 * The character the backend appends when it cuts a list teaser short — see
 * `BillMappings.Teaser`, which caps at 240 characters. Every bill in the corpus is
 * longer than that in practice, so a Shorts card that shows only the teaser is
 * showing a sentence that stops mid-thought.
 */
export const TEASER_ELLIPSIS = "…";

/** Whether this teaser is a cut-down of something longer. */
export function isTeaserTruncated(teaser: string | null | undefined): boolean {
  return !!teaser?.trimEnd().endsWith(TEASER_ELLIPSIS);
}

/**
 * The untruncated text a teaser was cut from.
 *
 * This mirrors the source preference in `BillMappings.Teaser` (neutral synthesis first,
 * raw source summary as the fallback) because the detail endpoint returns both fields
 * separately rather than the joined text. If that rule ever changes server-side, this has
 * to change with it — otherwise "See more" would expand into different prose than the
 * teaser it replaced.
 */
export function fullBillText(
  detail: Pick<BillDetail, "synthesisSummary" | "summary">,
): string {
  const synthesis = detail.synthesisSummary?.trim();
  return synthesis ? synthesis : detail.summary.trim();
}
