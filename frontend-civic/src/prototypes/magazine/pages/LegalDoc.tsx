import type { ReactNode } from "react";
import { LEGAL_EFFECTIVE_DATE } from "@/lib/legal";

export type LegalSection = {
  heading: string;
  /** Paragraphs and/or bullet lists rendered in order. */
  body: ReactNode;
};

/**
 * Shared shell for the Privacy Policy / Terms / EULA. Renders a magazine-styled
 * masthead, an effective date, an optional lead paragraph, and numbered
 * sections. Content lives in the individual page components.
 */
export default function LegalDoc({
  kicker,
  title,
  lead,
  sections,
  testid,
}: {
  kicker: string;
  title: string;
  lead?: ReactNode;
  sections: LegalSection[];
  testid: string;
}) {
  return (
    <section data-testid={testid} className="max-w-3xl">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent)]">
        {kicker}
      </p>
      <h1 className="display mt-1 text-4xl md:text-5xl">{title}</h1>
      <p className="mt-3 text-xs uppercase tracking-[0.2em] text-[var(--muted)]">
        Effective {LEGAL_EFFECTIVE_DATE}
      </p>

      <div className="mt-4 rounded-lg border border-[var(--line)] bg-[var(--accent)]/5 px-4 py-3 text-xs leading-relaxed text-[var(--fg-soft)]">
        This is a plain-language summary intended to be readable. It is provided
        as-is and does not constitute legal advice.
      </div>

      {lead && (
        <p className="mt-6 text-lg leading-relaxed text-[var(--fg-soft)]">{lead}</p>
      )}

      <div className="mt-8 flex flex-col gap-8">
        {sections.map((s, i) => (
          <div key={s.heading} data-testid="legal-section">
            <h2 className="text-lg font-semibold">
              <span className="text-[var(--accent)]">{i + 1}.</span> {s.heading}
            </h2>
            <div className="mt-2 flex flex-col gap-3 leading-relaxed text-[var(--fg-soft)]">
              {s.body}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
