import { civicApi } from "./client";
import type { StateProfile } from "@/taxModel/engine";

/**
 * Fetch all state profiles from the backend (the single source of truth — all 50
 * states). The shape matches the client StateProfile type, so the shipped TypeScript
 * engine computes directly from these. Falls back to the bundled 8-state set offline.
 */
export async function getTaxStates(): Promise<StateProfile[]> {
  const { data } = await civicApi.get<StateProfile[]>("/tax-model/states");
  return data;
}
