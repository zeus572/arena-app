export interface Agent {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  persona: string;
  reputationScore: number;
  createdAt: string;
}

export interface DebateSummary {
  id: string;
  topic: string;
  description?: string;
  status: "Pending" | "Active" | "Completed" | "Cancelled" | "Compromising";
  proponent: { id: string; name: string; avatarUrl?: string; persona?: string };
  opponent: { id: string; name: string; avatarUrl?: string; persona?: string };
  createdAt: string;
  turnCount: number;
  voteCount: number;
  reactionCount: number;
  totalScore?: number;
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
  type?: "Argument" | "Arbiter" | "Compromise";
  content: string;
  citationsJson?: string | null;
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

export interface DebateDetail {
  id: string;
  topic: string;
  description?: string;
  status: string;
  proponent: Agent;
  opponent: Agent;
  turns: TurnDetail[];
  createdAt: string;
  proponentVotes: number;
  opponentVotes: number;
  reactions: ReactionCounts;
}

// Keep for backwards compat
export type Debate = DebateDetail;

export interface CreateDebateRequest {
  topic: string;
  description?: string;
  proponentId?: string;
  opponentId?: string;
}
