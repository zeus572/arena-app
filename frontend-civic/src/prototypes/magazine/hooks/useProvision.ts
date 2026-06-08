import { useCallback, useEffect, useState } from "react";
import { getProvision, type ProvisionDetail } from "@/api/coalition";

/**
 * Shared loader for a single coalition provision. Both the Overview and the
 * Participate routes fetch independently but share fetch / mutate / busy state.
 */
export function useProvision(id: string) {
  const [d, setD] = useState<ProvisionDetail | null>(null);
  const [busy, setBusy] = useState(false);

  const reload = useCallback(() => {
    void getProvision(id).then(setD);
  }, [id]);

  useEffect(reload, [reload]);

  // Run a mutating API call that returns the fresh ProvisionDetail and swap it in.
  const run = useCallback(async (fn: () => Promise<ProvisionDetail>) => {
    setBusy(true);
    try {
      setD(await fn());
    } finally {
      setBusy(false);
    }
  }, []);

  return { d, setD, reload, run, busy };
}
