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
