export type Institution =
  | "Congress"
  | "Supreme Court"
  | "Executive"
  | "State Government";

export type Branch = "Legislative" | "Judicial" | "Executive" | "State";

export type AudienceLevel =
  | "Middle School"
  | "High School"
  | "College"
  | "Young Adult";

export type CivicBriefingSummary = {
  id: string;
  slug: string;
  headline: string;
  institution: Institution;
  branch: Branch;
  status: string;
  audienceLevel: AudienceLevel;
  keyConcept: string;
  tags: string[];
  summary30: string;
};

export type CivicBriefing = CivicBriefingSummary & {
  summary3Min: string;
  summary10Min: string;
  whoActed: string;
  whatChanged: string;
  whyItMatters: string;
  wordsToKnow: { term: string; definition: string }[];
  disagreement: string;
  strongestArgumentFor: string;
  strongestArgumentAgainst: string;
  valuesInConflict: string[];
  thinkDeeperQuestion: string;
  relatedConcepts: string[];
  whereToGoNext: string[];
  // Original-source attribution (null for hand-seeded briefings with no upstream article).
  sourceUrl: string | null;
  sourcePublisher: string | null;
  sourcePublishedAt: string | null;
};

export type Concept = {
  id: string;
  slug: string;
  title: string;
  category: string;
  plainDefinition: string;
  whyItMatters: string;
  whereYouSeeIt: string[];
  currentExample: string;
  commonMisunderstanding: string;
  relatedConcepts: string[];
  tryItQuestion: string;
};

export type ThinkDeeper = {
  id: string;
  slug: string;
  issue: string;
  firstReactionPrompt: string;
  values: string[];
  strongestArgumentA: string;
  strongestArgumentB: string;
  whatSideAMayMiss: string;
  whatSideBMayMiss: string;
  whatWouldChangeYourMind: string[];
  canBothBeTrue: string;
  buildYourViewPrompt: string;
};
