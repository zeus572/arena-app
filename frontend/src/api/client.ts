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
  config.headers["X-User-Id"] = getUserId();
  return config;
});

export async function fetchFeed(page = 1, pageSize = 20) {
  const res = await api.get<DebateSummary[]>("/feed", { params: { page, pageSize } });
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

export default api;
