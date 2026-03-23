import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { fetchAgents, createDebate } from "../api/client";
import type { Agent } from "../api/types";

export default function StartArgument() {
  const navigate = useNavigate();
  const [agents, setAgents] = useState<Agent[]>([]);
  const [topic, setTopic] = useState("");
  const [description, setDescription] = useState("");
  const [proponentId, setProponentId] = useState("");
  const [opponentId, setOpponentId] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    fetchAgents().then(setAgents);
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!topic.trim()) {
      setError("Topic is required.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      const result = await createDebate({
        topic: topic.trim(),
        description: description.trim() || undefined,
        proponentId: proponentId || undefined,
        opponentId: opponentId || undefined,
      });
      navigate(`/debates/${result.id}`);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to create debate.";
      setError(msg);
      setSubmitting(false);
    }
  }

  return (
    <div>
      <h1>Start an Argument</h1>
      <form onSubmit={handleSubmit} className="start-form">
        <label>
          Topic *
          <input
            type="text"
            value={topic}
            onChange={(e) => setTopic(e.target.value)}
            placeholder="e.g. Should AI be regulated?"
            required
          />
        </label>

        <label>
          Description (optional)
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Provide context for the debate..."
            rows={3}
          />
        </label>

        <div className="agent-pickers">
          <label>
            Proponent
            <select value={proponentId} onChange={(e) => setProponentId(e.target.value)}>
              <option value="">Random</option>
              {agents.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            Opponent
            <select value={opponentId} onChange={(e) => setOpponentId(e.target.value)}>
              <option value="">Random</option>
              {agents.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </select>
          </label>
        </div>

        {error && <p className="error">{error}</p>}

        <button type="submit" disabled={submitting}>
          {submitting ? "Creating..." : "Start Debate"}
        </button>
      </form>
    </div>
  );
}
