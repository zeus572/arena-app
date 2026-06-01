import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ChevronDown, Loader2 } from "lucide-react";
import { getRaces, createCampaign, type CivicRace, type CivicCampaignDifficulty } from "@/api/campaignManager";
import { getCandidate, type CandidateSummary, type CandidateDetail } from "@/api/campaign";
import { CandidateAvatar } from "../components/CandidateAvatar";

const DIFFICULTIES: { key: CivicCampaignDifficulty; label: string; blurb: string }[] = [
  { key: "Easy", label: "Easy", blurb: "Opponents campaign gently." },
  { key: "Normal", label: "Normal", blurb: "A fair fight." },
  { key: "Hard", label: "Hard", blurb: "Rivals come out swinging." },
];

export default function CampaignCreate() {
  const navigate = useNavigate();
  const [races, setRaces] = useState<CivicRace[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [selected, setSelected] = useState<CandidateSummary | null>(null);
  const [difficulty, setDifficulty] = useState<CivicCampaignDifficulty>("Normal");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void getRaces()
      .then(setRaces)
      .finally(() => setLoaded(true));
  }, []);

  async function start() {
    if (!selected) return;
    setSubmitting(true);
    setError(null);
    try {
      const created = await createCampaign({
        candidateSlug: selected.slug,
        difficulty,
      });
      navigate(`/campaigns/${created.id}`);
    } catch {
      setError("Could not start the campaign. Please try again.");
      setSubmitting(false);
    }
  }

  return (
    <section data-testid="campaign-create">
      <Link
        to="/campaigns"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Campaigns
      </Link>
      <h1 className="display mt-4 text-4xl">Choose your candidate</h1>
      <p className="mt-2 max-w-prose text-[var(--fg-soft)]">
        Pick an existing candidate to manage. You'll compete against the other candidates in their race.
      </p>

      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
          Loading races…
        </p>
      ) : (
        <div className="mt-8 space-y-10">
          {races.map((race) => (
            <div key={race.raceKey}>
              <h2 className="text-sm font-semibold uppercase tracking-wider text-[var(--accent)]">
                {race.label}
              </h2>
              <ul className="mt-3 grid items-start gap-3 sm:grid-cols-2">
                {race.candidates.map((c) => (
                  <CandidateOption
                    key={c.slug}
                    candidate={c}
                    selected={selected?.slug === c.slug}
                    onSelect={() => setSelected(c)}
                  />
                ))}
              </ul>
            </div>
          ))}

          <div className="border-t border-[var(--border)] pt-8">
            <h2 className="display text-2xl">Settings</h2>

            <div className="mt-4">
              <p className="text-sm font-semibold text-[var(--fg)]">Difficulty</p>
              <div className="mt-2 flex flex-wrap gap-2">
                {DIFFICULTIES.map((d) => (
                  <button
                    key={d.key}
                    type="button"
                    data-testid={`difficulty-${d.key}`}
                    onClick={() => setDifficulty(d.key)}
                    title={d.blurb}
                    className={`rounded-full px-4 py-1.5 text-sm font-semibold transition ${
                      difficulty === d.key
                        ? "bg-[var(--accent)] text-white"
                        : "border border-[var(--border)] text-[var(--fg-soft)]"
                    }`}
                  >
                    {d.label}
                  </button>
                ))}
              </div>
              <p className="mt-1 text-xs text-[var(--muted)]">
                {DIFFICULTIES.find((d) => d.key === difficulty)?.blurb}
              </p>
            </div>

            <p className="mt-6 text-sm text-[var(--fg-soft)]" data-testid="duration-note">
              The campaign runs until election day — every campaign is tied to the next real election.
            </p>

            {error && (
              <p className="mt-4 text-sm font-semibold text-[var(--danger,#dc2626)]" data-testid="create-error">
                {error}
              </p>
            )}

            <button
              type="button"
              data-testid="start-campaign"
              disabled={!selected || submitting}
              onClick={start}
              className="mt-6 rounded-full bg-[var(--accent)] px-6 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
            >
              {submitting
                ? "Starting…"
                : selected
                  ? `Manage ${selected.name}`
                  : "Select a candidate"}
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

function CandidateOption({
  candidate,
  selected,
  onSelect,
}: {
  candidate: CandidateSummary;
  selected: boolean;
  onSelect: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [detail, setDetail] = useState<CandidateDetail | null>(null);
  const [loading, setLoading] = useState(false);

  async function toggle() {
    const next = !expanded;
    setExpanded(next);
    // Lazy-load the full profile the first time it's opened.
    if (next && !detail && !loading) {
      setLoading(true);
      try {
        const d = await getCandidate(candidate.slug);
        setDetail(d ?? null);
      } finally {
        setLoading(false);
      }
    }
  }

  return (
    <li
      className={`border transition ${
        selected
          ? "border-[var(--accent)] bg-[var(--accent)]/5"
          : "border-[var(--border)] bg-[var(--bg-elev)]"
      }`}
      data-testid="candidate-option"
    >
      <div className="flex items-stretch">
        <button
          type="button"
          data-testid="candidate-select"
          onClick={onSelect}
          className="flex flex-1 items-center gap-3 p-4 text-left"
        >
          <CandidateAvatar candidate={candidate} size={48} />
          <span className="min-w-0">
            <span className="block font-semibold text-[var(--fg)]">{candidate.name}</span>
            <span className="block text-sm text-[var(--fg-soft)]">{candidate.party}</span>
            {candidate.isIncumbent && (
              <span className="text-xs font-semibold uppercase tracking-wide text-[var(--accent)]">
                Incumbent
              </span>
            )}
          </span>
        </button>
        <button
          type="button"
          data-testid="candidate-expand"
          aria-expanded={expanded}
          aria-label={expanded ? `Hide ${candidate.name}'s profile` : `Show ${candidate.name}'s profile`}
          onClick={toggle}
          className="flex shrink-0 items-center border-l border-[var(--border)] px-3 text-[var(--muted)] transition hover:text-[var(--fg)]"
        >
          <ChevronDown
            className={`h-5 w-5 transition-transform ${expanded ? "rotate-180" : ""}`}
          />
        </button>
      </div>

      {expanded && (
        <div className="border-t border-[var(--border)] p-4" data-testid="candidate-profile">
          {loading ? (
            <p className="flex items-center gap-2 text-sm text-[var(--muted)]">
              <Loader2 className="h-4 w-4 animate-spin" /> Loading profile…
            </p>
          ) : detail ? (
            <div className="space-y-3">
              <p className="text-sm leading-relaxed text-[var(--fg-soft)]">{detail.bio}</p>
              {detail.platformPlanks.length > 0 && (
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
                    Platform
                  </p>
                  <ul className="mt-1 space-y-1">
                    {detail.platformPlanks.slice(0, 3).map((p) => (
                      <li key={p.id} className="text-sm text-[var(--fg-soft)]">
                        · <span className="font-semibold text-[var(--fg)]">{p.title}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              <button
                type="button"
                onClick={onSelect}
                className="text-sm font-semibold text-[var(--accent)] hover:underline"
              >
                {selected ? "Selected ✓" : `Manage ${candidate.name} →`}
              </button>
            </div>
          ) : (
            <p className="text-sm text-[var(--muted)]">Profile unavailable.</p>
          )}
        </div>
      )}
    </li>
  );
}
