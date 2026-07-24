// Shared helpers for the admin-gated endpoints. The backend gates /api/admin/* on the
// "Admin" policy (email allowlist) and returns 403 for non-admins; map that to a typed
// error so pages can render an "Admins only" state instead of a generic failure.

export class ForbiddenError extends Error {}

/** Rethrows as ForbiddenError on HTTP 403; otherwise rethrows the original error. Returns never. */
export function mapAdminError(err: unknown): never {
  const status = (err as { response?: { status?: number } })?.response?.status;
  if (status === 403) throw new ForbiddenError("Not an admin.");
  throw err instanceof Error ? err : new Error(String(err));
}
