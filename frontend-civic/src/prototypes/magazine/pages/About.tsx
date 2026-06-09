import { Link } from "react-router-dom";
import { Compass, Handshake, Megaphone, Sparkles } from "lucide-react";

const STEPS = [
  {
    icon: Compass,
    title: "Discover how you'd actually govern",
    body: "Answer a few honest questions and your Civic Compass takes shape — across ten value axes, not a single left-vs-right line. No party labels, no quizzes that put you in a box. Just where you really stand, and where you're still deciding.",
  },
  {
    icon: Handshake,
    title: "Build coalitions around real events",
    body: "Each week's headlines become concrete provisions. Instead of arguing for a side, you take a position, propose a carve-out, and co-sign the wording that pulls the widest coalition together before the deadline. The game rewards bridging — not volume, not dunking.",
  },
  {
    icon: Megaphone,
    title: "Make your voice heard",
    body: "When thousands of people converge on a workable position, that's a signal worth sending. The Zeitgeist turns the patterns in how people are governing themselves into discoveries leaders can actually read.",
  },
];

export default function About() {
  return (
    <section data-testid="about-page" className="max-w-3xl">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
        What this is
      </p>
      <h1 className="display mt-1 text-4xl md:text-5xl">
        Govern together, without the bickering.
      </h1>
      <p className="mt-4 text-lg leading-relaxed text-[var(--fg-soft)]">
        Civersify is a place to build coalitions against the real events of the week —
        without resorting to partisan point-scoring. The country doesn't need more people
        shouting their team's slogans. It needs people who can find workable agreements with
        others who don't think exactly like them. That's the muscle we help you build.
      </p>

      <div className="mt-10 grid gap-4">
        {STEPS.map((s) => (
          <div
            key={s.title}
            className="rounded-2xl border border-[var(--line)] p-6"
            data-testid="about-step"
          >
            <h2 className="flex items-center gap-2 text-lg font-semibold">
              <s.icon size={18} className="text-[var(--accent)]" /> {s.title}
            </h2>
            <p className="mt-2 leading-relaxed text-[var(--fg-soft)]">{s.body}</p>
          </div>
        ))}
      </div>

      <div className="mt-10 rounded-2xl border border-[var(--accent)] bg-[var(--accent)]/5 p-6">
        <p className="flex items-center gap-2 text-sm font-semibold text-[var(--accent)]">
          <Sparkles size={16} /> Why it matters
        </p>
        <p className="mt-2 leading-relaxed text-[var(--fg-soft)]">
          Politics is sold to us as a fight between two tribes. But most real governing is the
          unglamorous work of writing something specific enough to act on and broad enough that
          a coalition will actually sign it. When you practice that here, you discover how you'd
          govern — and you help surface where the public is genuinely ready to move.
        </p>
      </div>

      <div className="mt-8 flex flex-wrap gap-3">
        <Link
          to="/onboarding"
          className="rounded-full bg-[var(--accent)] px-5 py-2.5 text-sm font-semibold text-white"
          data-testid="about-cta-compass"
        >
          Build your Civic Compass →
        </Link>
        <Link
          to="/coalition"
          className="rounded-full border border-[var(--accent)] px-5 py-2.5 text-sm font-semibold text-[var(--accent)]"
          data-testid="about-cta-coalition"
        >
          See this week's coalitions
        </Link>
        <Link
          to="/zeitgeist"
          className="rounded-full border border-[var(--line)] px-5 py-2.5 text-sm font-semibold text-[var(--fg)] hover:border-[var(--accent)]"
          data-testid="about-cta-zeitgeist"
        >
          Read the Zeitgeist
        </Link>
      </div>
    </section>
  );
}
