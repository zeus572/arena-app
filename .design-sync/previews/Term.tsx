import { Term } from "frontend-civic";

// Canonical: a glossary term woven into a sentence of body text. The dotted
// underline marks the affordance; hover/focus reveals the definition bubble.
export const InProse = () => (
  <p style={{ maxWidth: 520, fontSize: 16, lineHeight: 1.6 }}>
    A coalition forms around a single <Term term="plank">plank</Term> — the one
    concrete provision everyone is willing to sign. Stronger bills give that
    provision real <Term term="teeth">teeth</Term> instead of symbolic language.
  </p>
);

// The four known glossary terms, each rendered as an affordance in a line.
export const KnownTerms = () => (
  <p style={{ maxWidth: 520, fontSize: 16, lineHeight: 1.8 }}>
    <Term term="plank">Plank</Term> · <Term term="teeth">Teeth</Term> ·{" "}
    <Term term="promote">Promote</Term> · <Term term="relegate">Relegate</Term>
  </p>
);

// Unknown term → renders as plain text with no affordance (a typo never hides
// content). Shown next to a known term so the difference is visible.
export const UnknownFallsBack = () => (
  <p style={{ maxWidth: 520, fontSize: 16, lineHeight: 1.6 }}>
    Win enough rounds and you <Term term="promote">promote</Term> to a tougher
    league; the word <Term term="quorum">quorum</Term> has no glossary entry, so
    it stays plain.
  </p>
);
