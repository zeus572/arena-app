import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { ChevronLeft, ChevronRight, Megaphone } from "lucide-react";
import type { CivicBriefingSummary, Concept } from "@/api/types";
import { getBriefings } from "@/api/briefings";
import { localityLabel } from "@/api/profile";
import { getConcepts } from "@/api/concepts";
import { fetchBudgetFacts, type BudgetFact } from "@/api/budgetFacts";
import { listCampaigns, type CivicCampaignSummary } from "@/api/campaignManager";
import { useAuth } from "@/auth/AuthContext";
import { DEBATE_ARENA_URL } from "@/lib/links";
import { ButtonLink } from "../components/Button";
import { CandidateAvatar } from "../components/CandidateAvatar";
import { CoverStory } from "../components/CoverStory";
import { CountdownTimer } from "../components/CountdownTimer";
import { PullQuote } from "../components/PullQuote";
import { BudgetFactCard } from "../components/BudgetFactCard";

// Two-column grid → 10 rows max per page. The cover takes one slot on page 1,
// so page 1 shows the cover + (PAGE_SIZE - 1) explainers; later pages show a
// full PAGE_SIZE of explainers.
const PAGE_SIZE = 20;

function formatStoryDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

export default function MagazineHome() {
  const { user, isAuthenticated } = useAuth();
  const [cover, setCover] = useState<CivicBriefingSummary | null>(null);
  const [explainers, setExplainers] = useState<CivicBriefingSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [concept, setConcept] = useState<Concept | null>(null);
  const [budgetFacts, setBudgetFacts] = useState<BudgetFact[]>([]);
  const [activeCampaign, setActiveCampaign] = useState<CivicCampaignSummary | null>(null);
  const [loaded, setLoaded] = useState(false);
  const explainersRef = useRef<HTMLElement>(null);
  const didMountRef = useRef(false);

  useEffect(() => {
    void getBriefings(page, PAGE_SIZE)
      .then((p) => {
        setTotal(p.total);
        if (page === 1) {
          setCover(p.items[0] ?? null);
          setExplainers(p.items.slice(1));
        } else {
          setExplainers(p.items);
        }
      })
      .finally(() => setLoaded(true));
  }, [page]);

  // On page change (not the first load), bring the explainers section into view.
  useEffect(() => {
    if (!didMountRef.current) {
      didMountRef.current = true;
      return;
    }
    explainersRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [page]);

  useEffect(() => {
    void getConcepts().then((cs) => setConcept(cs[0] ?? null));
  }, []);

  useEffect(() => {
    void fetchBudgetFacts()
      .then(setBudgetFacts)
      .catch(() => {});
  }, []);

  // Once the player has picked a candidate to manage (an active campaign), the
  // "pick a candidate" CTA below is swapped for a tile showing that candidate's
  // current favorability. Only signed-in users can have campaigns.
  useEffect(() => {
    if (!isAuthenticated) {
      setActiveCampaign(null);
      return;
    }
    let cancelled = false;
    void listCampaigns()
      .then((campaigns) => {
        if (cancelled) return;
        const active =
          campaigns
            .filter((c) => c.status === "Active")
            .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))[0] ?? null;
        setActiveCampaign(active);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [isAuthenticated]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const rest = explainers;

  return (
    <div>
      {!loaded && (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Loading the issue…
        </p>
      )}
      {loaded && total === 0 && (
        <p
          className="py-12 text-base text-[var(--muted)]"
          data-testid="empty-issue"
        >
          This issue is still being assembled. Check back soon.
        </p>
      )}
      <div className="my-10 grid items-stretch gap-4 md:grid-cols-2">
        <CountdownTimer scope="National" testId="countdown-national" className="h-full" />
        {activeCampaign ? (
          <CampaignFavorabilityTile campaign={activeCampaign} />
        ) : (
          <Link
            to="/campaigns"
            data-testid="campaign-cta"
            className="flex h-full flex-col justify-between border border-[var(--accent)] bg-[var(--accent)]/5 p-6 transition hover:bg-[var(--accent)]/10"
          >
            <div>
              <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
                <Megaphone className="h-4 w-4" /> Campaign Manager
              </p>
              <h2 className="display mt-2 text-3xl">Run a campaign to election day.</h2>
              <p className="mt-1 text-sm leading-relaxed text-[var(--fg-soft)]">
                Take the reins for a candidate, respond to the real headlines, and try to win the race
                before the clock runs out.
              </p>
            </div>
            <span className="mt-5 inline-block w-fit rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white">
              Manage a campaign →
            </span>
          </Link>
        )}
      </div>

      {cover && <CoverStory briefing={cover} />}

      <section ref={explainersRef} className="mt-14 scroll-mt-24" data-testid="explainers-section">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Inside this issue
        </p>

        {/* Think Deeper — 3-4 quoted questions pulled from this page's briefings */}
        {rest.filter((b) => b.thinkDeeperQuestion).slice(0, 4).length > 0 && (
          <ul className="mt-5 space-y-3 border-l-2 border-[var(--accent)] pl-5">
            {rest
              .filter((b) => b.thinkDeeperQuestion)
              .slice(0, 4)
              .map((b) => (
                <li key={b.id}>
                  <Link
                    to={`/briefings/${b.slug}`}
                    className="group block"
                  >
                    <p className="text-base italic leading-snug text-[var(--fg)] group-hover:text-[var(--accent)]">
                      "{b.thinkDeeperQuestion}"
                    </p>
                    <p className="mt-1 text-xs font-semibold uppercase tracking-wider text-[var(--muted)] group-hover:text-[var(--accent)]">
                      {b.headline} →
                    </p>
                  </Link>
                </li>
              ))}
          </ul>
        )}

        {/* Mobile: horizontal snap-carousel of cards. Desktop: two-column grid. */}
        <ul
          className="-mx-4 mt-6 flex snap-x snap-mandatory gap-4 overflow-x-auto px-4 pb-3 md:mx-0 md:mt-8 md:grid md:snap-none md:grid-cols-2 md:gap-10 md:overflow-visible md:px-0 md:pb-0"
          data-testid="explainers-list"
        >
          {rest.map((b) => (
            <li
              key={b.id}
              className="w-[80vw] max-w-[340px] shrink-0 snap-start md:w-auto md:max-w-none md:shrink"
            >
              <Link
                to={`/briefings/${b.slug}`}
                className="group block h-full border border-[var(--border)] bg-[var(--bg-elev)] p-5 md:border-x-0 md:border-b-0 md:border-t md:bg-transparent md:p-0 md:pt-6"
              >
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
                  {b.locality && (
                    <span
                      className="mr-2 rounded-sm bg-[var(--accent)] px-1.5 py-0.5 text-[0.65rem] text-white"
                      data-testid={`briefing-local-badge-${b.locality}`}
                    >
                      Local · {localityLabel(b.locality)}
                    </span>
                  )}
                  {b.institution} · {b.status}
                </p>
                <h3 className="display mt-2 text-2xl group-hover:text-[var(--accent)] md:text-3xl">
                  {b.headline}
                </h3>
                <p className="mt-3 line-clamp-4 text-base leading-relaxed text-[var(--fg-soft)] md:line-clamp-none">
                  {b.summary30}
                </p>
                <p className="mt-3 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                  Key concept · {b.keyConcept}
                </p>
                <p className="mt-1 text-xs uppercase tracking-wider text-[var(--muted)]/70">
                  {formatStoryDate(b.createdAt)}
                </p>
              </Link>
            </li>
          ))}
        </ul>

        {totalPages > 1 && (
          <nav
            className="mt-8 flex items-center justify-center gap-4"
            data-testid="explainers-pagination"
            aria-label="Story pages"
          >
            <button
              type="button"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="flex items-center gap-1 rounded-full border border-[var(--border)] px-4 py-2 text-xs font-semibold uppercase tracking-wider text-[var(--fg)] transition hover:border-[var(--accent)] hover:text-[var(--accent)] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-[var(--border)] disabled:hover:text-[var(--fg)]"
              data-testid="explainers-prev"
            >
              <ChevronLeft className="h-4 w-4" /> Prev
            </button>
            <span className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
              Page {page} of {totalPages}
            </span>
            <button
              type="button"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="flex items-center gap-1 rounded-full border border-[var(--border)] px-4 py-2 text-xs font-semibold uppercase tracking-wider text-[var(--fg)] transition hover:border-[var(--accent)] hover:text-[var(--accent)] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-[var(--border)] disabled:hover:text-[var(--fg)]"
              data-testid="explainers-next"
            >
              Next <ChevronRight className="h-4 w-4" />
            </button>
          </nav>
        )}
      </section>

      <section
        className="mt-16 grid gap-3 border border-indigo-200 bg-indigo-50 p-8 md:grid-cols-[1fr_auto] md:items-center"
        data-testid="virtual-candidates-cta"
      >
        <div>
          <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-indigo-700">
            <Megaphone className="h-4 w-4" /> Virtual Candidates · A simulation
          </p>
          <p className="display mt-2 text-2xl text-indigo-900">
            Meet the AI candidates running for 2028.
          </p>
          <p className="mt-1 text-sm leading-relaxed text-indigo-900/80">
            Fictional candidates react to the same headlines you just read. React to whole posts —
            or to the exact lines that move you — then see which ones match your Civic Compass.
          </p>
        </div>
        <div className="flex flex-col gap-2">
          <Link
            to="/candidates"
            className="rounded-full bg-indigo-600 px-6 py-3 text-center text-sm font-semibold text-white"
            data-testid="virtual-candidates-cta-link"
          >
            Open the Campaign Feed
          </Link>
          <Link
            to="/match"
            className="text-center text-xs font-semibold uppercase tracking-wider text-indigo-700 hover:underline"
            data-testid="virtual-candidates-match-link"
          >
            Match me with candidates →
          </Link>
        </div>
      </section>

      <section className="mt-20 border-y border-[var(--border)] py-10">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Words you need to know
        </p>
        <PullQuote
          text="Most bills do not become law just because someone introduces them. They move through committees, where most quietly die."
          source="From: Congress advances a student data privacy bill"
        />
      </section>

      {budgetFacts.length > 0 && (
        <section className="mt-16" data-testid="budget-facts">
          <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
            The budget, from both sides
          </p>
          <h2 className="display mt-2 text-4xl">Did you know?</h2>
          <p className="mt-3 max-w-2xl text-base leading-relaxed text-[var(--fg-soft)]">
            Two facts can both be true and still pull in opposite directions.
            Fresh contradictions from the federal budget, every day.
          </p>
          <div className="mt-8 space-y-6">
            {budgetFacts.slice(0, 2).map((fact) => (
              <BudgetFactCard key={fact.id} fact={fact} />
            ))}
          </div>
        </section>
      )}

      {concept && (
        <section
          className="mt-16 border border-[var(--border)] bg-[var(--bg-elev)] p-8"
          data-testid="concept-of-the-day"
        >
          <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
            Concept of the day
          </p>
          <Link
            to={`/concepts/${concept.slug}`}
            className="display mt-2 block text-3xl hover:text-[var(--accent)]"
            data-testid="concept-of-the-day-link"
          >
            {concept.title}
          </Link>
          <p className="mt-3 text-base leading-relaxed text-[var(--fg-soft)]">
            {concept.plainDefinition}
          </p>
          <p className="mt-3 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            Read the full concept →
          </p>
        </section>
      )}

      <section className="mt-16" data-testid="learn-more-grid">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Go deeper
        </p>
        <h2 className="display mt-2 text-4xl">Three ways to learn</h2>
        <ul className="mt-8 grid gap-6 md:grid-cols-3">
          <li>
            <Link
              to="/quizzes"
              className="group block h-full border border-[var(--border)] bg-[var(--bg-elev)] p-6 hover:border-[var(--accent)]"
              data-testid="cta-quiz"
            >
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
                Civics 101
              </p>
              <h3 className="display mt-2 text-2xl group-hover:text-[var(--accent)]">
                Test what you know
              </h3>
              <p className="mt-3 text-sm leading-relaxed text-[var(--fg-soft)]">
                Four quick questions. Each one tells you what's actually
                happening behind the headline.
              </p>
            </Link>
          </li>
          <li>
            <Link
              to="/timelines/bill"
              className="group block h-full border border-[var(--border)] bg-[var(--bg-elev)] p-6 hover:border-[var(--accent)]"
              data-testid="cta-timeline"
            >
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
                Process diagram
              </p>
              <h3 className="display mt-2 text-2xl group-hover:text-[var(--accent)]">
                How a bill becomes law
              </h3>
              <p className="mt-3 text-sm leading-relaxed text-[var(--fg-soft)]">
                The five-step map. Place this week's headline on it and you'll
                see what's really going on.
              </p>
            </Link>
          </li>
          <li>
            <Link
              to="/teachers"
              className="group block h-full border border-[var(--border)] bg-[var(--bg-elev)] p-6 hover:border-[var(--accent)]"
              data-testid="cta-teachers"
            >
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
                For teachers & parents
              </p>
              <h3 className="display mt-2 text-2xl group-hover:text-[var(--accent)]">
                Classroom & dinner-table prompts
              </h3>
              <p className="mt-3 text-sm leading-relaxed text-[var(--fg-soft)]">
                Discussion starters keyed to any briefing — built to be
                teachable without being partisan.
              </p>
            </Link>
          </li>
        </ul>
      </section>

      {isAuthenticated && user ? (
        <section
          className="mt-16 grid gap-3 border border-[var(--border)] bg-[var(--bg-elev)] p-8 md:grid-cols-[1fr_auto] md:items-center"
          data-testid="welcome-back-cta"
        >
          <div>
            <p className="display text-2xl">
              Welcome back, {user.displayName ?? user.email}.
            </p>
            <p className="mt-1 text-sm text-[var(--fg-soft)]">
              Your Civic Compass and Debate Arena profile are in sync. Pick
              up where you left off.
            </p>
          </div>
          <ButtonLink to="/profile">
            View my Civic Compass
          </ButtonLink>
        </section>
      ) : (
        <section
          className="mt-16 grid gap-3 border border-[var(--border)] bg-[var(--bg-elev)] p-8 md:grid-cols-[1fr_auto] md:items-center"
          data-testid="onboarding-cta"
        >
          <div>
            <p className="display text-2xl">Build your Civic Compass.</p>
            <p className="mt-1 text-sm text-[var(--fg-soft)]">
              Ten quick choices. No party labels. You can change your mind
              later.
            </p>
          </div>
          <ButtonLink to="/onboarding">
            Start the questions
          </ButtonLink>
        </section>
      )}

      <section
        className="mt-8 grid gap-3 border border-[var(--border)] p-8 md:grid-cols-[1fr_auto] md:items-center"
        data-testid="signup-cta"
      >
        <div>
          <p className="display text-2xl">
            {isAuthenticated
              ? "Your account works in Debate Arena too."
              : "One account, two arenas."}
          </p>
          <p className="mt-1 text-sm text-[var(--fg-soft)]">
            {isAuthenticated
              ? "Jump into a debate on the Debate Arena floor with the same login."
              : "Create a Civersify account and use the same login on the Debate Arena debate floor — your civic profile follows you."}
          </p>
        </div>
        {isAuthenticated ? (
          <a
            href={DEBATE_ARENA_URL}
            target="_blank"
            rel="noreferrer"
            className="rounded-full border border-[var(--accent)] px-6 py-3 text-center text-sm font-semibold text-[var(--accent)]"
            data-testid="signup-cta-debate-link"
          >
            Open Debate Arena
          </a>
        ) : (
          <ButtonLink
            to="/register"
            variant="secondary"
            data-testid="signup-cta-register-link"
          >
            Sign up
          </ButtonLink>
        )}
      </section>
    </div>
  );
}

// Replaces the "pick a candidate" CTA once the player is managing a campaign.
// Surfaces the candidate's current favorability (their support share across the
// race) as the headline metric, and links straight back into the campaign.
function CampaignFavorabilityTile({ campaign }: { campaign: CivicCampaignSummary }) {
  const favorability = campaign.playerSupport;
  return (
    <Link
      to={`/campaigns/${campaign.id}`}
      data-testid="campaign-favorability"
      className="flex h-full flex-col justify-between border border-[var(--accent)] bg-[var(--accent)]/5 p-6 transition hover:bg-[var(--accent)]/10"
    >
      <div>
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          <Megaphone className="h-4 w-4" /> Campaign Manager
        </p>
        <div className="mt-3 flex items-center gap-3">
          <CandidateAvatar
            candidate={{
              slug: campaign.candidateSlug,
              name: campaign.candidateName,
              avatarBaseUrl: "",
            }}
            size={44}
          />
          <div className="min-w-0">
            <h2 className="display truncate text-2xl leading-tight">{campaign.candidateName}</h2>
            <p className="truncate text-sm text-[var(--fg-soft)]">
              {campaign.party} · {campaign.raceLabel}
            </p>
          </div>
        </div>
        <div className="mt-4 flex items-end gap-3">
          <p className="display text-[44px] leading-none" data-testid="favorability-value">
            {favorability.toFixed(1)}
            <span className="text-xl">%</span>
          </p>
          <div className="pb-1">
            <p className="text-[10px] font-semibold uppercase tracking-[0.15em] text-[var(--muted)]">
              Favorability
            </p>
            <p
              className={`text-xs font-semibold ${
                campaign.isLeading ? "text-emerald-600" : "text-[var(--fg-soft)]"
              }`}
            >
              {campaign.isLeading ? "Leading the race" : "Trailing the field"}
            </p>
          </div>
        </div>
      </div>
      <span className="mt-5 inline-block w-fit rounded-full bg-[var(--accent)] px-5 py-2 text-sm font-semibold text-white">
        Resume campaign →
      </span>
    </Link>
  );
}
