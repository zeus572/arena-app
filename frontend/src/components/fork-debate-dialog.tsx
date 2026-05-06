import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { fetchArenas, forkDebate } from "@/api/client";
import type { ArenaSummary } from "@/api/types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { GitFork, X } from "lucide-react";

interface ForkDebateDialogProps {
  debateId: string;
  parentTopic: string;
  parentArenaId?: string | null;
  onClose: () => void;
}

export function ForkDebateDialog({ debateId, parentTopic, parentArenaId, onClose }: ForkDebateDialogProps) {
  const navigate = useNavigate();
  const [arenas, setArenas] = useState<ArenaSummary[]>([]);
  const [topic, setTopic] = useState(`Re: ${parentTopic}`);
  const [forkNote, setForkNote] = useState("");
  const [arenaId, setArenaId] = useState<string | null>(parentArenaId ?? null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchArenas().then(setArenas).catch(() => {});
  }, []);

  const handleSubmit = async () => {
    if (submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await forkDebate(debateId, {
        topic: topic.trim() || undefined,
        forkNote: forkNote.trim() || undefined,
        arenaId: arenaId ?? undefined,
      });
      navigate(`/debates/${result.id}`);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : "Fork failed.";
      setError(msg);
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div className="relative w-full max-w-lg rounded-xl border border-border bg-card shadow-2xl">
        <button
          onClick={onClose}
          className="absolute right-3 top-3 text-muted-foreground hover:text-foreground"
          aria-label="Close"
        >
          <X size={16} />
        </button>

        <div className="p-5">
          <div className="flex items-center gap-2 mb-1">
            <GitFork size={16} className="text-primary" />
            <h2 className="text-lg font-bold tracking-tight">Fork this debate</h2>
          </div>
          <p className="text-xs text-muted-foreground mb-4 leading-relaxed">
            Branch this debate into a new one — swap the framing, change the arena, or just
            see what happens with fresh debaters arguing the same question.
          </p>

          <label className="block text-xs font-semibold text-foreground mb-1.5">
            New topic
          </label>
          <input
            value={topic}
            onChange={(e) => setTopic(e.target.value)}
            className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 mb-4"
          />

          <label className="block text-xs font-semibold text-foreground mb-1.5">
            What's different in this fork? <span className="font-normal text-muted-foreground">(optional)</span>
          </label>
          <textarea
            value={forkNote}
            onChange={(e) => setForkNote(e.target.value)}
            rows={3}
            placeholder="e.g. What if we assume AGI is 50 years away instead of 5?"
            className="w-full resize-none rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 mb-4"
          />

          {arenas.length > 0 && (
            <>
              <label className="block text-xs font-semibold text-foreground mb-1.5">
                Arena
              </label>
              <div className="flex flex-wrap gap-1.5 mb-4">
                <button
                  onClick={() => setArenaId(null)}
                  className={cn(
                    "rounded-full border px-2.5 py-1 text-[11px] transition-colors",
                    arenaId === null
                      ? "border-primary bg-primary/10 text-primary font-semibold"
                      : "border-border text-muted-foreground hover:text-foreground",
                  )}
                >
                  None
                </button>
                {arenas.map((a) => (
                  <button
                    key={a.id}
                    onClick={() => setArenaId(a.id)}
                    className={cn(
                      "flex items-center gap-1 rounded-full border px-2.5 py-1 text-[11px] transition-colors",
                      arenaId === a.id
                        ? "border-primary bg-primary/10 text-primary font-semibold"
                        : "border-border text-muted-foreground hover:text-foreground",
                    )}
                  >
                    <span>{a.iconEmoji}</span>
                    <span>{a.name}</span>
                  </button>
                ))}
              </div>
            </>
          )}

          {error && (
            <p className="mb-3 text-xs text-destructive">{error}</p>
          )}

          <div className="flex items-center justify-end gap-2">
            <Button variant="ghost" size="sm" onClick={onClose} disabled={submitting}>
              Cancel
            </Button>
            <Button size="sm" onClick={handleSubmit} disabled={submitting || topic.trim().length < 3} className="gap-1.5">
              {submitting ? "Forking…" : (<><GitFork size={14} /> Fork it</>)}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
