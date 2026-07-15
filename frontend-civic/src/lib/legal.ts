// Single place to fill in the entity-specific legal facts referenced by the
// Privacy Policy, Terms of Service, and EULA. These are intentionally
// placeholders — swap them for the real registered values before launch, and
// keep them in sync with the email footer config (Email:SenderIdentity /
// Email:SenderPostalAddress on the Arena backend).
//
// NOTE: This copy is a good-faith draft and has NOT been reviewed by a lawyer.
// Have counsel review before relying on it.

/** Registered operator name shown throughout the legal documents. */
export const LEGAL_ENTITY = "[LEGAL ENTITY NAME]";

/** Physical mailing address (CAN-SPAM / contact). */
export const LEGAL_ADDRESS = "[MAILING ADDRESS]";

/** Contact address for privacy / legal inquiries. */
export const LEGAL_CONTACT_EMAIL = "privacy@civersify.com";

/** Governing-law state for the ToS/EULA venue clause. */
export const GOVERNING_STATE = "Washington";

/** Human-readable effective date shown at the top of each document. */
export const LEGAL_EFFECTIVE_DATE = "July 13, 2026";

/** Minimum age to hold an account (COPPA). Mirrors the backend gate. */
export const MINIMUM_AGE = 13;
