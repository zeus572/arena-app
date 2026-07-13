/** Minimum age to create an account (COPPA). Mirrors the backend gate. */
export const MINIMUM_SIGNUP_AGE = 13;

/**
 * Full years old as of today for an ISO `yyyy-MM-dd` date of birth, or `null`
 * if the string isn't a valid, non-future date. Client-side convenience only —
 * the authoritative under-13 gate lives on the server (`AuthController.Register`).
 */
export function computeAge(dobIso: string): number | null {
  if (!dobIso) return null;
  const dob = new Date(`${dobIso}T00:00:00`);
  if (Number.isNaN(dob.getTime())) return null;
  const today = new Date();
  if (dob > today) return null;
  let age = today.getFullYear() - dob.getFullYear();
  const m = today.getMonth() - dob.getMonth();
  if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) age--;
  return age;
}
