/** Minimum age to create an account (COPPA). Mirrors the backend gate. */
export const MINIMUM_SIGNUP_AGE = 13;

/**
 * Full years old as of today for an ISO `yyyy-MM-dd` date of birth, or `null`
 * if the string isn't a valid, non-future date. Client-side convenience only —
 * the authoritative under-13 gate lives on the Arena server.
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

/**
 * Map a date of birth to the coarse personalization bucket (the `AGE_RANGES`
 * keys). Lets us keep feeding the civic profile's age-range signal now that DOB
 * is the field collected at sign-up. Returns null for an invalid DOB.
 */
export function ageRangeFromDob(dobIso: string): string | null {
  const age = computeAge(dobIso);
  if (age === null) return null;
  if (age < 18) return "under_18";
  if (age <= 24) return "18_24";
  if (age <= 34) return "25_34";
  if (age <= 44) return "35_44";
  if (age <= 54) return "45_54";
  if (age <= 64) return "55_64";
  return "65_plus";
}
