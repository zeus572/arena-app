import { useCallback, useEffect, useRef, useState } from "react";
import { getProvision, type ProvisionDetail } from "@/api/coalition";

/**
 * Shared loader for a single coalition provision. Both the Overview and the
 * Participate routes fetch independently but share fetch / mutate / busy state.
 *
 * `initial` seeds the state with a caller-supplied snapshot — e.g. the fresh detail
 * a just-completed co-sign returned, handed to the Overview via router navigation
 * state. When seeded we skip the mount refetch: that refetch can race the write and
 * return a pre-action (or anonymous-identity) snapshot that overwrites the correct
 * one — which is why a co-sign didn't show until a manual refresh.
 */
export function useProvision(id: string, initial?: ProvisionDetail | null) {
  const [d, setD] = useState<ProvisionDetail | null>(initial ?? null);
  const [busy, setBusy] = useState(false);
  // Last mutation error, so callers can surface "that didn't go through" instead of
  // a failed write looking identical to a no-op (the button just re-enables).
  const [error, setError] = useState<string | null>(null);
  const skipNextReload = useRef(initial != null);

  const reload = useCallback(() => {
    void getProvision(id).then(setD);
  }, [id]);

  useEffect(() => {
    // Trust the seed for the first render; refetch on every later id change.
    if (skipNextReload.current) {
      skipNextReload.current = false;
      return;
    }
    reload();
  }, [reload]);

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
