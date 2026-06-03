import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Users, Copy, Check, Trophy, Crown, Plus, LogOut } from "lucide-react";
import {
  getLeague,
  createInvite,
  listInvites,
  revokeInvite,
  linkCampaign,
  leaveLeague,
  openRound,
  type LeagueDetail as LeagueDetailT,
  type LeagueInvite,
  type LeagueStanding,
} from "@/api/leagues";
import { listCampaigns, type CivicCampaignSummary } from "@/api/campaignManager";
import { getBriefings } from "@/api/briefings";
import type { CivicBriefingSummary } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { CandidateAvatar } from "../components/CandidateAvatar";
import { SignInPrompt } from "../components/SignInPrompt";

export default function LeagueDetail() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated, isLoading } = useAuth();
  const [league, setLeague] = useState<LeagueDetailT | undefined | null>(null);

  const refresh = useCallback(async () => {
    const l = await getLeague(id);
    setLeague(l ?? undefined);
  }, [id]);

  useEffect(() => {
    if (!isAuthenticated) return;
    void refresh();
  }, [isAuthenticated, refresh]);

  if (!isLoading && !isAuthenticated) {
    return (
      <section className="mx-auto max-w-lg">
        <SignInPrompt title="Sign in to view this league" />
      </section>
    );
  }
  if (league === null) {
    return <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">Loading…</p>;
  }
  if (league === undefined) {
    return (
      <section data-testid="league-missing">
        <h1 className="display text-3xl">League not found</h1>
        <p className="mt-2 text-[var(--fg-soft)]">
          It may have been deleted, or you're not a member.{" "}
          <Link to="/leagues" className="font-semibold text-[var(--accent)]">Back to leagues</Link>
        </p>
      </section>
    );
  }

  const isOwner = league.myRole === "Owner";

  return (
    <section data-testid="league-detail-page" className="space-y-8">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Link to="/leagues" className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]">
            ← Leagues
          </Link>
          <h1 className="display mt-1 text-4xl">{league.name}</h1>
          {league.description && <p className="mt-1 text-[var(--fg-soft)]">{league.description}</p>}
          <p className="mt-2 inline-flex items-center gap-1 text-sm text-[var(--fg-soft)]">
            <Users className="h-4 w-4" /> {league.members.length}/{league.maxMembers} members · Season {league.seasonNumber}
          </p>
        </div>
        {!isOwner && (
          <button
            type="button"
            onClick={async () => {
              await leaveLeague(id);
              navigate("/leagues");
            }}
            data-testid="leave-league"
            className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-3 py-1.5 text-sm font-semibold text-[var(--fg-soft)] hover:border-red-400 hover:text-red-600"
          >
            <LogOut className="h-4 w-4" /> Leave
          </button>
        )}
      </header>

      <LinkCampaignPanel league={league} onLinked={refresh} />

      <RoundPanel league={league} isOwner={isOwner} onOpened={refresh} />

      <Standings standings={league.standings} />

      {isOwner && <InvitePanel leagueId={id} />}

      <MemberList league={league} />
    </section>
  );
}

// ---------------------------------------------------------------- Standings

function Standings({ standings }: { standings: LeagueStanding[] }) {
  return (
    <div data-testid="standings">
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">Season standings</h2>
      <p className="mt-1 text-xs text-[var(--muted)]">
        League score = round points (peer votes) + campaign performance (your candidate's support).
      </p>
      <div className="mt-3 overflow-x-auto border border-[var(--border)] bg-[var(--bg-elev)]">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-[var(--border)] text-left text-[var(--muted)]">
              <th className="px-3 py-2 font-semibold">#</th>
              <th className="px-3 py-2 font-semibold">Player</th>
              <th className="px-3 py-2 text-right font-semibold">Rounds</th>
              <th className="px-3 py-2 text-right font-semibold">Campaign</th>
              <th className="px-3 py-2 text-right font-semibold">Score</th>
            </tr>
          </thead>
          <tbody>
            {standings.map((s) => (
              <tr
                key={s.memberId}
                data-testid="standing-row"
                className={s.isMe ? "border-b border-[var(--border)] bg-[var(--accent)]/5" : "border-b border-[var(--border)]"}
              >
                <td className="px-3 py-2 font-semibold text-[var(--fg)]">{s.rank}</td>
                <td className="px-3 py-2">
                  <span className="font-semibold text-[var(--fg)]">{s.displayName}</span>
                  {s.isMe && <span className="ml-1 text-xs text-[var(--accent)]">(you)</span>}
                  <span className="block text-xs text-[var(--muted)]">
                    {s.candidateName ? `${s.candidateName}${s.party ? ` · ${s.party}` : ""}` : "No campaign linked"}
                    {s.supportShare != null && ` · ${s.supportShare.toFixed(1)}%`}
                  </span>
                </td>
                <td className="px-3 py-2 text-right text-[var(--fg-soft)]">{s.roundPoints}</td>
                <td className="px-3 py-2 text-right text-[var(--fg-soft)]">{s.campaignScore}</td>
                <td className="px-3 py-2 text-right text-lg font-bold text-[var(--fg)]">{s.leagueScore}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- Members

function MemberList({ league }: { league: LeagueDetailT }) {
  return (
    <div data-testid="member-list">
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">Members</h2>
      <ul className="mt-3 grid gap-2 sm:grid-cols-2">
        {league.members.map((m) => (
          <li key={m.id} className="flex items-center gap-3 border border-[var(--border)] bg-[var(--bg-elev)] p-3">
            {m.candidateSlug ? (
              <CandidateAvatar candidate={{ slug: m.candidateSlug, name: m.candidateName ?? m.displayName, avatarBaseUrl: "" }} size={40} />
            ) : (
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--border)] text-sm font-semibold text-[var(--fg-soft)]">
                {m.displayName.slice(0, 1).toUpperCase()}
              </span>
            )}
            <span className="min-w-0">
              <span className="flex items-center gap-1 font-semibold text-[var(--fg)]">
                {m.displayName}
                {m.role === "Owner" && <Crown className="h-3.5 w-3.5 text-[var(--accent)]" />}
              </span>
              <span className="block truncate text-xs text-[var(--muted)]">
                {m.candidateName ? `${m.candidateName}${m.party ? ` · ${m.party}` : ""}` : "No campaign linked"}
              </span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

// ---------------------------------------------------------------- Link campaign

function LinkCampaignPanel({ league, onLinked }: { league: LeagueDetailT; onLinked: () => Promise<void> }) {
  const [campaigns, setCampaigns] = useState<CivicCampaignSummary[]>([]);
  const [selected, setSelected] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void listCampaigns().then(setCampaigns).catch(() => setCampaigns([]));
  }, []);

  const linked = campaigns.find((c) => c.id === league.myCampaignId);

  async function onLink() {
    if (!selected || busy) return;
    setBusy(true);
    try {
      await linkCampaign(league.id, selected);
      await onLinked();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="link-campaign-panel">
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">Your candidate</h2>
      {league.myCampaignId ? (
        <p className="mt-1 text-sm text-[var(--fg-soft)]" data-testid="linked-campaign">
          You're fielding{" "}
          <span className="font-semibold text-[var(--fg)]">{linked?.candidateName ?? "your linked candidate"}</span>
          {linked && ` (${linked.raceLabel})`}. Change it below.
        </p>
      ) : (
        <p className="mt-1 text-sm text-[var(--fg-soft)]">
          Link one of your campaigns so its candidate represents you in this league's rounds and leaderboard.
        </p>
      )}
      {campaigns.length === 0 ? (
        <p className="mt-3 text-sm text-[var(--muted)]">
          You don't have a campaign yet.{" "}
          <Link to="/campaigns/new" className="font-semibold text-[var(--accent)]">Start one →</Link>
        </p>
      ) : (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <select
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
            data-testid="campaign-select"
            className="border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--accent)]"
          >
            <option value="">Choose a campaign…</option>
            {campaigns.map((c) => (
              <option key={c.id} value={c.id}>
                {c.candidateName} — {c.raceLabel}
              </option>
            ))}
          </select>
          <button
            type="button"
            onClick={onLink}
            disabled={!selected || busy}
            data-testid="link-campaign-submit"
            className="rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {busy ? "Linking…" : "Link campaign"}
          </button>
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- Rounds

function RoundPanel({
  league,
  isOwner,
  onOpened,
}: {
  league: LeagueDetailT;
  isOwner: boolean;
  onOpened: () => Promise<void>;
}) {
  const navigate = useNavigate();
  const [briefings, setBriefings] = useState<CivicBriefingSummary[]>([]);
  const [slug, setSlug] = useState("");
  const [busy, setBusy] = useState(false);
  const active = league.activeRound;

  useEffect(() => {
    if (isOwner && !active) void getBriefings().then(setBriefings).catch(() => setBriefings([]));
  }, [isOwner, active]);

  async function onOpen() {
    if (!slug || busy) return;
    setBusy(true);
    try {
      const round = await openRound(league.id, { briefingSlug: slug });
      await onOpened();
      navigate(`/leagues/${league.id}/rounds/${round.id}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="round-panel">
      <h2 className="flex items-center gap-1 text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">
        <Trophy className="h-4 w-4" /> Rounds
      </h2>

      {active ? (
        <Link
          to={`/leagues/${league.id}/rounds/${active.id}`}
          data-testid="active-round"
          className="mt-3 block border border-[var(--accent)] bg-[var(--accent)]/5 p-4 transition hover:bg-[var(--accent)]/10"
        >
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
            Round {active.roundNumber} · {labelForStatus(active.status)}
          </span>
          <span className="mt-1 block font-semibold text-[var(--fg)]">{active.headline}</span>
          <span className="mt-1 block text-sm text-[var(--fg-soft)]">
            {active.entryCount} {active.entryCount === 1 ? "entry" : "entries"} ·{" "}
            {active.iHaveEntered ? "You've entered" : "Open it to take part →"}
          </span>
        </Link>
      ) : isOwner ? (
        <div className="mt-3">
          <p className="text-sm text-[var(--fg-soft)]">
            Drop a round: pick a news story and everyone responds with their candidate, then you all vote.
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <select
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              data-testid="briefing-select"
              className="max-w-full border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--accent)]"
            >
              <option value="">Choose a news item…</option>
              {briefings.map((b) => (
                <option key={b.slug} value={b.slug}>
                  {b.headline}
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={onOpen}
              disabled={!slug || busy}
              data-testid="open-round-submit"
              className="inline-flex items-center gap-1 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
            >
              <Plus className="h-4 w-4" /> {busy ? "Opening…" : "Open round"}
            </button>
          </div>
        </div>
      ) : (
        <p className="mt-3 text-sm text-[var(--muted)]">No round running. The owner can start one.</p>
      )}

      {/* Past rounds */}
      {league.rounds.filter((r) => r.status === "Closed").length > 0 && (
        <ul className="mt-4 space-y-2" data-testid="past-rounds">
          {league.rounds
            .filter((r) => r.status === "Closed")
            .map((r) => (
              <li key={r.id}>
                <Link
                  to={`/leagues/${league.id}/rounds/${r.id}`}
                  className="flex items-center justify-between gap-2 border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm transition hover:border-[var(--accent)]"
                >
                  <span className="min-w-0">
                    <span className="font-semibold text-[var(--fg)]">Round {r.roundNumber}</span>
                    <span className="ml-2 text-[var(--muted)]">{r.headline}</span>
                  </span>
                  {r.winnerDisplayName && (
                    <span className="shrink-0 text-xs font-semibold text-[var(--accent)]">🏆 {r.winnerDisplayName}</span>
                  )}
                </Link>
              </li>
            ))}
        </ul>
      )}
    </div>
  );
}

function labelForStatus(status: string): string {
  if (status === "OpenForResponses") return "Responses open";
  if (status === "Voting") return "Voting open";
  return "Closed";
}

// ---------------------------------------------------------------- Invites

function InvitePanel({ leagueId }: { leagueId: string }) {
  const [invites, setInvites] = useState<LeagueInvite[]>([]);
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState<string | null>(null);

  const load = useCallback(() => {
    void listInvites(leagueId).then(setInvites).catch(() => setInvites([]));
  }, [leagueId]);

  useEffect(load, [load]);

  async function generate() {
    if (busy) return;
    setBusy(true);
    try {
      await createInvite(leagueId, {});
      load();
    } finally {
      setBusy(false);
    }
  }

  async function copy(code: string) {
    const url = `${window.location.origin}/leagues/join/${code}`;
    try {
      await navigator.clipboard.writeText(url);
      setCopied(code);
      setTimeout(() => setCopied((c) => (c === code ? null : c)), 1500);
    } catch {
      /* clipboard blocked — the link is still visible to copy manually */
    }
  }

  const active = invites.filter((i) => i.isValid);

  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="invite-panel">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">Invite friends</h2>
        <button
          type="button"
          onClick={generate}
          disabled={busy}
          data-testid="generate-invite"
          className="inline-flex items-center gap-1 rounded-full bg-[var(--accent)] px-3 py-1.5 text-sm font-semibold text-white disabled:opacity-50"
        >
          <Plus className="h-4 w-4" /> New link
        </button>
      </div>

      {active.length === 0 ? (
        <p className="mt-3 text-sm text-[var(--muted)]">
          No active invite links. Generate one and share it with your friends.
        </p>
      ) : (
        <ul className="mt-3 space-y-2" data-testid="invite-list">
          {active.map((i) => (
            <li
              key={i.id}
              className="flex flex-wrap items-center gap-2 border border-[var(--border)] bg-[var(--bg)] px-3 py-2"
            >
              <code className="font-mono text-sm text-[var(--fg)]" data-testid="invite-code">
                {`${window.location.origin}/leagues/join/${i.code}`}
              </code>
              <span className="text-xs text-[var(--muted)]">
                {i.useCount} use{i.useCount === 1 ? "" : "s"}
                {i.maxUses != null && ` / ${i.maxUses}`}
              </span>
              <div className="ml-auto flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => copy(i.code)}
                  data-testid="copy-invite"
                  className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-2 py-1 text-xs font-semibold text-[var(--fg-soft)] hover:border-[var(--accent)]"
                >
                  {copied === i.code ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
                  {copied === i.code ? "Copied" : "Copy"}
                </button>
                <button
                  type="button"
                  onClick={async () => {
                    await revokeInvite(leagueId, i.id);
                    load();
                  }}
                  data-testid="revoke-invite"
                  className="rounded-full border border-[var(--border)] px-2 py-1 text-xs font-semibold text-[var(--fg-soft)] hover:border-red-400 hover:text-red-600"
                >
                  Revoke
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
