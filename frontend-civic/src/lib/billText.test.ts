import { describe, expect, it } from "vitest";
import { fullBillText, isTeaserTruncated } from "./billText";

describe("bill teaser truncation", () => {
  it("spots a teaser the server cut short", () => {
    expect(isTeaserTruncated("Reauthorizes the FAA for five years, funding air…")).toBe(true);
  });

  it("leaves a complete teaser alone, so no dead 'See more' appears", () => {
    expect(isTeaserTruncated("Reauthorizes the FAA for five years.")).toBe(false);
  });

  it("tolerates trailing whitespace around the ellipsis", () => {
    expect(isTeaserTruncated("Something long…  ")).toBe(true);
  });

  it("treats an empty or missing teaser as complete", () => {
    expect(isTeaserTruncated("")).toBe(false);
    expect(isTeaserTruncated(null)).toBe(false);
    expect(isTeaserTruncated(undefined)).toBe(false);
  });

  it("does not mistake three periods for the ellipsis the server writes", () => {
    // BillMappings.Teaser appends U+2026, not "...". A summary that genuinely ends in an
    // ellipsis-looking run of periods is complete text, and expanding it would fetch the
    // same string back.
    expect(isTeaserTruncated("...and so on...")).toBe(false);
  });
});

describe("full bill text", () => {
  it("prefers the neutral synthesis, matching BillMappings.Teaser's source order", () => {
    expect(
      fullBillText({ synthesisSummary: "The neutral read.", summary: "The source blurb." }),
    ).toBe("The neutral read.");
  });

  it("falls back to the source summary when there is no synthesis", () => {
    expect(fullBillText({ synthesisSummary: null, summary: "The source blurb." })).toBe(
      "The source blurb.",
    );
  });

  it("treats a whitespace-only synthesis as absent", () => {
    // Otherwise "See more" would expand a truncated teaser into a blank paragraph.
    expect(fullBillText({ synthesisSummary: "   ", summary: "The source blurb." })).toBe(
      "The source blurb.",
    );
  });
});
