import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import type { ThinkDeeper } from "@/api/types";
import { getThinkDeeperBySlug } from "@/api/thinkDeepers";
import { ValueChip } from "../components/ValueChip";
import { PullQuote } from "../components/PullQuote";
import { Button } from "../components/Button";

export default function MagazineThinkDeeper() {
  const { slug } = useParams();
  const [td, setTd] = useState<ThinkDeeper | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!slug) return;
    setLoaded(false);
    void getThinkDeeperBySlug(slug)
      .then((t) => setTd(t ?? null))
      .finally(() => setLoaded(true));
    setSelected(new Set());
  }, [slug]);

  const toggle = (v: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(v)) next.delete(v);
      else next.add(v);
      return next;
    });
  };

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading…
      </p>
    );
  }

  if (!td) {
    return (
      <div className="mx-auto max-w-3xl py-16 text-center" data-testid="thinkdeeper-not-found">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Not in this issue
        </p>
        <h1 className="display mt-3 text-4xl">
          We don't have that reflection yet.
        </h1>
        <Link
          to="/"
          className="mt-6 inline-block text-sm font-semibold text-[var(--accent)] hover:underline"
        >
          ← Back to the current issue
        </Link>
      </div>
    );
  }

  return (
    <article className="mx-auto max-w-3xl">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mt-8 text-center">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Think deeper · A reflection essay
        </p>
        <h1 className="display mt-3 text-5xl">{td.issue}</h1>
      </header>

      <section className="mt-12">
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
          First reaction
        </p>
        <p className="mt-3 text-xl leading-relaxed text-[var(--fg-soft)]">
          {td.firstReactionPrompt}
        </p>
      </section>

      <section className="mt-12">
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
          Which values matter to you here? (Tap as many as apply.)
        </p>
        <div className="mt-4 flex flex-wrap gap-3">
          {td.values.map((v) => (
            <ValueChip
              key={v}
              label={v}
              selected={selected.has(v)}
              onClick={() => toggle(v)}
            />
          ))}
        </div>
      </section>

      <section className="mt-16 grid gap-8 md:grid-cols-2">
        <div className="border-t-2 border-[var(--accent)] pt-4">
          <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
            Strongest argument A
          </p>
          <p className="display mt-2 text-2xl leading-tight">
            {td.strongestArgumentA}
          </p>
          <p className="mt-4 text-sm leading-relaxed text-[var(--fg-soft)]">
            <strong className="text-[var(--fg)]">What this side may miss:</strong>{" "}
            {td.whatSideAMayMiss}
          </p>
        </div>
        <div className="border-t-2 border-[var(--fg)] pt-4">
          <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
            Strongest argument B
          </p>
          <p className="display mt-2 text-2xl leading-tight">
            {td.strongestArgumentB}
          </p>
          <p className="mt-4 text-sm leading-relaxed text-[var(--fg-soft)]">
            <strong className="text-[var(--fg)]">What this side may miss:</strong>{" "}
            {td.whatSideBMayMiss}
          </p>
        </div>
      </section>

      <PullQuote text={td.canBothBeTrue} source="Can both things be true?" />

      <section className="mt-12">
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">
          What would change your mind?
        </p>
        <ul className="mt-4 space-y-2 text-base leading-relaxed">
          {td.whatWouldChangeYourMind.map((m, i) => (
            <li key={i} className="border-l-2 border-[var(--border)] pl-4">
              {m}
            </li>
          ))}
        </ul>
      </section>

      <section className="mt-16 border border-[var(--border)] bg-[var(--bg-elev)] p-8">
        <p className="display text-xs font-semibold uppercase tracking-[0.2em] text-[var(--accent)]">
          Build your view
        </p>
        <p className="display mt-2 text-2xl">{td.buildYourViewPrompt}</p>
        <textarea
          rows={5}
          placeholder="Write 2–3 sentences here…"
          className="mt-5 w-full border border-[var(--border)] bg-white p-4 text-base"
        />
        <div className="mt-3 flex items-center justify-between text-xs text-[var(--muted)]">
          <span>
            Values you weighted:{" "}
            {selected.size === 0
              ? "(none yet)"
              : Array.from(selected).join(", ")}
          </span>
          <Button>
            Save reflection (mock)
          </Button>
        </div>
      </section>
    </article>
  );
}
