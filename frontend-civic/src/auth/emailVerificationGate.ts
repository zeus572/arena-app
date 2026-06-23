/**
 * Tiny pub/sub bridging the axios layer to React. The civic API response
 * interceptor calls {@link notifyEmailUnverified} whenever the backend rejects a
 * write with `403 { code: "email_unverified" }`; the app-root
 * <EmailVerificationGateModal> subscribes and shows a "verify your email" prompt.
 *
 * Kept framework-agnostic so `src/api/client.ts` doesn't have to import React.
 */
type Listener = () => void;

const listeners = new Set<Listener>();

export function subscribeEmailUnverified(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function notifyEmailUnverified(): void {
  listeners.forEach((listener) => listener());
}
