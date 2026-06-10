import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ButtonLink } from "../components/Button";
import { getReceipt, type ValuesReceipt } from "@/api/receipts";

export default function MagazineReceipt() {
  const { id } = useParams();
  const [receipt, setReceipt] = useState<ValuesReceipt | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!id) return;
    void getReceipt(id)
      .then((r) => setReceipt(r ?? null))
      .finally(() => setLoaded(true));
  }, [id]);

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="loading">
        Loading the receipt…
      </p>
    );
  }

  if (!receipt) {
    return (
      <div className="mx-auto max-w-3xl py-16 text-center" data-testid="receipt-not-found">
        <h1 className="display text-4xl">Receipt not found.</h1>
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
    <article className="mx-auto max-w-3xl" data-testid="receipt">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      <header className="mt-8 text-center">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Today's Values Receipt
        </p>
        <h1 className="display mt-3 text-5xl">What we learned about you.</h1>
        <p className="mt-4 text-sm text-[var(--fg-soft)]">
          Drawn from {receipt.answerCountAtTime} answer
          {receipt.answerCountAtTime === 1 ? "" : "s"}. Profile version{" "}
          {receipt.profileVersionAtTime}.
        </p>
      </header>

      <section className="mt-12">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
          Today we learned
        </p>
        <ul
          className="mt-4 space-y-3 text-lg leading-relaxed"
          data-testid="learned-list"
        >
          {receipt.learnedInsights.map((line, i) => (
            <li
              key={i}
              className="border-l-2 border-[var(--accent)] pl-4"
              data-testid={`insight-${i}`}
            >
              {line}
            </li>
          ))}
        </ul>
      </section>

      {receipt.tensions.length > 0 && (
        <section className="mt-14" data-testid="tensions-section">
          <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
            Worth sitting with
          </p>
          <ul className="mt-4 space-y-4">
            {receipt.tensions.map((t) => (
              <li
                key={t.axisKey}
                className="border border-[var(--border)] bg-[var(--bg-elev)] p-5"
                data-testid={`tension-${t.axisKey}`}
              >
                <p className="display text-base font-semibold">{t.axisName}</p>
                <p className="mt-2 text-base leading-relaxed text-[var(--fg-soft)]">
                  {t.framing}
                </p>
              </li>
            ))}
          </ul>
        </section>
      )}

      {receipt.uncertainAreas.length > 0 && (
        <section className="mt-14">
          <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)]">
            Where we're not sure yet
          </p>
          <p className="mt-3 text-sm text-[var(--fg-soft)]">
            Your answers haven't yet given us a strong read on{" "}
            {receipt.uncertainAreas.length} axis
            {receipt.uncertainAreas.length === 1 ? "" : "es"}. Answer more
            questions to fill those in.
          </p>
        </section>
      )}

      <section className="mt-16 text-center">
        <ButtonLink to="/onboarding" variant="ghost">
          Answer more questions
        </ButtonLink>
        <ButtonLink to="/profile" className="ml-3">
          See your full profile
        </ButtonLink>
      </section>
    </article>
  );
}
