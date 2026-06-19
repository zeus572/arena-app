import { CoverStory } from "frontend-civic";

// The full-bleed magazine cover: a tall gradient hero with the "Cover story"
// badge, the institution·status eyebrow, a large serif headline and a clamped
// summary. Links to the briefing detail.

const congressCover = {
  id: "br-farm-bill",
  slug: "senate-passes-farm-bill",
  headline: "The Farm Bill Finally Moves",
  institution: "Congress",
  branch: "Legislative",
  status: "Passed Senate",
  audienceLevel: "High School",
  keyConcept: "Reconciliation",
  tags: ["agriculture", "SNAP"],
  summary30:
    "After a year of stalled negotiations, the Senate cleared a five-year farm bill that renews crop insurance and reauthorizes food assistance, sending it to the House for a final vote.",
  createdAt: "2026-06-17T14:30:00Z",
  thinkDeeperQuestion:
    "Should food assistance and farm subsidies share one bill?",
  locality: null,
};

const courtCover = {
  id: "br-scotus-emissions",
  slug: "court-limits-epa-authority",
  headline: "The Court Redraws the Line on Agency Power",
  institution: "Supreme Court",
  branch: "Judicial",
  status: "Decided 6-3",
  audienceLevel: "College",
  keyConcept: "Major questions doctrine",
  tags: ["environment", "regulation"],
  summary30:
    "A divided Court held that regulators overstepped their statutory authority, requiring clearer direction from Congress before agencies impose economy-wide rules.",
  createdAt: "2026-06-16T09:00:00Z",
  thinkDeeperQuestion:
    "Where should the line fall between agency expertise and congressional intent?",
  locality: null,
};

export const CongressCover = () => (
  <div style={{ maxWidth: 980 }}>
    <CoverStory briefing={congressCover as any} />
  </div>
);

export const CourtCover = () => (
  <div style={{ maxWidth: 980 }}>
    <CoverStory briefing={courtCover as any} />
  </div>
);
