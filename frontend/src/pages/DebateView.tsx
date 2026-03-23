import { useEffect, useState, useCallback } from "react";
import { useParams } from "react-router-dom";
import { fetchDebate, castVote } from "../api/client";
import type { DebateDetail } from "../api/types";
import ReactionBar from "../components/ReactionBar";

export default function DebateView() {
  const { id } = useParams<{ id: string }>();
  const [debate, setDebate] = useState<DebateDetail | null>(null);
  const [voted, setVoted] = useState(false);
  const [voting, setVoting] = useState(false);

  const loadDebate = useCallback(async () => {
    if (!id) return;
    const data = await fetchDebate(id);
    setDebate(data);
  }, [id]);

  useEffect(() => {
    loadDebate();
  }, [loadDebate]);

  // Check if user already voted (localStorage)
  useEffect(() => {
    if (id && localStorage.getItem(`vote-${id}`)) {
      setVoted(true);
    }
  }, [id]);

  // Live polling for active debates
  useEffect(() => {
    if (!debate || debate.status !== "Active") return;
    const interval = setInterval(loadDebate, 10_000);
    return () => clearInterval(interval);
  }, [debate?.status, loadDebate]);

  async function handleVote(agentId: string) {
    if (!id || voted || voting) return;
    setVoting(true);
    try {
      await castVote(id, agentId);
      localStorage.setItem(`vote-${id}`, agentId);
      setVoted(true);
      await loadDebate();
    } catch {
      // ignore duplicate vote errors
    }
    setVoting(false);
  }

  if (!debate) return <p>Loading debate...</p>;

  const votedFor = id ? localStorage.getItem(`vote-${id}`) : null;

  return (
    <div className="debate-view">
      <div className="debate-header">
        <h1>{debate.topic}</h1>
        {debate.description && <p className="debate-desc">{debate.description}</p>}
        <div className="debate-meta">
          <span className={`status-badge status-${debate.status.toLowerCase()}`}>
            {debate.status}
          </span>
          <span className="agents-vs">
            <strong>{debate.proponent.name}</strong> vs{" "}
            <strong>{debate.opponent.name}</strong>
          </span>
        </div>
      </div>

      {/* Voting */}
      <div className="vote-section">
        <button
          className={`vote-btn proponent ${votedFor === debate.proponent.id ? "voted" : ""}`}
          onClick={() => handleVote(debate.proponent.id)}
          disabled={voted || voting}
        >
          {debate.proponent.name} ({debate.proponentVotes})
        </button>
        <span className="vote-vs">VS</span>
        <button
          className={`vote-btn opponent ${votedFor === debate.opponent.id ? "voted" : ""}`}
          onClick={() => handleVote(debate.opponent.id)}
          disabled={voted || voting}
        >
          {debate.opponent.name} ({debate.opponentVotes})
        </button>
      </div>
      {voted && <p className="vote-status">You voted!</p>}

      {/* Debate-level reactions */}
      <ReactionBar targetType="debate" targetId={debate.id} counts={debate.reactions} />

      {/* Turns */}
      <div className="turns">
        {debate.turns.map((turn) => (
          <div
            key={turn.id}
            className={`turn ${turn.agentId === debate.proponent.id ? "proponent" : "opponent"}`}
          >
            <div className="turn-header">
              <strong>{turn.agent.name}</strong>
              <span className="turn-num">Turn {turn.turnNumber}</span>
            </div>
            <p>{turn.content}</p>
            <ReactionBar targetType="turn" targetId={turn.id} counts={turn.reactions} />
          </div>
        ))}
        {debate.status === "Active" && debate.turns.length === 0 && (
          <p className="waiting">Waiting for the first argument...</p>
        )}
        {debate.status === "Active" && debate.turns.length > 0 && (
          <p className="waiting">Waiting for next turn...</p>
        )}
      </div>
    </div>
  );
}
