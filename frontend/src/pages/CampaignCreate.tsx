import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { getCampaignPersonas, createCampaign } from "@/api/client";
import type { Persona, CampaignDifficulty } from "@/api/types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { ArrowLeft, Check, Megaphone } from "lucide-react";

const DIFFICULTIES: { value: CampaignDifficulty; label: string; note: string }[] = [
  { value: "Easy", label: "Easy", note: "Forgiving opponent" },
  { value: "Normal", label: "Normal", note: "Balanced race" },
  { value: "Hard", label: "Hard", note: "Tough opponent" },
];

export default function CampaignCreate() {
  const navigate = useNavigate();
  const [personas, setPersonas] = useState<Persona[]>([]);
  const [loadingPersonas, setLoadingPersonas] = useState(true);

  const [candidateName, setCandidateName] = useState("");
  const [personaId, setPersonaId] = useState<string>("");
  const [difficulty, setDifficulty] = useState<CampaignDifficulty>("Normal");
  const [totalWeeks, setTotalWeeks] = useState(4);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getCampaignPersonas()
      .then((data) => {
        if (!active) return;
        setPersonas(data);
        if (data.length > 0) setPersonaId(data[0].key);
      })
      .catch(() => {
        if (active) setError("Could not load personas.");
      })
      .finally(() => {
        if (active) setLoadingPersonas(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const canSubmit = candidateName.trim().length >= 2 && personaId !== "" && !submitting;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setSubmitting(true);
    setError(null);
    try {
      const detail = await createCampaign({
        candidateName: candidateName.trim(),
        personaId,
        difficulty,
        totalWeeks,
      });
      navigate(`/campaigns/${detail.campaign.id}`);
    } catch {
      setError("Could not create campaign. Please try again.");
      setSubmitting(false);
    }
  };

  return (
    <main className="mx-auto max-w-2xl px-4 py-8">
      <Link
        to="/campaigns"
        className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground mb-4 no-underline"
      >
        <ArrowLeft size={13} /> Back to campaigns
      </Link>

      <div className="flex items-center gap-2 mb-6">
        <Megaphone size={20} className="text-primary" />
        <h1 className="text-2xl font-bold text-foreground">New Campaign</h1>
      </div>

      <form onSubmit={handleSubmit} className="flex flex-col gap-6">
        {/* Candidate name */}
        <div>
          <label className="block text-xs font-semibold text-foreground mb-1.5">
            Candidate name
          </label>
          <input
            type="text"
            value={candidateName}
            onChange={(e) => setCandidateName(e.target.value)}
            placeholder="e.g. Alex Rivera"
            required
            minLength={2}
            className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>

        {/* Persona selection */}
        <div>
          <label className="block text-xs font-semibold text-foreground mb-1.5">
            Choose a persona
          </label>
          {loadingPersonas ? (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              {[1, 2, 3].map((i) => (
                <div key={i} className="rounded-xl border border-border bg-card h-32 animate-pulse" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              {personas.map((p) => {
                const selected = p.key === personaId;
                return (
                  <button
                    type="button"
                    key={p.key}
                    onClick={() => setPersonaId(p.key)}
                    className={cn(
                      "relative text-left rounded-xl border bg-card p-4 transition-colors",
                      selected
                        ? "border-primary ring-2 ring-primary/20"
                        : "border-border hover:border-primary/30"
                    )}
                  >
                    {selected && (
                      <span className="absolute top-2 right-2 flex h-5 w-5 items-center justify-center rounded-full bg-primary text-primary-foreground">
                        <Check size={12} />
                      </span>
                    )}
                    <p className="text-sm font-semibold text-card-foreground">{p.name}</p>
                    <p className="text-[11px] text-muted-foreground mt-1">{p.theme}</p>
                    <p className="text-[11px] text-muted-foreground/80 mt-2">
                      Faces {p.opponentName}
                    </p>
                  </button>
                );
              })}
            </div>
          )}
        </div>

        {/* Difficulty */}
        <div>
          <label className="block text-xs font-semibold text-foreground mb-1.5">Difficulty</label>
          <div className="flex gap-2">
            {DIFFICULTIES.map((d) => {
              const selected = d.value === difficulty;
              return (
                <button
                  type="button"
                  key={d.value}
                  onClick={() => setDifficulty(d.value)}
                  className={cn(
                    "flex-1 rounded-lg border bg-card px-3 py-2 text-left transition-colors",
                    selected
                      ? "border-primary ring-2 ring-primary/20"
                      : "border-border hover:border-primary/30"
                  )}
                >
                  <span className="block text-sm font-medium text-card-foreground">{d.label}</span>
                  <span className="block text-[10px] text-muted-foreground">{d.note}</span>
                </button>
              );
            })}
          </div>
        </div>

        {/* Total weeks */}
        <div>
          <label className="block text-xs font-semibold text-foreground mb-1.5">
            Campaign length: {totalWeeks} weeks
          </label>
          <input
            type="range"
            min={4}
            max={24}
            step={2}
            value={totalWeeks}
            onChange={(e) => setTotalWeeks(Number(e.target.value))}
            className="w-full accent-primary"
          />
          <div className="flex justify-between text-[10px] text-muted-foreground mt-1">
            <span>4</span>
            <span>24</span>
          </div>
        </div>

        {error && <p className="text-xs text-destructive">{error}</p>}

        <Button type="submit" disabled={!canSubmit} className="self-start text-sm">
          {submitting ? "Creating..." : "Launch Campaign"}
        </Button>
      </form>
    </main>
  );
}
