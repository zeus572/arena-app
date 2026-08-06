import { civicApi, isNotFound } from "./client";

/**
 * Topic Rooms (docs/Rooms Expansion).
 *
 * One payload serves every density — the backend deliberately does not vary the facts by
 * view, so `?view=board` changes rendering only.
 */

/** The eight evidence statuses (PRD 07 §6.1). The mark renders from this, never from prose. */
export type ClaimStatus =
  | "Confirmed"
  | "StronglySupported"
  | "PlausibleButUnresolved"
  | "Disputed"
  | "Unsupported"
  | "False"
  | "Outdated"
  | "Prediction";

export type ClaimKind = "Factual" | "Interpretation" | "Opinion" | "Prediction";

export type RoomKind = "Theme" | "Story";

export interface RoomSummary {
  id: string;
  slug: string;
  kind: RoomKind;
  title: string;
  dek: string;
  status: string;
  locality: string | null;
  revision: number;
  lastMeaningfulUpdateAt: string | null;
  contentNote: string | null;
  storyType?: string | null;
  eventTime?: string | null;
  estimatedMinutes?: number | null;
}

export interface EssentialFact {
  text: string;
  claimId: string | null;
  claimSlug: string | null;
  /** Null when no claim is attached yet; otherwise drives the evidence mark. */
  claimStatus: ClaimStatus | null;
  ordinal: number;
}

export interface TerminologyNote {
  term: string;
  note: string;
}

export interface ChangeLogEntry {
  type: string;
  /** Short uppercase word for the ledger's type column. */
  label: string;
  isMeaningful: boolean;
  headline: string;
  whyItMatters: string | null;
  objectType: string | null;
  objectId: string | null;
  fromValue: string | null;
  toValue: string | null;
  correctionKind: string | null;
  revision: number;
  createdAt: string;
}

export interface WithheldChange {
  type: string;
  count: number;
}

/**
 * "Since your last visit." Corrections arrive in their OWN array — the API splits them so
 * the UI structurally cannot fold them into "updated".
 */
export interface RoomDelta {
  fromRevision: number;
  toRevision: number;
  meaningfulChanges: ChangeLogEntry[];
  corrections: ChangeLogEntry[];
  withheldCount: number;
  withheldByType: WithheldChange[];
  hasChanges: boolean;
}

export interface SectionProgress {
  sectionKey: string;
  opened: boolean;
  itemsSeen: number;
  itemsTotal: number;
}

export interface RoomViewerState {
  lastSeenRevision: number;
  following: boolean;
  density: "Read" | "Brief" | "Board";
  sectionProgress: SectionProgress[];
  /** Present only on a return visit with something to report. */
  delta: RoomDelta | null;
}

export interface ThemeRoomDetail extends RoomSummary {
  alternateTitles: string[];
  scopeStatement: string;
  inclusionRules: string[];
  exclusionRules: string[];
  /** The room's most important element. Describes a state, not an event. */
  currentStatusSentence: string;
  /** True when the sentence is withheld pending an unreviewed correction. */
  statusSentenceUnderReview: boolean;
  topUnresolvedQuestion: string;
  watchNext: string;
  essentialFacts: EssentialFact[];
  terminologyNotes: TerminologyNote[];
  monitoringCadence: string;
  articlesConsideredCount: number;
  developmentWindowDays: number;
  viewer: RoomViewerState;
}

export interface StoryDimension {
  dimension: string;
  text: string;
  claimId: string | null;
}

export interface StakeholderImpact {
  group: string;
  impactSummary: string;
  confidence: number;
}

export interface NextStep {
  description: string;
  /** "Confirmed if:" — the objective criterion. */
  verificationCondition: string;
  actorId: string | null;
  expectedTiming: string | null;
  predictionId: string | null;
}

export interface StoryRoomDetail extends RoomSummary {
  howItWorksIntro: string;
  whyItMatters: StoryDimension[];
  stakeholders: StakeholderImpact[];
  nextSteps: NextStep[];
  typePayload: unknown;
  sourceBillId: string | null;
  viewer: RoomViewerState;
}

export interface Development {
  id: string;
  occurredAt: string;
  category: string;
  headline: string;
  summary: string;
  whyItMatters: string;
  inclusionReason: string;
  evidenceStatus: ClaimStatus;
  storyRoomId: string | null;
  storySlug: string | null;
}

/** The Latest section, with the denominator the disclosure depends on. */
export interface RoomLatest {
  developments: Development[];
  articlesConsidered: number;
  windowDays: number;
  inclusionRules: string[];
  exclusionRules: string[];
  excludedCount: number;
}

export interface TimelineEvent {
  occurredOn: string;
  occurredPrecision: "Day" | "Month" | "Year";
  label: string;
  description: string;
  marker: "Agreed" | "Contested" | "Trigger" | "Now";
  /** What was known ON this date, not what is known now. */
  whatWasKnownThen: string | null;
  textAlternative: string | null;
}

export interface RoomActor {
  id: string;
  slug: string;
  name: string;
  actorType: string;
  tier: "Decides" | "Shapes" | "Constrained";
  roleHere: string;
  actualPower: string;
  /** Always a quote or filing with a date — never inferred motive. */
  statedWants: string | null;
  statedWantsAsOf: string | null;
  statedWantsSourceRefId: string | null;
  constrainedBy: string;
  leverageStatement: string;
  appearanceCount: number;
}

export interface RoomActors {
  decisionKey: string | null;
  availableDecisions: string[];
  decides: RoomActor[];
  shapes: RoomActor[];
  constrained: RoomActor[];
}

export interface SourceRef {
  id: string;
  url: string;
  title: string;
  author: string | null;
  organization: string | null;
  /** What kind of source this is — never a trust score. */
  sourceType: string;
  isPrimary: boolean;
  publishedAt: string | null;
  retrievedAt: string;
  availability: string;
  hasInterest: boolean;
  interestNote: string | null;
}

export interface ClaimStatusHistoryEntry {
  fromStatus: string | null;
  toStatus: string;
  changeKind: string;
  rationale: string;
  changedAt: string;
  /** When the ORIGINAL source corrected itself. The published metric runs from here. */
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
  kind: ClaimKind;
  evidenceSummary: string | null;
  lastReviewedAt: string | null;
  staleAsOf: string | null;
  supportingCount: number;
  contradictingCount: number;
}

export interface ClaimDetail extends ClaimSummary {
  /** Required field — a claim nobody can say how to settle is not a claim. */
  whatWouldSettleIt: string;
  geographyScope: string | null;
  timeScopeStart: string | null;
  timeScopeEnd: string | null;
  confidence: number;
  firstSeenAt: string;
  evidenceFor: SourceRef[];
  evidenceAgainst: SourceRef[];
  assertedBy: ClaimAppearance[];
  appearsIn: ClaimAppearance[];
  statusHistory: ClaimStatusHistoryEntry[];
}

export interface RoomRevisionMark {
  revision: number;
  isMeaningful: boolean;
  summary: string;
  createdAt: string;
  hasSnapshot: boolean;
}

/** One source behind the room, as returned by /rooms/{slug}/sources. */
export interface RoomSource {
  id: string;
  url: string;
  title: string;
  organization: string | null;
  sourceType: string;
  isPrimary: boolean;
  publishedAt: string | null;
  availability: string;
  /** False for most reporting: Civic holds a headline and an RSS summary, not the body. */
  fullTextAvailable: boolean;
}

export interface RoomSourceGroup {
  sourceType: string;
  count: number;
  sources: RoomSource[];
}

export interface RoomSources {
  total: number;
  fullTextHeldCount: number;
  groups: RoomSourceGroup[];
}

// ---------------------------------------------------------------------------- calls

export async function listRooms(kind?: RoomKind): Promise<RoomSummary[]> {
  const { data } = await civicApi.get<RoomSummary[]>("/rooms", {
    params: kind ? { kind } : undefined,
  });
  return data;
}

/** 404 resolves to undefined so callers can render a not-found state; other errors throw. */
export async function getThemeRoom(slug: string): Promise<ThemeRoomDetail | undefined> {
  try {
    const { data } = await civicApi.get<ThemeRoomDetail>(`/rooms/${slug}`);
    return data;
  } catch (err) {
    if (isNotFound(err)) return undefined;
    throw err;
  }
}

export async function getStoryRoom(slug: string): Promise<StoryRoomDetail | undefined> {
  try {
    const { data } = await civicApi.get<StoryRoomDetail>(`/rooms/${slug}`);
    return data;
  } catch (err) {
    if (isNotFound(err)) return undefined;
    throw err;
  }
}

export async function getRoomLatest(slug: string): Promise<RoomLatest> {
  const { data } = await civicApi.get<RoomLatest>(`/rooms/${slug}/latest`);
  return data;
}

export async function getRoomTimeline(slug: string): Promise<TimelineEvent[]> {
  const { data } = await civicApi.get<TimelineEvent[]>(`/rooms/${slug}/timeline`);
  return data;
}

export async function getRoomActors(slug: string, decision?: string): Promise<RoomActors> {
  const { data } = await civicApi.get<RoomActors>(`/rooms/${slug}/actors`, {
    params: decision ? { decision } : undefined,
  });
  return data;
}

export async function getRoomDelta(slug: string, sinceRevision?: number): Promise<RoomDelta> {
  const { data } = await civicApi.get<RoomDelta>(`/rooms/${slug}/delta`, {
    params: sinceRevision === undefined ? undefined : { sinceRevision },
  });
  return data;
}

export async function getRoomChangelog(slug: string, take = 100): Promise<ChangeLogEntry[]> {
  const { data } = await civicApi.get<ChangeLogEntry[]>(`/rooms/${slug}/changelog`, {
    params: { take },
  });
  return data;
}

export async function getRoomRevisions(slug: string): Promise<RoomRevisionMark[]> {
  const { data } = await civicApi.get<RoomRevisionMark[]>(`/rooms/${slug}/revisions`);
  return data;
}

/** Anonymous-friendly: PRD 01 §TR-5 wants "since your last visit" to work without an account. */
export async function markRoomSeen(slug: string, revision?: number): Promise<void> {
  await civicApi.post(`/rooms/${slug}/seen`, { revision });
}

export async function followRoom(slug: string): Promise<void> {
  await civicApi.post(`/rooms/${slug}/follow`);
}

export async function unfollowRoom(slug: string): Promise<void> {
  await civicApi.delete(`/rooms/${slug}/follow`);
}

export async function recordSectionProgress(
  slug: string,
  sectionKey: string,
  itemsSeen: number,
  itemsTotal: number,
): Promise<void> {
  await civicApi.post(`/rooms/${slug}/sections/${sectionKey}/progress`, {
    itemsSeen,
    itemsTotal,
  });
}

export async function listClaims(opts?: {
  sort?: "date" | "reviewed";
  unsettledOnly?: boolean;
  take?: number;
}): Promise<ClaimSummary[]> {
  const { data } = await civicApi.get<ClaimSummary[]>("/claims", { params: opts });
  return data;
}

export async function getClaim(slug: string): Promise<ClaimDetail | undefined> {
  try {
    const { data } = await civicApi.get<ClaimDetail>(`/claims/${slug}`);
    return data;
  } catch (err) {
    if (isNotFound(err)) return undefined;
    throw err;
  }
}


export async function getRoomSources(slug: string): Promise<RoomSources> {
  const { data } = await civicApi.get<RoomSources>(`/rooms/${slug}/sources`);
  return data;
}
