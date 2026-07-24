import { useEffect, useState } from "react";
import { ForbiddenError } from "@/api/admin";

export type AdminStatus = "loading" | "ok" | "forbidden" | "error";

/**
 * Fetch helper for admin pages: runs `fetchFn` once `enabled` is true, and maps a 403
 * (ForbiddenError) to the "forbidden" status so the page can show an "Admins only" state.
 * `fetchFn` is intentionally excluded from the effect deps (callers pass inline lambdas);
 * use `reload()` to refetch.
 */
export function useAdminData<T>(fetchFn: () => Promise<T>, enabled: boolean) {
  const [data, setData] = useState<T | null>(null);
  const [status, setStatus] = useState<AdminStatus>("loading");
  const [nonce, setNonce] = useState(0);

  useEffect(() => {
    if (!enabled) return;
    let live = true;
    setStatus("loading");
    fetchFn()
      .then((d) => {
        if (!live) return;
        setData(d);
        setStatus("ok");
      })
      .catch((err) => {
        if (!live) return;
        setStatus(err instanceof ForbiddenError ? "forbidden" : "error");
      });
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, nonce]);

  return { data, status, reload: () => setNonce((n) => n + 1) };
}

/** Renders the non-ok admin states (loading / forbidden / error). Render only when status !== "ok". */
export function AdminStates({ status, testid }: { status: AdminStatus; testid: string }) {
  if (status === "loading") {
    return <p className="py-10 text-sm text-[var(--muted)]" data-testid={`${testid}-loading`}>Loading…</p>;
  }
  if (status === "forbidden") {
    return (
      <div className="py-10" data-testid={`${testid}-forbidden`}>
        <h2 className="display text-2xl">Admins only</h2>
        <p className="mt-2 text-sm text-[var(--fg-soft)]">
          Your account isn’t on the admin allowlist for this dashboard.
        </p>
      </div>
    );
  }
  return <p className="py-10 text-sm text-[var(--muted)]" data-testid={`${testid}-error`}>Unavailable right now.</p>;
}
