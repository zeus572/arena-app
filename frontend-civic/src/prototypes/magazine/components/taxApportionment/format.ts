// Display helpers for the Tax Apportionment module. Pure formatting, no logic.

const usd0 = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

/** Whole-dollar currency, e.g. $13,449. */
export function usd(value: number): string {
  return usd0.format(Math.round(value));
}

/** Percent with one decimal, e.g. 21.1%. */
export function pct(fraction: number): string {
  return `${(fraction * 100).toFixed(1)}%`;
}

/** Percent with no decimals, e.g. 64%. */
export function pct0(fraction: number): string {
  return `${Math.round(fraction * 100)}%`;
}
