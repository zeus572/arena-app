import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Users } from "lucide-react";
import { previewInvite, joinByCode, type LeagueInvitePreview } from "@/api/leagues";
import { useAuth } from "@/auth/AuthContext";
import { SignInPrompt } from "../components/SignInPrompt";

export default function LeagueJoin() {
  const { code = "" } = useParams();
  const { user, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const [preview, setPreview] = useState<LeagueInvitePreview | undefined | null>(null);
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated) return;
    void previewInvite(code).then(setPreview);
  }, [isAuthenticated, code]);

  async function onJoin() {
    if (joining) return;
    setJoining(true);
    setError(null);
    try {
      const league = await joinByCode(code, {
        displayName: user?.displayName ?? undefined,
        email: user?.email ?? undefined,
        avatarUrl: user?.avatarUrl ?? undefined,
      });
      navigate(`/leagues/${league.id}`);
    } catch (err) {
      setError(
        (err as { response?: { data?: { error?: string } } }).response?.data?.error ??
          "Couldn't join this league.",
      );
      setJoining(false);
    }
  }

  return (
    <section className="mx-auto max-w-lg" data-testid="league-join-page">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
        League invite
      </p>
      <h1 className="display mt-1 text-4xl">You're invited</h1>

      {!isLoading && !isAuthenticated ? (
        <div className="mt-6">
          <SignInPrompt
            title="Sign in to join this league"
            message="Create a free account (or sign in) and you'll land right back here to join."
          />
        </div>
      ) : preview === null ? (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Loading…
        </p>
      ) : preview === undefined ? (
        <Card>
          <p className="text-lg font-semibold text-[var(--fg)]">Invite not found</p>
          <p className="mt-1 text-sm text-[var(--fg-soft)]">
            This invite link doesn't look right. Ask the league owner for a fresh link.
          </p>
        </Card>
      ) : preview.alreadyMember ? (
        <Card>
          <p className="text-lg font-semibold text-[var(--fg)]">
            You're already in {preview.leagueName}
          </p>
          <ActionButton onClick={onJoin} disabled={joining} label="Go to the league" />
        </Card>
      ) : !preview.isValid ? (
        <Card>
          <p className="text-lg font-semibold text-[var(--fg)]">This invite isn't usable</p>
          <p className="mt-1 text-sm text-[var(--fg-soft)]" data-testid="invite-invalid-reason">
            {preview.reason ?? "This invite is no longer valid."}
          </p>
        </Card>
      ) : preview.isFull ? (
        <Card>
          <p className="text-lg font-semibold text-[var(--fg)]">{preview.leagueName} is full</p>
          <p className="mt-1 text-sm text-[var(--fg-soft)]">
            This league has reached its {preview.maxMembers}-member cap.
          </p>
        </Card>
      ) : (
        <Card>
          <p className="display text-2xl">{preview.leagueName}</p>
          <p className="mt-1 inline-flex items-center gap-1 text-sm text-[var(--fg-soft)]">
            <Users className="h-4 w-4" /> {preview.memberCount}/{preview.maxMembers} members
          </p>
          {preview.inviterDisplayName && (
            <p className="mt-3 text-sm text-[var(--fg-soft)]" data-testid="invite-from">
              Invited by{" "}
              <span className="font-semibold text-[var(--fg)]">{preview.inviterDisplayName}</span>
              {preview.inviterEmail && (
                <span className="text-[var(--muted)]"> · {preview.inviterEmail}</span>
              )}
            </p>
          )}
          {error && (
            <p className="mt-3 text-sm text-red-600" data-testid="join-error">
              {error}
            </p>
          )}
          <ActionButton onClick={onJoin} disabled={joining} label={joining ? "Joining…" : "Join league"} />
        </Card>
      )}
    </section>
  );
}

function Card({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-6 border border-[var(--border)] bg-[var(--bg-elev)] p-6" data-testid="join-card">
      {children}
    </div>
  );
}

function ActionButton({ onClick, disabled, label }: { onClick: () => void; disabled: boolean; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      data-testid="join-submit"
      className="mt-4 inline-flex w-full items-center justify-center rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white disabled:opacity-50"
    >
      {label}
    </button>
  );
}
