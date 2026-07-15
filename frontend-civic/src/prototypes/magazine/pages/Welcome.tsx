import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getBriefings } from "@/api/briefings";
import type { CivicBriefingSummary } from "@/api/types";
import { DEBATE_ARENA_URL } from "@/lib/links";
import "../theme.css";
import "./welcome.css";

// Static fallback so the page is fully compelling for social-media / crawler
// visitors even when the civic API is cold or unavailable.
const FALLBACK_FEATURE = {
  slug: null as string | null,
  headline: "This week’s biggest fight — rewritten as a bill you can shape.",
  summary30:
    "Instead of arguing for a team, you take a position, propose the carve-out that makes it workable, and co-sign the wording that pulls the widest coalition together.",
};

const STEPS = [
  {
    n: "01",
    title: "Position",
    body: "Say where you actually stand on the week’s bill. No party label required — just your honest read.",
  },
  {
    n: "02",
    title: "Carve-out",
    body: "Propose the exception that makes it work for people who don’t think like you. This is where deals get real.",
  },
  {
    n: "03",
    title: "Co-sign",
    body: "Back the wording that pulls the widest coalition together before the deadline. Bridging wins — not volume, not dunking.",
  },
];

export default function Welcome() {
  const [feature, setFeature] = useState(FALLBACK_FEATURE);

  useEffect(() => {
    let active = true;
    // Best-effort: surface a real, current briefing headline. Silent fallback.
    getBriefings(1, 1)
      .then((page) => {
        const top: CivicBriefingSummary | undefined = page.items[0];
        if (active && top) {
          setFeature({ slug: top.slug, headline: top.headline, summary30: top.summary30 });
        }
      })
      .catch(() => {
        /* keep the fallback feature */
      });
    return () => {
      active = false;
    };
  }, []);

  const featureHref = feature.slug ? `/briefings/${feature.slug}` : "/register";

  return (
    <div className="theme-magazine welcome">
      {/* HERO */}
      <header className="welcome-hero">
        <div className="welcome-wrap">
          <p className="welcome-eyebrow reveal" style={{ ["--d" as string]: "0ms" }}>
            Civersify · Civics without the shouting
          </p>

          <h1 className="welcome-headline reveal" style={{ ["--d" as string]: "80ms" }}>
            Stop picking <span className="welcome-strike">a side</span>.
            <br />
            Start writing the <span className="welcome-em">deal</span>.
          </h1>

          {/* Signature: a hairline that bridges the two brand colors — the whole
              product in one mark. */}
          <div className="welcome-bridge reveal" style={{ ["--d" as string]: "160ms" }} aria-hidden>
            <span className="welcome-bridge-left" />
            <span className="welcome-bridge-node" />
            <span className="welcome-bridge-right" />
          </div>

          <p className="welcome-sub reveal" style={{ ["--d" as string]: "220ms" }}>
            Every week’s headline becomes a concrete bill you can actually shape.
            Take a position, propose a carve-out, and co-sign the wording that
            pulls the widest coalition together — before the deadline.
          </p>

          <div className="welcome-cta reveal" style={{ ["--d" as string]: "300ms" }}>
            <Link to="/register" className="welcome-btn welcome-btn--solid" data-testid="welcome-signup">
              Create your account →
            </Link>
            <a href="#this-week" className="welcome-btn welcome-btn--ghost">
              See this week’s fight
            </a>
          </div>
        </div>
      </header>

      {/* THIS WEEK — featured briefing */}
      <section id="this-week" className="welcome-section welcome-feature" data-testid="welcome-feature">
        <div className="welcome-wrap">
          <p className="welcome-kicker">This week on Civersify</p>
          <a href={featureHref} className="welcome-feature-card">
            <h2 className="welcome-feature-head">{feature.headline}</h2>
            <p className="welcome-feature-sum">{feature.summary30}</p>
            <span className="welcome-feature-link">Read the briefing →</span>
          </a>
        </div>
      </section>

      {/* THE MECHANIC — a real, ordered sequence, so the numbering earns its place */}
      <section className="welcome-section">
        <div className="welcome-wrap">
          <p className="welcome-kicker">How a week plays out</p>
          <div className="welcome-steps">
            {STEPS.map((s) => (
              <div key={s.n} className="welcome-step">
                <span className="welcome-step-n">{s.n}</span>
                <h3 className="welcome-step-title">{s.title}</h3>
                <p className="welcome-step-body">{s.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* THE THESIS — ten axes, not one line */}
      <section className="welcome-section welcome-axes-section">
        <div className="welcome-wrap welcome-axes-grid">
          <div>
            <p className="welcome-kicker">The premise</p>
            <h2 className="welcome-axes-head">
              You’re not a point on a left–right line.
            </h2>
            <p className="welcome-axes-body">
              Across ten value axes, your Civic Compass shows how you’d actually
              govern — and where you’re still deciding. No box, no team, no single
              slider that pretends to sum you up.
            </p>
          </div>
          <div className="welcome-axes" aria-hidden>
            {Array.from({ length: 10 }).map((_, i) => (
              <span
                key={i}
                className={`welcome-axis${i === 6 ? " welcome-axis--mark" : ""}`}
                style={{ ["--h" as string]: `${28 + ((i * 37) % 60)}%`, ["--i" as string]: String(i) }}
              />
            ))}
          </div>
        </div>
      </section>

      {/* CLOSE */}
      <section className="welcome-section welcome-close">
        <div className="welcome-wrap welcome-close-inner">
          <h2 className="welcome-close-head">Find the workable agreement.</h2>
          <p className="welcome-close-sub">
            The country doesn’t need more people shouting their team’s slogans. It
            needs people who can write something a coalition will sign. That’s the
            muscle we help you build.
          </p>
          <div className="welcome-cta">
            <Link to="/register" className="welcome-btn welcome-btn--solid" data-testid="welcome-signup-2">
              Create your account →
            </Link>
            <a
              href={DEBATE_ARENA_URL}
              target="_blank"
              rel="noreferrer"
              className="welcome-btn welcome-btn--ghost"
            >
              Enter the Debate Arena ↗
            </a>
          </div>
          <p className="welcome-signin">
            Already have an account?{" "}
            <Link to="/login" className="welcome-signin-link">
              Sign in
            </Link>
          </p>
        </div>
      </section>
    </div>
  );
}
