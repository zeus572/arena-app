// Lightweight cookie/analytics consent. Persisted in localStorage so the choice
// survives reloads. Analytics code (e.g. Application Insights) can call
// hasAnalyticsConsent() before enabling non-essential cookies, and listen for
// the "civersify:consent" event to react when the user makes a choice.

export type ConsentChoice = "accepted" | "declined";

const KEY = "civersify-cookie-consent";
export const CONSENT_EVENT = "civersify:consent";

export function getConsent(): ConsentChoice | null {
  try {
    const v = localStorage.getItem(KEY);
    return v === "accepted" || v === "declined" ? v : null;
  } catch {
    return null;
  }
}

export function setConsent(choice: ConsentChoice): void {
  try {
    localStorage.setItem(KEY, choice);
    window.dispatchEvent(new CustomEvent<ConsentChoice>(CONSENT_EVENT, { detail: choice }));
  } catch {
    /* storage unavailable (private mode); treat as no persisted consent */
  }
}

/** True only when the user has explicitly accepted non-essential cookies. */
export function hasAnalyticsConsent(): boolean {
  return getConsent() === "accepted";
}
