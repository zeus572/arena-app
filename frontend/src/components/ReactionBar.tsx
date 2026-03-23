import { useState } from "react";
import { addReaction } from "../api/client";
import type { ReactionCounts } from "../api/types";

const REACTIONS = [
  { type: "like", emoji: "\uD83D\uDC4D" },
  { type: "fire", emoji: "\uD83D\uDD25" },
  { type: "think", emoji: "\uD83E\uDD14" },
  { type: "disagree", emoji: "\u274C" },
  { type: "insightful", emoji: "\uD83D\uDCA1" },
];

interface Props {
  targetType: "debate" | "turn";
  targetId: string;
  counts: ReactionCounts;
}

export default function ReactionBar({ targetType, targetId, counts }: Props) {
  const [localCounts, setLocalCounts] = useState<ReactionCounts>({ ...counts });
  const [reacted, setReacted] = useState<Set<string>>(new Set());

  async function handleReaction(type: string) {
    if (reacted.has(type)) return;

    // Optimistic update
    setLocalCounts((prev) => ({ ...prev, [type]: (prev[type] ?? 0) + 1 }));
    setReacted((prev) => new Set(prev).add(type));

    try {
      await addReaction(targetType, targetId, type);
    } catch {
      // Revert on failure
      setLocalCounts((prev) => ({ ...prev, [type]: (prev[type] ?? 1) - 1 }));
      setReacted((prev) => {
        const next = new Set(prev);
        next.delete(type);
        return next;
      });
    }
  }

  return (
    <div className="reaction-bar">
      {REACTIONS.map((r) => (
        <button
          key={r.type}
          className={`reaction-btn ${reacted.has(r.type) ? "reacted" : ""}`}
          onClick={() => handleReaction(r.type)}
          title={r.type}
        >
          {r.emoji} {localCounts[r.type] || 0}
        </button>
      ))}
    </div>
  );
}
