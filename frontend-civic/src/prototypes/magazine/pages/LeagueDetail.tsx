import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Users, Copy, Check, Trophy, Crown, Plus, LogOut, Mail, Link2, Send } from "lucide-react";
import {
  getLeague,
  createInvite,
  listInvites,
  revokeInvite,
  inviteByEmail,
  linkCampaign,
  leaveLeague,
  openRound,
  type LeagueDetail as LeagueDetailT,
  type LeagueInvite,
  type EmailInviteResult,
  type LeagueStanding,
} from "@/api/leagues";
import { listCampaigns, type CivicCampaignSummary } from "@/api/campaignManager";
import { getBriefings } from "@/api/briefings";
import type { CivicBriefingSummary } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { CandidateAvatar } from "../components/CandidateAvatar";
import { SignInPrompt } from "../components/SignInPrompt";
import { Button } from "../components/Button";

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
          <Button
            variant="danger"
            size="sm"
            onClick={async () => {
              await leaveLeague(id);
              navigate("/leagues");
            }}
            data-testid="leave-league"
          >
            <LogOut className="h-4 w-4" /> Leave
          </Button>
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
          <Button onClick={onLink} disabled={!selected || busy} data-testid="link-campaign-submit">
            {busy ? "Linking…" : "Link campaign"}
          </Button>
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
    if (isOwner && !active)
      void getBriefings(1, 100)
        .then((p) => setBriefings(p.items))
        .catch(() => setBriefings([]));
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
            <Button onClick={onOpen} disabled={!slug || busy} data-testid="open-round-submit">
              <Plus className="h-4 w-4" /> {busy ? "Opening…" : "Open round"}
            </Button>
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

function joinUrl(code: string): string {
  return `${window.location.origin}/leagues/join/${code}`;
}

function InvitePanel({ leagueId }: { leagueId: string }) {
  const [invites, setInvites] = useState<LeagueInvite[]>([]);
  const [tab, setTab] = useState<"link" | "email">("link");
  const [copied, setCopied] = useState<string | null>(null);

  const load = useCallback(() => {
    void listInvites(leagueId).then(setInvites).catch(() => setInvites([]));
  }, [leagueId]);

  useEffect(load, [load]);

  async function copy(code: string) {
    try {
      await navigator.clipboard.writeText(joinUrl(code));
      setCopied(code);
      setTimeout(() => setCopied((c) => (c === code ? null : c)), 1500);
    } catch {
      /* clipboard blocked — the link is still visible to copy manually */
    }
  }

  async function revoke(inviteId: string) {
    await revokeInvite(leagueId, inviteId);
    load();
  }

  return (
    <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-4" data-testid="invite-panel">
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">Invite friends</h2>

      <div className="mt-3 inline-flex rounded-full border border-[var(--border)] p-0.5" role="tablist">
        <TabButton active={tab === "link"} onClick={() => setTab("link")} testId="invite-tab-link">
          <Link2 className="h-3.5 w-3.5" /> Share a link
        </TabButton>
        <TabButton active={tab === "email"} onClick={() => setTab("email")} testId="invite-tab-email">
          <Mail className="h-3.5 w-3.5" /> By email
        </TabButton>
      </div>

      {tab === "link" ? (
        <LinkInvites
          leagueId={leagueId}
          invites={invites}
          copied={copied}
          onCopy={copy}
          onRevoke={revoke}
          onChanged={load}
        />
      ) : (
        <EmailInvites
          leagueId={leagueId}
          invites={invites}
          copied={copied}
          onCopy={copy}
          onRevoke={revoke}
          onChanged={load}
        />
      )}
    </div>
  );
}

function TabButton({
  active,
  onClick,
  testId,
  children,
}: {
  active: boolean;
  onClick: () => void;
  testId: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      data-testid={testId}
      className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-semibold transition ${
        active ? "bg-[var(--accent)] text-white" : "text-[var(--fg-soft)] hover:text-[var(--fg)]"
      }`}
    >
      {children}
    </button>
  );
}

// ---- Link invites: open, shareable codes ----

function LinkInvites({
  leagueId,
  invites,
  copied,
  onCopy,
  onRevoke,
  onChanged,
}: {
  leagueId: string;
  invites: LeagueInvite[];
  copied: string | null;
  onCopy: (code: string) => void;
  onRevoke: (inviteId: string) => Promise<void>;
  onChanged: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const links = invites.filter((i) => i.email === null && i.isValid);

  async function generate() {
    if (busy) return;
    setBusy(true);
    try {
      await createInvite(leagueId, {});
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-4">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm text-[var(--fg-soft)]">
          Anyone with the link can join — share it in your group chat.
        </p>
        <Button onClick={generate} disabled={busy} data-testid="generate-invite" className="shrink-0">
          <Plus className="h-4 w-4" /> New link
        </Button>
      </div>

      {links.length === 0 ? (
        <p className="mt-3 text-sm text-[var(--muted)]">
          No active invite links. Generate one and share it with your friends.
        </p>
      ) : (
        <ul className="mt-3 space-y-2" data-testid="invite-list">
          {links.map((i) => (
            <li
              key={i.id}
              className="flex flex-wrap items-center gap-2 border border-[var(--border)] bg-[var(--bg)] px-3 py-2"
            >
              <code className="font-mono text-sm text-[var(--fg)]" data-testid="invite-code">
                {joinUrl(i.code)}
              </code>
              <span className="text-xs text-[var(--muted)]">
                {i.useCount} use{i.useCount === 1 ? "" : "s"}
                {i.maxUses != null && ` / ${i.maxUses}`}
              </span>
              <div className="ml-auto flex items-center gap-1">
                <CopyButton code={i.code} copied={copied} onCopy={onCopy} />
                <RevokeButton onClick={() => onRevoke(i.id)} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ---- Email invites: personal, single-use invites by address ----

function EmailInvites({
  leagueId,
  invites,
  copied,
  onCopy,
  onRevoke,
  onChanged,
}: {
  leagueId: string;
  invites: LeagueInvite[];
  copied: string | null;
  onCopy: (code: string) => void;
  onRevoke: (inviteId: string) => Promise<void>;
  onChanged: () => void;
}) {
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [results, setResults] = useState<EmailInviteResult[] | null>(null);

  // Personal invites (email-targeted): keep pending + accepted, drop revoked ones (isValid false
  // with no acceptance). Accepted invites sort to the bottom.
  const personal = invites
    .filter((i) => i.email !== null && (i.accepted || i.isValid))
    .sort((a, b) => Number(a.accepted) - Number(b.accepted));

  function parseEmails(raw: string): string[] {
    return raw
      .split(/[\s,;]+/)
      .map((e) => e.trim())
      .filter(Boolean);
  }

  const parsed = parseEmails(text);

  async function send() {
    if (busy || parsed.length === 0) return;
    setBusy(true);
    try {
      const res = await inviteByEmail(leagueId, parsed);
      setResults(res);
      // Clear the addresses we successfully invited; keep any invalid ones for fixing.
      const invalid = res.filter((r) => r.status === "invalid").map((r) => r.email);
      setText(invalid.join("\n"));
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-4">
      <p className="text-sm text-[var(--fg-soft)]">
        Invite friends by email — each gets a personal, single-use join link to copy and send.
      </p>
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={2}
        placeholder="friend@example.com, another@example.com"
        data-testid="email-invite-input"
        className="mt-2 w-full resize-y border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--accent)]"
      />
      <div className="mt-2 flex items-center justify-between gap-2">
        <span className="text-xs text-[var(--muted)]">
          {parsed.length > 0 ? `${parsed.length} address${parsed.length === 1 ? "" : "es"}` : "Separate with commas or new lines"}
        </span>
        <Button onClick={send} disabled={busy || parsed.length === 0} data-testid="email-invite-submit">
          <Send className="h-4 w-4" /> {busy ? "Inviting…" : "Create invites"}
        </Button>
      </div>

      {results && results.length > 0 && (
        <ul className="mt-3 space-y-1" data-testid="email-invite-results">
          {results.map((r, idx) => (
            <li key={`${r.email}-${idx}`} className="flex items-center gap-2 text-sm">
              <ResultBadge status={r.status} />
              <span className="text-[var(--fg-soft)]">{r.email}</span>
            </li>
          ))}
        </ul>
      )}

      {personal.length > 0 && (
        <ul className="mt-4 space-y-2" data-testid="email-invite-list">
          {personal.map((i) => (
            <li
              key={i.id}
              className="flex flex-wrap items-center gap-2 border border-[var(--border)] bg-[var(--bg)] px-3 py-2"
            >
              <span className="flex min-w-0 items-center gap-2">
                <span className="truncate font-semibold text-[var(--fg)]" data-testid="email-invite-address">
                  {i.email}
                </span>
                {i.accepted ? (
                  <span className="inline-flex items-center gap-1 rounded-full bg-green-500/10 px-2 py-0.5 text-xs font-semibold text-green-600">
                    <Check className="h-3 w-3" /> Joined
                  </span>
                ) : (
                  <span className="rounded-full bg-[var(--border)] px-2 py-0.5 text-xs font-semibold text-[var(--fg-soft)]">
                    Pending
                  </span>
                )}
              </span>
              {!i.accepted && (
                <div className="ml-auto flex items-center gap-1">
                  <CopyButton code={i.code} copied={copied} onCopy={onCopy} label="Copy link" />
                  <RevokeButton onClick={() => onRevoke(i.id)} />
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ResultBadge({ status }: { status: EmailInviteResult["status"] }) {
  const map: Record<EmailInviteResult["status"], { label: string; cls: string }> = {
    invited: { label: "Invited", cls: "bg-green-500/10 text-green-600" },
    already_invited: { label: "Already invited", cls: "bg-[var(--accent)]/10 text-[var(--accent)]" },
    already_member: { label: "Already a member", cls: "bg-[var(--border)] text-[var(--fg-soft)]" },
    invalid: { label: "Invalid", cls: "bg-red-500/10 text-red-600" },
  };
  const { label, cls } = map[status];
  return <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${cls}`}>{label}</span>;
}

function CopyButton({
  code,
  copied,
  onCopy,
  label = "Copy",
}: {
  code: string;
  copied: string | null;
  onCopy: (code: string) => void;
  label?: string;
}) {
  return (
    <Button variant="ghost" size="sm" onClick={() => onCopy(code)} data-testid="copy-invite">
      {copied === code ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
      {copied === code ? "Copied" : label}
    </Button>
  );
}

function RevokeButton({ onClick }: { onClick: () => void }) {
  return (
    <Button variant="danger" size="sm" onClick={onClick} data-testid="revoke-invite">
      Revoke
    </Button>
  );
}
