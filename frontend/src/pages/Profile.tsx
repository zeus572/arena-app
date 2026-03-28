import { useState, useEffect } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import api, { fetchUserStats, type UserStats } from "@/api/client";
import { User, Shield, Crown, Mail, CheckCircle, AlertCircle, Star, Award, Zap, Target, MessageCircleQuestion, ThumbsUp, Sparkles } from "lucide-react";

const INTEREST_OPTIONS = [
  "Economy & Fiscal Policy",
  "Healthcare",
  "Education",
  "Climate & Environment",
  "National Security",
  "Immigration",
  "Criminal Justice",
  "Technology & AI",
  "Foreign Policy",
  "Civil Rights",
  "Housing",
  "Labor & Workers' Rights",
];

export default function Profile() {
  const { user, refreshUser } = useAuth();
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [politicalLeaning, setPoliticalLeaning] = useState(user?.politicalLeaning ?? "");
  const [avatarUrl, setAvatarUrl] = useState(user?.avatarUrl ?? "");
  const [interests, setInterests] = useState<string[]>(() => {
    try {
      return JSON.parse(user?.politicalLeaning?.match(/\[.*\]/)?.[0] ?? "[]");
    } catch { return []; }
  });
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [verifying, setVerifying] = useState(false);
  const [verifyMsg, setVerifyMsg] = useState<string | null>(null);

  const [stats, setStats] = useState<UserStats | null>(null);

  useEffect(() => {
    fetchUserStats().then(setStats).catch(() => {});
  }, []);

  if (!user) return null;

  const toggleInterest = (interest: string) => {
    setInterests((prev) =>
      prev.includes(interest) ? prev.filter((i) => i !== interest) : [...prev, interest]
    );
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSaved(false);
    try {
      await api.put("/profile/me", {
        displayName: displayName || null,
        politicalLeaning: politicalLeaning || null,
        avatarUrl: avatarUrl || null,
      });
      await refreshUser();
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } finally {
      setSaving(false);
    }
  };

  const handleVerifyEmail = async () => {
    setVerifying(true);
    setVerifyMsg(null);
    try {
      // For MVP, request verification token from a resend endpoint
      // Since we don't have actual email sending, we'll call verify-email with the stored token
      const res = await api.post<{ emailVerifyToken?: string }>("/auth/resend-verification", {});
      if (res.data.emailVerifyToken) {
        // Auto-verify for MVP (no actual email)
        await api.get(`/auth/verify-email?token=${res.data.emailVerifyToken}`);
        await refreshUser();
        setVerifyMsg("Email verified!");
      }
    } catch {
      setVerifyMsg("Verification failed. Please try again.");
    } finally {
      setVerifying(false);
    }
  };

  return (
    <main className="mx-auto max-w-lg px-4 py-8">
      <div className="flex items-center gap-2 mb-6">
        <User size={20} className="text-primary" />
        <h1 className="text-xl font-bold text-foreground">Profile</h1>
      </div>

      {/* Account card */}
      <div className="rounded-xl border border-border bg-card p-6 mb-4">
        <div className="flex items-center gap-3 mb-6">
          <div className="h-14 w-14 rounded-full bg-primary/10 flex items-center justify-center text-primary font-bold text-xl">
            {(user.displayName ?? user.email)[0].toUpperCase()}
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-card-foreground">{user.displayName ?? user.email}</p>
            <p className="text-xs text-muted-foreground">{user.email}</p>
          </div>
          <span className="flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-semibold bg-primary/10 text-primary">
            {user.plan === "Premium" ? <Crown size={10} /> : <Shield size={10} />}
            {user.plan}
          </span>
        </div>

        {/* Email verification banner */}
        {!user.emailVerified && (
          <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-3 mb-5 flex items-start gap-3">
            <AlertCircle size={16} className="text-amber-500 shrink-0 mt-0.5" />
            <div className="flex-1">
              <p className="text-xs font-semibold text-amber-600 dark:text-amber-400">Email not verified</p>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Verify your email to unlock all features.
              </p>
              {verifyMsg && (
                <p className="text-[11px] text-green-600 mt-1">{verifyMsg}</p>
              )}
            </div>
            <Button
              variant="outline"
              size="sm"
              disabled={verifying}
              onClick={handleVerifyEmail}
              className="text-[10px] h-7 gap-1 shrink-0"
            >
              <Mail size={10} />
              {verifying ? "Verifying..." : "Verify Now"}
            </Button>
          </div>
        )}

        {user.emailVerified && (
          <div className="rounded-lg bg-green-500/5 border border-green-500/20 p-2.5 mb-5 flex items-center gap-2">
            <CheckCircle size={14} className="text-green-600" />
            <span className="text-[11px] font-medium text-green-600">Email verified</span>
          </div>
        )}

        <form onSubmit={handleSave} className="flex flex-col gap-4">
          <div>
            <label className="text-xs font-medium text-muted-foreground mb-1 block">Display Name</label>
            <input
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="Your name"
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>

          <div>
            <label className="text-xs font-medium text-muted-foreground mb-1 block">Political Leaning</label>
            <select
              value={politicalLeaning}
              onChange={(e) => setPoliticalLeaning(e.target.value)}
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">Select...</option>
              <option value="Very Progressive">Very Progressive</option>
              <option value="Progressive">Progressive</option>
              <option value="Center-Left">Center-Left</option>
              <option value="Moderate">Moderate</option>
              <option value="Center-Right">Center-Right</option>
              <option value="Conservative">Conservative</option>
              <option value="Very Conservative">Very Conservative</option>
              <option value="Libertarian">Libertarian</option>
              <option value="Independent">Independent</option>
            </select>
          </div>

          <div>
            <label className="text-xs font-medium text-muted-foreground mb-1.5 block">Interests</label>
            <p className="text-[11px] text-muted-foreground mb-2">Select topics you care about to help surface relevant debates.</p>
            <div className="flex flex-wrap gap-2">
              {INTEREST_OPTIONS.map((interest) => (
                <button
                  key={interest}
                  type="button"
                  onClick={() => toggleInterest(interest)}
                  className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                    interests.includes(interest)
                      ? "bg-primary text-primary-foreground"
                      : "bg-secondary text-secondary-foreground hover:bg-primary/10"
                  }`}
                >
                  {interest}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="text-xs font-medium text-muted-foreground mb-1 block">Avatar URL</label>
            <input
              type="url"
              value={avatarUrl}
              onChange={(e) => setAvatarUrl(e.target.value)}
              placeholder="https://..."
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>

          <Button type="submit" disabled={saving} size="sm" className="self-start text-xs">
            {saving ? "Saving..." : saved ? "Saved!" : "Save Changes"}
          </Button>
        </form>
      </div>

      {/* XP & Gamification */}
      {stats && (
        <>
          <div className="rounded-xl border border-border bg-card p-6 mb-4">
            <div className="flex items-center gap-2 mb-4">
              <Zap size={16} className="text-amber-500" />
              <h2 className="text-sm font-bold text-card-foreground">Level & XP</h2>
            </div>

            <div className="flex items-center gap-4 mb-4">
              <div className="flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-amber-500 to-orange-600 text-white font-bold text-xl shadow-lg">
                {stats.level}
              </div>
              <div className="flex-1">
                <p className="text-sm font-semibold text-card-foreground">{stats.title}</p>
                <p className="text-xs text-muted-foreground">{stats.xp.toLocaleString()} XP</p>
                <div className="mt-1.5 h-2 w-full rounded-full bg-secondary overflow-hidden">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-amber-500 to-orange-500 transition-all duration-500"
                    style={{ width: `${stats.xpProgress}%` }}
                  />
                </div>
                <p className="text-[10px] text-muted-foreground mt-0.5">
                  {stats.xpForNextLevel - stats.xp} XP to next level
                </p>
              </div>
            </div>

            {/* Activity breakdown */}
            <div className="grid grid-cols-3 gap-3 mt-4">
              {[
                { label: "Votes", value: stats.activity.votes, icon: ThumbsUp },
                { label: "Reactions", value: stats.activity.reactions, icon: Sparkles },
                { label: "Debates", value: stats.activity.debatesStarted, icon: Star },
                { label: "Predictions", value: stats.activity.predictions, icon: Target },
                { label: "Correct", value: stats.activity.correctPredictions, icon: CheckCircle },
                { label: "Questions", value: stats.activity.interventions, icon: MessageCircleQuestion },
              ].map(({ label, value, icon: Icon }) => (
                <div key={label} className="rounded-lg bg-secondary/50 p-2.5 text-center">
                  <Icon size={14} className="mx-auto text-muted-foreground mb-1" />
                  <p className="text-lg font-bold text-foreground tabular-nums">{value}</p>
                  <p className="text-[10px] text-muted-foreground">{label}</p>
                </div>
              ))}
            </div>
          </div>

          {/* Badges */}
          {stats.badges.length > 0 && (
            <div className="rounded-xl border border-border bg-card p-6 mb-4">
              <div className="flex items-center gap-2 mb-4">
                <Award size={16} className="text-purple-500" />
                <h2 className="text-sm font-bold text-card-foreground">Badges</h2>
                <span className="text-[10px] text-muted-foreground ml-auto">{stats.badges.length} earned</span>
              </div>
              <div className="grid grid-cols-2 gap-2">
                {stats.badges.map((badge) => (
                  <div
                    key={badge.id}
                    className="rounded-lg border border-border bg-secondary/30 p-3 flex items-start gap-2"
                  >
                    <div className="h-8 w-8 rounded-full bg-purple-500/10 flex items-center justify-center shrink-0">
                      <Award size={14} className="text-purple-500" />
                    </div>
                    <div>
                      <p className="text-xs font-semibold text-card-foreground">{badge.name}</p>
                      <p className="text-[10px] text-muted-foreground">{badge.description}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </main>
  );
}
