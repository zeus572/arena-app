import { useEffect, useState, useCallback } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { fetchTopics, createTopic, voteOnTopic, removeTopicVote, type TopicProposal, type TopicParams } from "@/api/client";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { ChevronUp, ChevronDown, Plus, Flame, Clock, Trophy, Crown, X } from "lucide-react";

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

function TopicCard({
  topic,
  isAuthenticated,
  onVote,
}: {
  topic: TopicProposal;
  isAuthenticated: boolean;
  onVote: (id: string, value: 1 | -1 | 0) => void;
}) {
  return (
    <div className="rounded-xl border border-border bg-card p-4 flex gap-3">
      <div className="flex flex-col items-center gap-0.5 shrink-0">
        <button
          disabled={!isAuthenticated}
          onClick={() => onVote(topic.id, topic.userVote === 1 ? 0 : 1)}
          className={cn(
            "p-1 rounded hover:bg-secondary transition-colors",
            topic.userVote === 1 ? "text-primary" : "text-muted-foreground",
            !isAuthenticated && "opacity-40 cursor-not-allowed"
          )}
        >
          <ChevronUp size={18} />
        </button>
        <span className={cn(
          "text-sm font-bold min-w-[2ch] text-center",
          topic.netVotes > 0 ? "text-primary" : topic.netVotes < 0 ? "text-destructive" : "text-muted-foreground"
        )}>
          {topic.netVotes}
        </span>
        <button
          disabled={!isAuthenticated}
          onClick={() => onVote(topic.id, topic.userVote === -1 ? 0 : -1)}
          className={cn(
            "p-1 rounded hover:bg-secondary transition-colors",
            topic.userVote === -1 ? "text-destructive" : "text-muted-foreground",
            !isAuthenticated && "opacity-40 cursor-not-allowed"
          )}
        >
          <ChevronDown size={18} />
        </button>
      </div>

      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-card-foreground">{topic.title}</p>
        {topic.description && (
          <p className="text-xs text-muted-foreground mt-1 line-clamp-2">{topic.description}</p>
        )}
        <div className="flex items-center gap-3 mt-2 text-[11px] text-muted-foreground">
          <span>by {topic.proposedBy.displayName ?? "Anonymous"}</span>
          <span>{timeAgo(topic.createdAt)}</span>
          <span className={cn(
            "rounded-full px-2 py-0.5 text-[10px] font-semibold",
            topic.status === "Approved" ? "bg-green-500/10 text-green-600" :
            topic.status === "Rejected" ? "bg-red-500/10 text-red-500" :
            "bg-secondary text-muted-foreground"
          )}>
            {topic.status}
          </span>
        </div>
      </div>
    </div>
  );
}

export default function Topics() {
  const { isAuthenticated, isPremium } = useAuth();
  const [topics, setTopics] = useState<TopicProposal[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState<"hot" | "new" | "top">("hot");
  const [loading, setLoading] = useState(true);
  const [showSubmit, setShowSubmit] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newDesc, setNewDesc] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const loadTopics = useCallback(async (params: TopicParams = {}) => {
    const data = await fetchTopics({ sort: sortBy, ...params });
    setTopics(data.items);
    setTotalCount(data.totalCount);
    setLoading(false);
  }, [sortBy]);

  useEffect(() => {
    setLoading(true);
    loadTopics();
  }, [loadTopics]);

  const handleVote = async (topicId: string, value: 1 | -1 | 0) => {
    if (!isAuthenticated) return;
    try {
      let result;
      if (value === 0) {
        result = await removeTopicVote(topicId);
      } else {
        result = await voteOnTopic(topicId, value);
      }
      setTopics((prev) =>
        prev.map((t) =>
          t.id === topicId
            ? { ...t, upvoteCount: result.upvoteCount, downvoteCount: result.downvoteCount, netVotes: result.netVotes, userVote: value === 0 ? null : value }
            : t
        )
      );
    } catch { /* ignore */ }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim() || newTitle.trim().length < 10) return;
    setSubmitting(true);
    try {
      await createTopic(newTitle.trim(), newDesc.trim() || undefined);
      setNewTitle("");
      setNewDesc("");
      setShowSubmit(false);
      await loadTopics();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-foreground">Proposed Debate Topics</h1>
        {isPremium ? (
          <Button size="sm" className="gap-1.5 text-xs" onClick={() => setShowSubmit(!showSubmit)}>
            <Plus size={13} />
            Propose Topic
          </Button>
        ) : isAuthenticated ? (
          <span className="flex items-center gap-1 text-[11px] text-muted-foreground bg-secondary rounded-full px-3 py-1">
            <Crown size={10} />
            Premium required to submit topics
          </span>
        ) : null}
      </div>

      {!isAuthenticated && (
        <div className="rounded-lg border border-border bg-amber-500/5 px-4 py-3 mb-4 text-xs text-muted-foreground">
          <a href="/login?redirect=/topics" className="text-primary hover:underline font-medium">Log in</a> to vote on topics.
          Premium members can submit new topics.
        </div>
      )}

      {showSubmit && (
        <div className="rounded-xl border border-primary/30 bg-card p-5 mb-6">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-sm font-semibold text-card-foreground">Propose a Debate Topic</h2>
            <button onClick={() => setShowSubmit(false)} className="text-muted-foreground hover:text-foreground">
              <X size={14} />
            </button>
          </div>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <input
              type="text"
              placeholder="Topic title (min 10 characters)"
              value={newTitle}
              onChange={(e) => setNewTitle(e.target.value)}
              minLength={10}
              required
              className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
            <textarea
              placeholder="Optional description or context..."
              value={newDesc}
              onChange={(e) => setNewDesc(e.target.value)}
              rows={2}
              className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground resize-none outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
            <Button type="submit" size="sm" disabled={submitting || newTitle.trim().length < 10} className="self-start text-xs">
              {submitting ? "Submitting..." : "Submit Topic"}
            </Button>
          </form>
        </div>
      )}

      {/* Sort tabs */}
      <div className="flex items-center gap-1 mb-4">
        {([
          { key: "hot" as const, label: "Hot", icon: Flame },
          { key: "new" as const, label: "New", icon: Clock },
          { key: "top" as const, label: "Top", icon: Trophy },
        ]).map(({ key, label, icon: Icon }) => (
          <Button
            key={key}
            variant="ghost"
            size="sm"
            onClick={() => setSortBy(key)}
            className={cn(
              "h-7 text-xs gap-1",
              sortBy === key ? "text-primary font-medium" : "text-muted-foreground"
            )}
          >
            <Icon size={12} /> {label}
          </Button>
        ))}
        <span className="ml-auto text-xs text-muted-foreground">{totalCount} topics</span>
      </div>

      {loading ? (
        <div className="flex flex-col gap-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-5 h-20 animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {topics.map((topic) => (
            <TopicCard
              key={topic.id}
              topic={topic}
              isAuthenticated={isAuthenticated}
              onVote={handleVote}
            />
          ))}
          {topics.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-8">
              No topics proposed yet. {isPremium ? "Be the first!" : ""}
            </p>
          )}
        </div>
      )}
    </main>
  );
}
