import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { FilingStatus, StateProfile } from "@/taxModel/engine";
import { compute, MACRO_PROOF, STATE_PROFILES } from "@/taxModel/engine";
import { getTaxStates } from "@/api/taxModel";
import { HouseholdCalculator } from "../components/taxApportionment/HouseholdCalculator";
import { ScalingTable } from "../components/taxApportionment/ScalingTable";
import { StateCard } from "../components/taxApportionment/StateCard";
import { CaveatGrid } from "../components/taxApportionment/CaveatGrid";
import { PonderSection } from "../components/taxApportionment/PonderSection";

function SectionHeading({ kicker, title }: { kicker: string; title: string }) {
  return (
    <header className="mb-6">
      <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        {kicker}
      </p>
      <h2 className="display mt-2 text-3xl md:text-4xl">{title}</h2>
    </header>
  );
}

export default function MagazineTaxApportionment() {
  const [income, setIncome] = useState(100_000);
  const [filing, setFiling] = useState<FilingStatus>("single");
  const [stateCode, setStateCode] = useState("CA");
  // Start with the bundled 8 so the page renders instantly, then swap in all 50
  // from the API (the single source of truth). Falls back to the 8 if the API is down.
  const [states, setStates] = useState<StateProfile[]>(STATE_PROFILES);

  useEffect(() => {
    let active = true;
    void getTaxStates()
      .then((fetched) => {
        if (active && fetched.length > 0) setStates(fetched);
      })
      .catch(() => {
        /* keep the bundled fallback set */
      });
    return () => {
      active = false;
    };
  }, []);

  const profile =
    states.find((s) => s.code === stateCode) ?? states[0] ?? STATE_PROFILES[0];
  const result = useMemo(() => compute(income, filing, profile), [income, filing, profile]);

  return (
    <article data-testid="tax-apportionment">
      <Link
        to="/"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← Back to issue
      </Link>

      {/* 1 · Hero */}
      <header className="mx-auto mt-8 max-w-3xl text-center">
        <p className="display text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          Interactive model · Where your money goes
        </p>
        <h1 className="display mt-3 text-5xl md:text-6xl">Who actually gets your tax dollar?</h1>
        <p className="mt-5 text-xl leading-relaxed text-[var(--fg-soft)]">
          "Pork" is a verdict on top of an arithmetic. Before you can argue whether federal
          spending is wasteful, it helps to see how much of your tax even reaches the federal
          government — and how much never leaves your state.
        </p>
      </header>

      {/* 2 · Macro proof */}
      <section className="mt-16">
        <div className="grid gap-4 sm:grid-cols-3" data-testid="tax-macro-proof">
          <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-6 text-center">
            <div className="display text-4xl" style={{ color: "var(--federal)" }}>
              ${MACRO_PROOF.federalCollectionsTrillions.toFixed(2)}T
            </div>
            <p className="mt-2 text-xs uppercase tracking-wider text-[var(--muted)]">
              Federal collections (FY2024)
            </p>
          </div>
          <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-6 text-center">
            <div className="display text-4xl" style={{ color: "var(--state)" }}>
              ~${MACRO_PROOF.stateLocalRevenueTrillions.toFixed(1)}T
            </div>
            <p className="mt-2 text-xs uppercase tracking-wider text-[var(--muted)]">
              State + local tax revenue (2024)
            </p>
          </div>
          <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-6 text-center">
            <div className="display text-4xl">≈{MACRO_PROOF.ratio.toFixed(1)}×</div>
            <p className="mt-2 text-xs uppercase tracking-wider text-[var(--muted)]">
              Federal to state-and-local
            </p>
          </div>
        </div>
        <p className="mx-auto mt-6 max-w-3xl text-center text-base leading-relaxed text-[var(--fg-soft)]">
          The federal government collects roughly {MACRO_PROOF.ratio.toFixed(1)}× what every state
          and city takes in combined. That gap is why federal money turns up in almost every project
          you can name — and why "pork" is a judgment laid on top of the arithmetic, not the
          arithmetic itself.
        </p>
      </section>

      {/* 3 · Household calculator */}
      <section className="mt-20">
        <SectionHeading kicker="Make it yours" title="What does your tax dollar split look like?" />
        <HouseholdCalculator
          income={income}
          filing={filing}
          stateCode={stateCode}
          profile={profile}
          result={result}
          states={states}
          onIncomeChange={setIncome}
          onFilingChange={setFiling}
          onStateChange={setStateCode}
        />
      </section>

      {/* 4 · Scaling table */}
      <section className="mt-20">
        <SectionHeading kicker="Up the ladder" title={`How the split scales in ${profile.name}`} />
        <ScalingTable filing={filing} profile={profile} currentIncome={income} />
      </section>

      {/* 5 · State card */}
      <section className="mt-20">
        <SectionHeading kicker="The fine print" title="What makes this state unusual" />
        <StateCard profile={profile} />
      </section>

      {/* 6 · Caveats */}
      <section className="mt-20">
        <SectionHeading kicker="Honesty" title="Where the model gets fuzzy" />
        <CaveatGrid />
      </section>

      {/* 7 · Ponder */}
      <div className="mt-20">
        <PonderSection />
      </div>

      {/* Source-transparency footer (§9) */}
      <footer className="mt-12 border-t border-[var(--border)] pt-6" data-testid="tax-sources">
        <p className="text-xs leading-relaxed text-[var(--muted)]">
          <span className="font-semibold uppercase tracking-wider">Sources · verified tax year 2025</span>
          <br />
          Federal brackets &amp; standard deduction: IRS Rev. Proc. 2024-40; OBBBA (2025). FICA:
          Social Security Administration (2025 wage base $176,100) and Medicare thresholds. Federal
          vs. state/local revenue: IRS / USAFacts FY2024 and U.S. Census Bureau (2024). State income,
          sales &amp; property rates: Tax Foundation 2025. The tax engine is deterministic — no figures
          are generated by AI.
        </p>
        <p className="mt-3 text-xs">
          <Link
            to="/briefings/who-gets-your-tax-dollar/methodology"
            className="font-semibold text-[var(--accent)] hover:underline"
            data-testid="tax-methodology-link"
          >
            Want to recreate the math? Read the full methodology →
          </Link>
        </p>
      </footer>
    </article>
  );
}
