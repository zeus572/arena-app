# 05 — Time Machine

**Real headlines, wrong order.** Sort them by era, or spot the one from this week.
~45 seconds. Read `00_OVERVIEW.md` first.

## The loop

Two modes, alternating by day so the game doesn't wear out:

**Mode A — Sort (default).** Five real headlines, shuffled. Drag them into chronological
order. Reveal shows the true dates and publishers.

**Mode B — Odd one out.** Five headlines on a recurring theme; four are decades old, one is
from this week. Pick the current one.

The reveal line is the whole point: *"Four of these are from 1978, 1991, 2003, and 2011. The
debate is older than it looks."* Recognizing that today's argument has a long history is a
civic skill, and it lands better as a surprise than as an essay.

## Explicit non-goal: never fabricate a headline

The obvious version of this game is "real or fake — spot the misinformation." **We are not
building that.** It requires generating plausible fake news, which means an LLM cost, a
moderation surface, and a table of synthetic misinformation sitting in our database with our
name on it. Sorting *real* headlines delivers the same media-literacy beat with none of it.

Every headline in this game is real, carries a publisher, a date, and a URL, and is
verifiable by the player. No exceptions, in either mode.

## Content source

**Recent side — self-refreshing.** `NewsItem` (`Headline`, `Publisher ?? Source`,
`PublishedAt`, `Url`) is populated continuously by `NewsIngestionService`. Mode B's "this
week" item and Mode A's newest entry come from here. Set `SourceNewsItemId`.

**Archival side — authored.** `backend-civic/Seed/archive-headlines.json`, ~200 items:

```jsonc
{
  "key": "1978-airline-dereg",
  "headline": "Congress Votes to End Airline Route Controls",
  "publisher": "The New York Times",
  "publishedAt": "1978-10-15",
  "url": "https://...",
  "theme": "deregulation",
  "era": "1970s"
}
```

Themes let Mode B assemble a coherent set — five headlines about the same recurring argument
across five decades is far better content than five unrelated ones.

Selection rules:
- Mode A: five items spanning at least three decades, gaps wide enough that the ordering is
  inferable from content rather than a coin flip. Reject any pair less than 4 years apart.
- Mode B: four archival items sharing a `theme`, plus one `NewsItem` from the last 7 days
  whose topic matches. If no current item matches any theme, fall back to Mode A for the day.
- No headline reused within 90 days.

## Payload contract (`PayloadVersion = 1`)

```jsonc
{
  "mode": "sort",                      // "sort" | "oddOneOut"
  "items": [
    { "id": "a", "headline": "…", "publisher": "The New York Times" }
  ],
  "trueOrder": ["c","a","e","b","d"],  // SECRET — strip on GET (sort mode)
  "currentItemId": "d",                // SECRET — strip on GET (odd-one-out mode)
  "dates": { "a": "1978-10-15" },      // SECRET — strip on GET
  "urls":  { "a": "https://…" },       // revealed with the answer
  "revealLine": "The debate is older than it looks."
}
```

Publisher is shown *before* the answer; the date is not. Note the deliberate tradeoff:
showing the publisher makes the puzzle fairer and the content more credible, but a masthead
is itself a weak date hint. That is acceptable — the alternative, anonymous headlines, reads
as untrustworthy, and credibility matters more here than difficulty.

`ResponseJson`: `{ "order": ["a","c","b","e","d"] }` or `{ "pick": "d" }`.

## Scoring

**Mode A** — pairwise concordance (Kendall tau, rescaled), which rewards nearly-right
orderings instead of collapsing to all-or-nothing:

```
concordant = count of pairs (i,j) ordered correctly     // 10 pairs for 5 items
Score      = round(100 * concordant / 10)
```

**Mode B** — 100 correct, 0 incorrect. One guess.

## Neutrality

A headline bank is an editorial artifact. Two balance requirements on the seed file, both
checked by unit test and surfaced on `/admin/daily`:

- **Publisher balance.** No single publisher over 25% of the archival bank, and the set spans
  the mainstream spectrum.
- **Theme balance.** Themes must not cluster on issues that flatter one side of a live
  argument. "The debate is older than it looks" is a neutral observation about
  deregulation, immigration, or media panic alike — the bank should make it about all of them.

Time Machine is on the **review-required** list in the admin queue. A juxtaposition can imply
an argument that no individual headline makes, and that is exactly the kind of thing a human
should see before it ships.

## Share

```
Time Machine #7
Sort — 9/10 pairs
🟩🟩🟩🟨🟩
civersify.com/daily
```

Per-item squares mark whether each landed in its correct slot. Headlines never appear in the
grid.

## Verification

- Every item in both banks has a publisher, a date, and a resolvable URL; a unit test fails
  on any missing field.
- Mode A rejects candidate sets with any pair under 4 years apart.
- Mode B falls back to Mode A when no current `NewsItem` matches an archival theme.
- `GET` responses contain no `trueOrder`, `currentItemId`, or `dates`.
- Kendall scoring: a perfect order scores 100, a fully reversed order scores 0, one adjacent
  swap scores 90.
- Publisher balance test fails if any publisher exceeds 25% of the bank.
- No headline appears in two puzzles within 90 days.
