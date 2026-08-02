import { describe, expect, it } from "vitest";
import { CIVIC_QUOTES, quoteOfDay, rotatingQuote, sessionQuotes } from "./quotes";

describe("civic quote library", () => {
  it("has stable, unique ids", () => {
    const ids = CIVIC_QUOTES.map((q) => q.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("never repeats the same line, even from the same speaker", () => {
    const texts = CIVIC_QUOTES.map((q) => q.text.toLowerCase());
    expect(new Set(texts).size).toBe(texts.length);
  });

  it("cites a checkable source for every quote", () => {
    // The guard that keeps misattributions out: no entry may land here without a
    // primary document someone can go read. See the header note in quotes.ts.
    for (const q of CIVIC_QUOTES) {
      expect(q.source.trim(), q.id).not.toBe("");
      expect(q.speaker.trim(), q.id).not.toBe("");
      expect(q.context.trim(), q.id).not.toBe("");
      expect(q.year.trim(), q.id).not.toBe("");
      expect(q.text.trim(), q.id).not.toBe("");
    }
  });

  it("is large enough that the surfaces stay fresh", () => {
    // Not a style rule — the whole design rests on the pool being deep enough that a
    // daily footer quote takes months to wrap around.
    expect(CIVIC_QUOTES.length).toBeGreaterThanOrEqual(100);
  });

  it("draws on more than a handful of voices", () => {
    const speakers = new Set(CIVIC_QUOTES.map((q) => q.speaker));
    expect(speakers.size).toBeGreaterThanOrEqual(60);
  });
});

describe("stride walk", () => {
  it("visits every quote before repeating one", () => {
    // The core freshness guarantee: a full cycle is a permutation, not a sample.
    const seen = Array.from({ length: CIVIC_QUOTES.length }, (_, i) => rotatingQuote(i, 0).id);
    expect(new Set(seen).size).toBe(CIVIC_QUOTES.length);
  });

  it("wraps around after a full cycle rather than running off the end", () => {
    expect(rotatingQuote(CIVIC_QUOTES.length, 0).id).toBe(rotatingQuote(0, 0).id);
  });

  it("keeps consecutive rotations far apart in the library", () => {
    // Adjacent array entries are often the same speaker (two Madisons, two Brandeis).
    // The stride exists so the reader never gets those back to back.
    const idx = (id: string) => CIVIC_QUOTES.findIndex((q) => q.id === id);
    for (let seed = 0; seed < CIVIC_QUOTES.length; seed++) {
      const gap = Math.abs(idx(rotatingQuote(seed + 1, 0).id) - idx(rotatingQuote(seed, 0).id));
      expect(gap).toBeGreaterThan(1);
    }
  });

  it("gives each session a different starting point", () => {
    expect(rotatingQuote(0, 3).id).not.toBe(rotatingQuote(0, 4).id);
  });
});

describe("quoteOfDay", () => {
  const day = (iso: string) => new Date(`${iso}T12:00:00`);

  it("is stable within a day, so the footer doesn't reshuffle as you navigate", () => {
    expect(quoteOfDay({ date: day("2026-08-01") }).id).toBe(
      quoteOfDay({ date: new Date("2026-08-01T23:14:00") }).id,
    );
  });

  it("changes from one day to the next", () => {
    expect(quoteOfDay({ date: day("2026-08-01") }).id).not.toBe(
      quoteOfDay({ date: day("2026-08-02") }).id,
    );
  });

  it("runs a full library cycle before repeating a day's quote", () => {
    const start = day("2026-08-01");
    const ids = Array.from({ length: CIVIC_QUOTES.length }, (_, i) => {
      const d = new Date(start);
      d.setDate(d.getDate() + i);
      return quoteOfDay({ date: d }).id;
    });
    expect(new Set(ids).size).toBe(CIVIC_QUOTES.length);
  });

  it("honors maxLength by walking on rather than truncating the quotation", () => {
    // Every day of a full cycle must yield a short quote — otherwise the footer
    // would clip someone's words on some dates, which is a misquotation.
    const start = day("2026-08-01");
    for (let i = 0; i < CIVIC_QUOTES.length; i++) {
      const d = new Date(start);
      d.setDate(d.getDate() + i);
      expect(quoteOfDay({ date: d, maxLength: 120 }).text.length).toBeLessThanOrEqual(120);
    }
  });
});

describe("sessionQuotes", () => {
  it("returns the requested number of distinct quotes", () => {
    const ids = sessionQuotes(12, 0).map((q) => q.id);
    expect(ids).toHaveLength(12);
    expect(new Set(ids).size).toBe(12);
  });

  it("can't be asked for more than the library holds", () => {
    expect(sessionQuotes(9999, 0)).toHaveLength(CIVIC_QUOTES.length);
  });

  it("starts somewhere different each session", () => {
    expect(sessionQuotes(3, 10)[0].id).not.toBe(sessionQuotes(3, 11)[0].id);
  });
});
