import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { getRaces, createCampaign, type CivicRace, type CivicCampaignDifficulty } from "@/api/campaignManager";
import type { CandidateSummary } from "@/api/campaign";
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
  const [weeks, setWeeks] = useState(8);
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
        totalWeeks: weeks,
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
              <ul className="mt-3 grid gap-3 sm:grid-cols-2">
                {race.candidates.map((c) => {
                  const isSelected = selected?.slug === c.slug;
                  return (
                    <li key={c.slug}>
                      <button
                        type="button"
                        data-testid="candidate-option"
                        onClick={() => setSelected(c)}
                        className={`flex w-full items-center gap-3 border p-4 text-left transition ${
                          isSelected
                            ? "border-[var(--accent)] bg-[var(--accent)]/5"
                            : "border-[var(--border)] bg-[var(--bg-elev)] hover:border-[var(--accent)]"
                        }`}
                      >
                        <CandidateAvatar candidate={c} size={48} />
                        <span className="min-w-0">
                          <span className="block font-semibold text-[var(--fg)]">{c.name}</span>
                          <span className="block text-sm text-[var(--fg-soft)]">{c.party}</span>
                          {c.isIncumbent && (
                            <span className="text-xs font-semibold uppercase tracking-wide text-[var(--accent)]">
                              Incumbent
                            </span>
                          )}
                        </span>
                      </button>
                    </li>
                  );
                })}
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

            <div className="mt-6">
              <label htmlFor="weeks" className="text-sm font-semibold text-[var(--fg)]">
                Campaign length: <span className="text-[var(--accent)]">{weeks} weeks</span>
              </label>
              <input
                id="weeks"
                type="range"
                min={4}
                max={16}
                value={weeks}
                data-testid="weeks-slider"
                onChange={(e) => setWeeks(Number(e.target.value))}
                className="mt-2 w-full max-w-sm accent-[var(--accent)]"
              />
            </div>

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
