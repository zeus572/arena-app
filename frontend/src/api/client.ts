import axios from "axios";
import type { DebateDetail, DebateSummary, Agent, CreateDebateRequest } from "./types";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://localhost:5000/api",
});

// Attach a persistent anonymous user ID
function getUserId(): string {
  let id = localStorage.getItem("arena-user-id");
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem("arena-user-id", id);
  }
  return id;
}

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("arena-access-token");
  if (token) {
    config.headers["Authorization"] = `Bearer ${token}`;
  } else {
    // Fallback: legacy anonymous ID for unauthenticated users
    config.headers["X-User-Id"] = getUserId();
  }
  return config;
});

export interface FeedParams {
  page?: number;
  pageSize?: number;
  q?: string;
  tag?: string;
  sort?: "hot" | "new" | "top";
  status?: string;
}

export interface FeedResponse {
  items: DebateSummary[];
  totalCount: number;
}

export async function fetchFeed(params: FeedParams = {}) {
  const res = await api.get<FeedResponse>("/feed", { params: { page: 1, pageSize: 20, ...params } });
  return res.data;
}

export async function fetchDebate(id: string) {
  const res = await api.get<DebateDetail>(`/debates/${id}`);
  return res.data;
}

export async function fetchAgents() {
  const res = await api.get<Agent[]>("/agents");
  return res.data;
}

export async function createDebate(req: CreateDebateRequest) {
  const res = await api.post<{ id: string }>("/debates", req);
  return res.data;
}

export async function castVote(debateId: string, votedForAgentId: string) {
  const res = await api.post(`/debates/${debateId}/votes`, { votedForAgentId });
  return res.data;
}

export async function fetchTrendingTopics() {
  const res = await api.get<{ topic: string; score: number }[]>("/feed/trending");
  return res.data;
}

export async function addReaction(
  targetType: "debate" | "turn",
  targetId: string,
  type: string,
) {
  const url =
    targetType === "debate"
      ? `/debates/${targetId}/reactions`
      : `/turns/${targetId}/reactions`;
  const res = await api.post(url, { type });
  return res.data;
}

// Sources
export interface SourceInfo {
  id: string;
  name: string;
  url: string;
  category: string;
  description: string;
  icon: string;
}

export async function fetchSources() {
  const res = await api.get<SourceInfo[]>("/sources");
  return res.data;
}

// Topics
export interface TopicParams {
  page?: number;
  pageSize?: number;
  sort?: "hot" | "new" | "top";
  status?: string;
}

export interface TopicProposal {
  id: string;
  title: string;
  description?: string;
  status: string;
  upvoteCount: number;
  downvoteCount: number;
  netVotes: number;
  proposedBy: { id: string; displayName: string | null };
  createdAt: string;
  userVote?: number | null;
}

export interface TopicResponse {
  items: TopicProposal[];
  totalCount: number;
}

export async function fetchTopics(params: TopicParams = {}) {
  const res = await api.get<TopicResponse>("/topics", { params: { page: 1, pageSize: 20, ...params } });
  return res.data;
}

export async function createTopic(title: string, description?: string) {
  const res = await api.post<{ id: string; title: string }>("/topics", { title, description });
  return res.data;
}

export async function voteOnTopic(topicId: string, value: 1 | -1) {
  const res = await api.post<{ upvoteCount: number; downvoteCount: number; netVotes: number }>(`/topics/${topicId}/vote`, { value });
  return res.data;
}

export async function removeTopicVote(topicId: string) {
  const res = await api.delete<{ upvoteCount: number; downvoteCount: number; netVotes: number }>(`/topics/${topicId}/vote`);
  return res.data;
}

export async function forceTick() {
  const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5000/api").replace(/\/api$/, "");
  const res = await axios.post(`${baseUrl}/dev/tick`);
  return res.data;
}

export default api;
