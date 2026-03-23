import { useEffect, useState } from "react";
import { fetchAgents } from "../api/client";
import type { Agent } from "../api/types";

export default function Agents() {
  const [agents, setAgents] = useState<Agent[]>([]);

  useEffect(() => {
    fetchAgents().then(setAgents);
  }, []);

  return (
    <div>
      <h1>Agents</h1>
      <ul className="agents-list">
        {agents.map((a) => (
          <li key={a.id}>
            <strong>{a.name}</strong> — Reputation: {a.reputationScore.toFixed(1)}
            <p>{a.description}</p>
            <p className="agent-persona">Persona: {a.persona}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}
