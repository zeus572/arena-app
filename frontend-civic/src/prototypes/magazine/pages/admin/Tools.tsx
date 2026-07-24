import { useState } from "react";
import { Wand2, AlertTriangle } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { Button } from "@/prototypes/magazine/components/Button";
import { getCandidates, type CampaignPost } from "@/api/campaign";
import { generateCandidatePost, GenerationSkippedError } from "@/api/adminCandidates";
import { ForbiddenError } from "@/api/admin";
import { useAdminData, AdminStates } from "./common";

export default function Tools() {
  const { isAuthenticated, isLoading } = useAuth();
  const { data: candidates, status } = useAdminData(() => getCandidates({}), !isLoading && isAuthenticated);

  const [slug, setSlug] = useState("");
  const [briefingId, setBriefingId] = useState("");
  const [force, setForce] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<CampaignPost | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!slug || busy) return;
    setBusy(true);
    setResult(null);
    setError(null);
    try {
      const post = await generateCandidatePost(slug, {
        triggerBriefingId: briefingId.trim() || undefined,
        force,
      });
      setResult(post);
    } catch (err) {
      if (err instanceof GenerationSkippedError) setError(err.message);
      else if (err instanceof ForbiddenError) setError("Your account isn’t on the admin allowlist.");
      else setError("Generation failed. Check the candidate slug and try again.");
    } finally {
      setBusy(false);
    }
  }

  if (status !== "ok" || !candidates) return <AdminStates status={status} testid="admin-tools" />;

  return (
    <section data-testid="admin-tools-page">
      <header>
        <h1 className="display text-3xl md:text-4xl">Candidate tools</h1>
        <p className="mt-3 max-w-prose text-[var(--fg-soft)]">
          Manually generate a campaign post for an AI candidate. This calls the LLM and spends
          budget — use <span className="font-semibold">force</span> only to override a cooldown or
          daily cap.
        </p>
      </header>

      <form onSubmit={submit} className="mt-8 grid max-w-xl gap-4 border border-[var(--border)] p-5" data-testid="admin-tools-form">
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Candidate</span>
          <select
            className="border border-[var(--border)] bg-[var(--bg-elev)] p-2"
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            data-testid="admin-tools-candidate"
          >
            <option value="">Select a candidate…</option>
            {candidates.map((c) => (
              <option key={c.id} value={c.slug}>
                {c.name} — {c.office}
                {c.state ? ` (${c.state})` : ""}
              </option>
            ))}
          </select>
        </label>

        <label className="grid gap-1 text-sm">
          <span className="font-medium">
            Trigger briefing ID <span className="font-normal text-[var(--muted)]">(optional)</span>
          </span>
          <input
            type="text"
            className="border border-[var(--border)] bg-[var(--bg-elev)] p-2 font-mono text-xs"
            placeholder="GUID — leave blank for a platform post"
            value={briefingId}
            onChange={(e) => setBriefingId(e.target.value)}
            data-testid="admin-tools-briefing"
          />
        </label>

        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={force} onChange={(e) => setForce(e.target.checked)} data-testid="admin-tools-force" />
          <span>Force (override cooldown / daily budget)</span>
        </label>

        <div>
          <Button type="submit" disabled={!slug || busy} data-testid="admin-tools-submit">
            <Wand2 size={16} /> {busy ? "Generating…" : "Generate post"}
          </Button>
        </div>
      </form>

      {error && (
        <div className="mt-4 flex max-w-xl items-start gap-2 border border-[var(--state)] bg-[var(--state-soft)] p-3 text-sm" data-testid="admin-tools-error">
          <AlertTriangle size={16} className="mt-0.5 shrink-0 text-[var(--state)]" />
          <span>{error}</span>
        </div>
      )}

      {result && (
        <div className="mt-4 max-w-xl border border-[var(--border)] p-4" data-testid="admin-tools-result">
          <p className="text-xs uppercase tracking-wider text-[var(--accent)]">
            Generated · {result.toneLabel} · intensity {result.intensity}
          </p>
          <p className="mt-2 whitespace-pre-wrap text-sm">{result.body}</p>
          {result.issueTags.length > 0 && (
            <p className="mt-2 text-[11px] uppercase tracking-wider text-[var(--muted)]">{result.issueTags.join(" · ")}</p>
          )}
        </div>
      )}
    </section>
  );
}
