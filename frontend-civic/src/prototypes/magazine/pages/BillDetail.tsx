import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ExternalLink, Compass, Sparkles } from "lucide-react";
import { getBill, type BillDetail, type BillAxisAlignment } from "@/api/bills";
import { usePrefersReducedMotion } from "@/lib/useReducedMotion";
import CompassRadial, { type RadialAxis } from "../components/CompassRadial";
import { ButtonLink } from "../components/Button";

const NEUTRAL_BAND = 0.15;

/** Client-side mirror of the backend BillAlignment.Classify so explore-mode sliders update live. */
function classify(userScore: number, billScore: number): "aligned" | "mixed" | "tension" {
  if (Math.abs(userScore) < NEUTRAL_BAND || Math.abs(billScore) < NEUTRAL_BAND) return "mixed";
  return Math.sign(userScore) === Math.sign(billScore) ? "aligned" : "tension";
}

function overallPercent(pairs: { u: number; b: number; conf: number }[]): number | null {
  let weight = 0;
  let sum = 0;
  for (const { u, b, conf } of pairs) {
    const w = Math.max(0.05, Math.min(1, conf));
    sum += (1 - Math.abs(u - b) / 2) * w;
    weight += w;
  }
  return weight <= 0 ? null : Math.round((100 * sum) / weight);
}

function AlignmentBadge({ percent }: { percent: number }) {
  const tone =
    percent >= 66 ? "bg-emerald-100 text-emerald-700" : percent >= 40 ? "bg-amber-100 text-amber-700" : "bg-rose-100 text-rose-700";
  return (
    <span className={`rounded-full px-3 py-1 text-sm font-semibold ${tone}`} data-testid="overall-alignment">
      {percent}% aligned with your compass
    </span>
  );
}

/** A compact slider used in explore mode to let a signed-out visitor set their own lean. */
function ExploreSlider({
  axis,
  value,
  onChange,
}: {
  axis: BillAxisAlignment;
  value: number;
  onChange: (v: number) => void;
}) {
  const align = classify(value, axis.billScore);
  const tone = align === "aligned" ? "text-emerald-600" : align === "tension" ? "text-[var(--state)]" : "text-[var(--muted)]";
  return (
    <div className="border-t border-[var(--border)] pt-4" data-testid={`explore-${axis.axisKey}`}>
      <div className="flex items-baseline justify-between">
        <p className="text-sm font-semibold">{axis.axisName}</p>
        <span className={`text-[11px] font-semibold uppercase tracking-wider ${tone}`}>{align}</span>
      </div>
      <div className="mt-2 flex items-center justify-between text-[11px] text-[var(--muted)]">
        <span>{axis.lowLabel}</span>
        <span>{axis.highLabel}</span>
      </div>
      <input
        type="range"
        min={-1}
        max={1}
        step={0.1}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="mt-1 w-full accent-[var(--accent)]"
        aria-label={`Your position on ${axis.axisName}`}
      />
    </div>
  );
}

export default function MagazineBillDetail() {
  const { id = "" } = useParams();
  const reduced = usePrefersReducedMotion();
  const [bill, setBill] = useState<BillDetail | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [exploreScores, setExploreScores] = useState<Record<string, number>>({});

  useEffect(() => {
    setLoaded(false);
    void getBill(id)
      .then((b) => {
        setBill(b);
        if (!b.hasUserCompass) {
          setExploreScores(Object.fromEntries(b.axes.map((a) => [a.axisKey, 0])));
        }
      })
      .finally(() => setLoaded(true));
  }, [id]);

  const explore = bill != null && !bill.hasUserCompass;

  // Radial axes: signed-in uses server alignment; explore mode folds in the live slider values.
  const radialAxes: RadialAxis[] = useMemo(() => {
    if (!bill) return [];
    return bill.axes.map((a) => {
      const userScore = explore ? exploreScores[a.axisKey] ?? 0 : a.userScore;
      const alignment =
        userScore == null ? null : explore ? classify(userScore, a.billScore) : a.alignment;
      return {
        axisKey: a.axisKey,
        axisName: a.axisName,
        lowLabel: a.lowLabel,
        highLabel: a.highLabel,
        billScore: a.billScore,
        billConfidence: a.billConfidence,
        rationale: a.rationale,
        userScore: explore ? userScore : a.userScore,
        alignment,
      };
    });
  }, [bill, explore, exploreScores]);

  const exploreOverall = useMemo(() => {
    if (!bill || !explore) return null;
    return overallPercent(
      bill.axes.map((a) => ({ u: exploreScores[a.axisKey] ?? 0, b: a.billScore, conf: a.billConfidence })),
    );
  }, [bill, explore, exploreScores]);

  if (!loaded) {
    return (
      <p className="py-12 text-sm text-[var(--muted)]" data-testid="bill-loading">
        Loading the bill…
      </p>
    );
  }

  if (!bill) {
    return (
      <div className="py-12" data-testid="bill-not-found">
        <p className="text-base text-[var(--muted)]">That bill couldn't be found.</p>
        <Link to="/bills" className="mt-3 inline-block text-sm font-semibold text-[var(--accent)]">
          ← All bills
        </Link>
      </div>
    );
  }

  const showUser = radialAxes.some((a) => a.userScore != null);

  return (
    <article data-testid="magazine-bill-detail" className="pb-16">
      <Link
        to="/bills"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
      >
        ← All bills
      </Link>

      <header className="mt-6">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">{bill.identifier}</p>
        <h1 className="display mt-2 text-4xl leading-tight md:text-5xl">{bill.title}</h1>
        <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-[var(--muted)]">
          <span>{bill.sponsor}{bill.party ? ` (${bill.party})` : ""}</span>
          <span>· {bill.status.replace(/([A-Z])/g, " $1").trim()}</span>
          {bill.fullTextUrl && (
            <a
              href={bill.fullTextUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 font-semibold text-[var(--accent)]"
            >
              Full text <ExternalLink className="h-3.5 w-3.5" />
            </a>
          )}
        </div>
      </header>

      <div className="mt-8 grid gap-10 md:grid-cols-[1fr_360px]">
        {/* Center: the bill + its compass */}
        <div>
          <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5">
            {bill.synthesisSummary && (
              <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
                <Sparkles className="h-3.5 w-3.5" /> What it does
              </p>
            )}
            <p className="mt-2 text-base leading-relaxed text-[var(--fg)]">
              {bill.synthesisSummary || bill.summary}
            </p>
          </div>

          <div className="mt-8">
            <div className="flex items-center justify-between">
              <h2 className="display text-2xl">The bill on your compass</h2>
              {!explore && bill.overallAlignmentPercent != null && (
                <AlignmentBadge percent={bill.overallAlignmentPercent} />
              )}
              {explore && exploreOverall != null && <AlignmentBadge percent={exploreOverall} />}
            </div>
            <div className="mt-4">
              <CompassRadial axes={radialAxes} showUser={showUser} reducedMotion={reduced} />
            </div>
          </div>

          {/* Per-axis breakdown */}
          <div className="mt-8">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
              Axis by axis
            </h3>
            <div className="mt-3 grid gap-3">
              {radialAxes.map((a) => (
                <div
                  key={a.axisKey}
                  className="border border-[var(--border)] p-3"
                  data-testid={`axis-detail-${a.axisKey}`}
                >
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-semibold">{a.axisName}</p>
                    <span className="text-xs text-[var(--muted)]">
                      leans {a.billScore >= 0 ? a.highLabel : a.lowLabel}
                    </span>
                  </div>
                  <p className="mt-1 text-xs leading-relaxed text-[var(--fg-soft)]">{a.rationale}</p>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Rail: your compass (signed in) or explore sliders (signed out) */}
        <aside className="md:sticky md:top-24 md:self-start">
          {explore ? (
            <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5" data-testid="explore-panel">
              <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
                <Compass className="h-3.5 w-3.5" /> Explore your values
              </p>
              <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">
                Drag each slider to where you stand. We'll show live where you align with — and clash
                against — this bill.
              </p>
              <div className="mt-4 grid gap-3">
                {bill.axes.map((a) => (
                  <ExploreSlider
                    key={a.axisKey}
                    axis={a}
                    value={exploreScores[a.axisKey] ?? 0}
                    onChange={(v) => setExploreScores((s) => ({ ...s, [a.axisKey]: v }))}
                  />
                ))}
              </div>
              <div className="mt-5 border-t border-[var(--border)] pt-4 text-center">
                <p className="text-sm text-[var(--fg-soft)]">Want this saved to a real compass?</p>
                <ButtonLink to="/onboarding" className="mt-2" data-testid="explore-onboarding-cta">
                  Take the values quiz
                </ButtonLink>
              </div>
            </div>
          ) : (
            <div className="border border-[var(--border)] bg-[var(--bg-elev)] p-5" data-testid="your-compass-panel">
              <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[var(--accent)]">
                <Compass className="h-3.5 w-3.5" /> Your compass
              </p>
              <p className="mt-2 text-sm leading-relaxed text-[var(--fg-soft)]">
                The dashed outline on the chart is you. Green spokes are where you and this bill agree;
                red spokes are where you pull apart.
              </p>
              <div className="mt-4 grid gap-2">
                {radialAxes
                  .filter((a) => a.userScore != null)
                  .map((a) => {
                    const tone =
                      a.alignment === "aligned"
                        ? "text-emerald-600"
                        : a.alignment === "tension"
                          ? "text-[var(--state)]"
                          : "text-[var(--muted)]";
                    return (
                      <div key={a.axisKey} className="flex items-center justify-between text-sm">
                        <span className="text-[var(--fg-soft)]">{a.axisName}</span>
                        <span className={`font-semibold ${tone}`}>{a.alignment}</span>
                      </div>
                    );
                  })}
              </div>
              <div className="mt-5 border-t border-[var(--border)] pt-4 text-center">
                <ButtonLink to="/profile" variant="ghost" className="w-full">
                  View full compass
                </ButtonLink>
              </div>
            </div>
          )}
        </aside>
      </div>
    </article>
  );
}
