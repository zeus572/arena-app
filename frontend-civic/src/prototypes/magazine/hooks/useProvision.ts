import { useCallback, useEffect, useState } from "react";
import { getProvision, type ProvisionDetail } from "@/api/coalition";

/**
 * Shared loader for a single coalition provision. Both the Overview and the
 * Participate routes fetch independently but share fetch / mutate / busy state.
 */
export function useProvision(id: string) {
  const [d, setD] = useState<ProvisionDetail | null>(null);
  const [busy, setBusy] = useState(false);
  // Last mutation error, so callers can surface "that didn't go through" instead of
  // a failed write looking identical to a no-op (the button just re-enables).
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(() => {
    void getProvision(id).then(setD);
  }, [id]);

  useEffect(reload, [reload]);

  // Run a mutating API call that returns the fresh ProvisionDetail and swap it in.
  // Returns true on success, false on failure (with `error` set) so callers can gate
  // their success confirmation (overlay / banner) on the action actually landing.
  const run = useCallback(async (fn: () => Promise<ProvisionDetail>): Promise<boolean> => {
    setBusy(true);
    setError(null);
    try {
      setD(await fn());
      return true;
    } catch {
      // The civicApi interceptor already opens the verify-email modal on a 403
      // email_unverified; this inline message covers every other failure.
      setError("That didn't go through — please try again.");
      return false;
    } finally {
      setBusy(false);
    }
  }, []);

  const clearError = useCallback(() => setError(null), []);

  return { d, setD, reload, run, busy, error, clearError };
}
