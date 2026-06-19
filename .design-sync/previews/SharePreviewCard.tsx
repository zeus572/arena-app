import { SharePreviewCard } from "frontend-civic";

// A shareable briefing preview: gradient hero with the eyebrow, serif headline
// and 30-second summary, plus a footer share action.

const senateBriefing = {
  id: "br-farm-bill",
  slug: "senate-passes-farm-bill",
  headline: "Senate Passes the 2026 Farm Bill After Months of Deadlock",
  institution: "Congress",
  branch: "Legislative",
  status: "Passed Senate",
  audienceLevel: "High School",
  keyConcept: "Reconciliation",
  tags: ["agriculture", "SNAP", "appropriations"],
  summary30:
    "After a year of stalled negotiations, the Senate cleared a five-year farm bill that renews crop insurance and reauthorizes SNAP, sending it to the House for a final vote.",
  createdAt: "2026-06-17T14:30:00Z",
  thinkDeeperQuestion:
    "Should food assistance and farm subsidies be funded in the same bill?",
  locality: null,
};

const courtBriefing = {
  id: "br-scotus-emissions",
  slug: "court-limits-epa-authority",
  headline: "Supreme Court Narrows the EPA's Power to Set Emissions Caps",
  institution: "Supreme Court",
  branch: "Judicial",
  status: "Decided 6-3",
  audienceLevel: "College",
  keyConcept: "Major questions doctrine",
  tags: ["environment", "regulation", "separation of powers"],
  summary30:
    "In a 6-3 ruling, the Court held that the agency overstepped its statutory authority, requiring clearer direction from Congress before regulators can impose economy-wide emissions limits.",
  createdAt: "2026-06-16T09:00:00Z",
  thinkDeeperQuestion:
    "Where should the line fall between agency expertise and congressional intent?",
  locality: null,
};

export const FarmBill = () => (
  <div style={{ maxWidth: 680 }}>
    <SharePreviewCard briefing={senateBriefing as any} />
  </div>
);

export const CourtRuling = () => (
  <div style={{ maxWidth: 680 }}>
    <SharePreviewCard briefing={courtBriefing as any} />
  </div>
);
