export interface AgentStats {
  wins: number;
  losses: number;
  draws: number;
  totalDebates: number;
  winStreak: number;
  topTag?: string | null;
  title?: string | null;
}

export interface Agent {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  persona: string;
  reputationScore: number;
  createdAt: string;
  agentType?: string | null;
  era?: string | null;
  stats?: AgentStats;
}

export interface DebateSummary {
  id: string;
  topic: string;
  description?: string;
  status: "Pending" | "Active" | "Completed" | "Cancelled" | "Compromising";
  format?: string;
  proponent: { id: string; name: string; avatarUrl?: string; persona?: string; agentType?: string | null; era?: string | null };
  opponent: { id: string; name: string; avatarUrl?: string; persona?: string; agentType?: string | null; era?: string | null };
  createdAt: string;
  turnCount: number;
  voteCount: number;
  reactionCount: number;
  totalScore?: number;
  proponentVotes: number;
  opponentVotes: number;
  reactions: ReactionCounts;
  source?: string;
  newsInfo?: NewsInfo | null;
  label?: "Controversial" | "Insightful" | "Heated" | null;
  rivalry?: { matchups: number; proponentWins: number; opponentWins: number } | null;
  topQuote?: TopQuote | null;
}

export interface TopQuote {
  text: string;
  agentName: string;
  isProponent: boolean;
  reactionCount: number;
  insightfulCount: number;
}

export interface NewsInfo {
  headline: string;
  source: string;
  publishedAt: string;
}

export type ReactionCounts = Record<string, number>;

export interface TurnCitation {
  source: string;
  title: string;
  url: string;
}

export interface TurnDetail {
  id: string;
  debateId: string;
  agentId: string;
  agent: { id: string; name: string; avatarUrl?: string };
  turnNumber: number;
  type?: "Argument" | "Arbiter" | "Compromise" | "Wildcard" | "Commentary" | "Agreement" | "Question" | "Roast";
  content: string;
  citationsJson?: string | null;
  analysisJson?: string | null;
  createdAt: string;
  reactions: ReactionCounts;
}

export interface Turn {
  id: string;
  debateId: string;
  agentId: string;
  agent: Agent;
  turnNumber: number;
  content: string;
  createdAt: string;
}

export interface FormatConfig {
  displayName: string;
  maxTurns: number;
  maxCharactersPerTurn?: number | null;
  hasCompromisePhase: boolean;
  hasWildcards: boolean;
  hasCommentary: boolean;
}

export interface DebateDetail {
  id: string;
  topic: string;
  description?: string;
  status: string;
  format?: string;
  formatConfig?: FormatConfig;
  proponent: Agent;
  opponent: Agent;
  turns: TurnDetail[];
  createdAt: string;
  source?: string;
  newsInfo?: NewsInfo | null;
  proponentVotes: number;
  opponentVotes: number;
  reactions: ReactionCounts;
  arena?: {
    id: string;
    slug: string;
    name: string;
    iconEmoji: string;
    accentColor: string;
    tone: string;
  } | null;
  forkedFromDebateId?: string | null;
  forkNote?: string | null;
  forkCount?: number;
}

// Keep for backwards compat
export type Debate = DebateDetail;

export interface LeaderboardAgent {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  persona: string;
  reputationScore: number;
  stats: {
    wins: number;
    losses: number;
    draws: number;
    totalDebates: number;
    winRate: number;
    periodWins: number;
    periodLosses: number;
    controversialDebates: number;
    avgVoteMargin: number;
    totalReactions: number;
    disagreeReactions: number;
    insightfulReactions: number;
    topTag?: string | null;
    underratedScore: number;
  };
}

export interface LeaderboardResponse {
  sort: string;
  period: string;
  agents: LeaderboardAgent[];
}

export interface PredictionData {
  totalPredictions: number;
  proponentPredictions: number;
  opponentPredictions: number;
  proponentOdds: number;
  opponentOdds: number;
  userPredictedAgentId?: string | null;
  userIsCorrect?: boolean | null;
}

export interface InterventionData {
  id: string;
  content: string;
  upvotes: number;
  used: boolean;
  usedInTurnNumber?: number | null;
  createdAt: string;
  authorName: string;
}

export interface AgentSourceInfo {
  id: string;
  sourceType: string;
  title: string;
  author: string;
  year?: number | null;
  themeTag?: string | null;
  priority: number;
}

export interface AgentDetail {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  persona: string;
  reputationScore: number;
  createdAt: string;
  agentType?: string | null;
  era?: string | null;
  sources?: AgentSourceInfo[];
  stats: {
    wins: number;
    losses: number;
    draws: number;
    totalDebates: number;
    avgWordsPerTurn: number;
    totalTurns: number;
    totalCitations: number;
  };
  personality: {
    aggressiveness: number;
    eloquence: number;
    factReliance: number;
    empathy: number;
    wit: number;
  };
  topTags: { tag: string; count: number }[];
  reactionBreakdown: {
    likes: number;
    insightful: number;
    disagree: number;
  };
}

export interface CreateDebateRequest {
  topic: string;
  description?: string;
  format?: string;
  proponentId?: string;
  opponentId?: string;
  arenaId?: string;
  arenaSlug?: string;
}

export interface ArenaSummary {
  id: string;
  slug: string;
  name: string;
  description: string;
  topic: string;
  tone: "serious" | "comedic" | "adversarial" | "educational";
  defaultFormat: string;
  iconEmoji: string;
  accentColor: string;
  isOfficial: boolean;
  debateCount: number;
  activeDebateCount: number;
}

export interface ArenaDetail extends ArenaSummary {
  rules: string;
  createdAt: string;
}

export interface ArenaFeedItem {
  id: string;
  topic: string;
  description?: string;
  status: string;
  format: string;
  source?: string;
  proponent: { id: string; name: string; avatarUrl?: string; persona?: string };
  opponent: { id: string; name: string; avatarUrl?: string; persona?: string };
  createdAt: string;
  turnCount: number;
  voteCount: number;
  reactionCount: number;
  totalScore: number;
  proponentVotes: number;
  opponentVotes: number;
  forkCount: number;
  isForked: boolean;
}

export interface ArenaFeedResponse {
  arena: {
    id: string;
    slug: string;
    name: string;
    description: string;
    topic: string;
    tone: string;
    rules: string;
    defaultFormat: string;
    iconEmoji: string;
    accentColor: string;
  };
  items: ArenaFeedItem[];
  totalCount: number;
}

export interface ForkDebateRequest {
  topic?: string;
  forkNote?: string;
  format?: string;
  arenaId?: string;
  proponentId?: string;
  opponentId?: string;
}

export interface ForkSummary {
  id: string;
  topic: string;
  forkNote?: string | null;
  status: string;
  format: string;
  proponent: { id: string; name: string; avatarUrl?: string };
  opponent: { id: string; name: string; avatarUrl?: string };
  createdAt: string;
  turnCount: number;
}

export interface DebateFormatInfo {
  format: string;
  displayName: string;
  description: string;
  maxTurns: number;
  maxTokens: number;
  maxCharactersPerTurn?: number | null;
  hasCompromisePhase: boolean;
  hasWildcards: boolean;
  hasCommentary: boolean;
  hasTools: boolean;
  hasBudgetTable: boolean;
}

/* ─────────────────── Campaign Manager ─────────────────── */

export type CampaignDifficulty = "Easy" | "Normal" | "Hard";
export type CampaignStatus = "Active" | "Completed" | "Abandoned";
export type CampaignEventTypeValue = "Opportunity" | "Crisis" | "Neutral";
export type CampaignActivityType =
  | "Advertising"
  | "TownHall"
  | "Fundraising"
  | "OppResearch"
  | "DebatePrep"
  | "Polling";

export interface Persona {
  key: string;
  name: string;
  persona: string;
  theme: string;
  opponentName: string;
  opponentPersona: string;
}

export interface CampaignSummary {
  id: string;
  candidateName: string;
  personaId: string;
  opponentName: string;
  theme: string;
  currentWeek: number;
  totalWeeks: number;
  difficulty: string;
  status: string;
  approval: number;
  won?: boolean | null;
  finalApproval?: number | null;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
}

export interface CampaignResources {
  budget: number;
  timeUnits: number;
  staffCount: number;
  momentum: number;
}

export interface CampaignWeek {
  weekNumber: number;
  approvalRating: number;
  decisionsJson: string;
  resourceChangesJson: string;
  debateId?: string | null;
  summary: string;
  createdAt: string;
}

export interface CampaignEventOption {
  id: string;
  label: string;
}

export interface CampaignEvent {
  id: string;
  weekNumber: number;
  type: string;
  eventKey: string;
  title: string;
  description: string;
  options: CampaignEventOption[];
  resolved: boolean;
  responseChosen?: string | null;
}

export interface CampaignDetail {
  campaign: CampaignSummary;
  resources: CampaignResources;
  currentApproval: number;
  weeks: CampaignWeek[];
  pendingEvents: CampaignEvent[];
  debateMilestoneDue: boolean;
  activeDebateId?: string | null;
}

export interface AdvanceWeekResult {
  detail: CampaignDetail;
  weekSummary: CampaignWeek;
  completed: boolean;
  debateMilestoneDue: boolean;
}

export interface DebateMilestoneResult {
  debateId?: string | null;
  skipped: boolean;
  won?: boolean | null;
  signedEffect: number;
  summary: string;
  detail: CampaignDetail;
}

export interface AllocationLineItem {
  type: string;
  budgetCost: number;
  timeCost: number;
  note?: string | null;
}

export interface AllocationPreviewResult {
  affordable: boolean;
  projectedBudget: number;
  projectedTimeUnits: number;
  projectedStaff: number;
  issues: string[];
  lineItems: AllocationLineItem[];
}

export interface CampaignResults {
  candidateName: string;
  won: boolean;
  finalApproval: number;
  totalWeeks: number;
  debatesPlayed: number;
  debatesWon: number;
  approvalTrend: number[];
  outcome: string;
}

export interface ActivityAllocation {
  type: CampaignActivityType;
  budget?: number | null;
  timeUnits?: number | null;
  staffCount?: number | null;
  count?: number | null;
}

export interface CreateCampaignRequest {
  candidateName: string;
  personaId: string;
  difficulty: CampaignDifficulty;
  totalWeeks?: number;
  theme?: string;
  platform?: Record<string, string>;
}

export interface AdvanceWeekRequest {
  activities: ActivityAllocation[];
}

export interface RespondEventRequest {
  optionId: string;
}

export interface RunDebateRequest {
  skip: boolean;
  topic?: string;
}
