import { civicApi, isNotFound } from "./client";
import type { ClaimStatus } from "./rooms";

/**
 * The claims ledger (PRD 04, design 1n / 1m).
 *
 * A claim is addressable on its own because the whole graph rests on that: rooms reference
 * claims rather than copying their text, so the evidence trail has to live somewhere a
 * reader can reach from any evidence mark, in any room, on any page.
 */

export interface ClaimSource {
  id: string;
  url: string;
  title: string;
  author: string | null;
  organization: string | null;
  sourceType: string;
  isPrimary: boolean;
  publishedAt: string | null;
  retrievedAt: string;
  availability: string;
  /** False for most reporting: Civic holds a headline and an RSS summary, not the body. */
  fullTextAvailable: boolean;
  hasInterest: boolean;
  interestNote: string | null;
}

export interface ClaimStatusHistoryEntry {
  fromStatus: ClaimStatus | null;
  toStatus: ClaimStatus;
  changeKind: string;
  rationale: string;
  changedAt: string;
  /** When the SOURCE was corrected, not when we noticed. The published metric keys on this. */
  sourceCorrectedAt: string | null;
}

export interface ClaimAppearance {
  objectType: string;
  objectId: string;
  slug: string;
  label: string;
  relation: string;
}

export interface ClaimSummary {
  id: string;
  slug: string;
  text: string;
  status: ClaimStatus;
  kind: string;
  evidenceSummary: string | null;
  lastReviewedAt: string | null;
  staleAsOf: string | null;
  supportingCount: number;
  contradictingCount: number;
}

export interface ClaimDetail extends ClaimSummary {
  whatWouldSettleIt: string;
  geographyScope: string | null;
  timeScopeStart: string | null;
  timeScopeEnd: string | null;
  confidence: number;
  firstSeenAt: string;
  evidenceFor: ClaimSource[];
  evidenceAgainst: ClaimSource[];
  assertedBy: ClaimAppearance[];
  appearsIn: ClaimAppearance[];
  statusHistory: ClaimStatusHistoryEntry[];
}

/** 404 resolves to undefined so the page can render a not-found state; other errors throw. */
export async function getClaim(slug: string): Promise<ClaimDetail | undefined> {
  try {
    const { data } = await civicApi.get<ClaimDetail>(`/claims/${slug}`);
    return data;
  } catch (err) {
    if (isNotFound(err)) return undefined;
    throw err;
  }
}

/** Least-settled first by default — design 1n: "that is where you are most likely to be misled." */
export async function listClaims(opts?: {
  sort?: "date" | "reviewed";
  unsettledOnly?: boolean;
  take?: number;
}): Promise<ClaimSummary[]> {
  const { data } = await civicApi.get<ClaimSummary[]>("/claims", { params: opts });
  return data;
}
