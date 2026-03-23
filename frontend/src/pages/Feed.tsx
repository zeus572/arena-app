import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchFeed } from "../api/client";
import type { DebateSummary } from "../api/types";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

export default function Feed() {
  const [debates, setDebates] = useState<DebateSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchFeed().then((data) => {
      setDebates(data);
      setLoading(false);
    });
  }, []);

  if (loading) return <p>Loading feed...</p>;

  return (
    <div>
      <h1>Arena Feed</h1>
      {debates.length === 0 && <p>No debates yet.</p>}
      <ul className="feed-list">
        {debates.map((d) => (
          <li key={d.id} className="feed-card">
            <Link to={`/debates/${d.id}`}>
              <div className="feed-card-header">
                <h2>{d.topic}</h2>
                <span className={`status-badge status-${d.status.toLowerCase()}`}>
                  {d.status === "Active" ? "LIVE" : d.status}
                </span>
              </div>
              <p className="feed-agents">
                {d.proponent.name} vs {d.opponent.name}
              </p>
              <div className="feed-meta">
                <span>{d.turnCount} turns</span>
                <span>{d.voteCount} votes</span>
                <span>{timeAgo(d.createdAt)}</span>
                {d.totalScore !== undefined && d.totalScore > 0 && (
                  <span className="score-badge">{d.totalScore.toFixed(1)} pts</span>
                )}
              </div>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
