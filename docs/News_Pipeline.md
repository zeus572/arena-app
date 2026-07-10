# Civic News Pipeline

How the Civic Arena turns real-world news into the briefings that fill the main
feed, and how that selection is balanced across sources and localities.

Lives in `backend-civic/Services/Generation/` plus the shared provider layer in
`shared/Arena.Shared/News/`.

## Stages

```
news sources ─▶ NewsIngestionService ──▶ NewsItems (Status=Ingested)
(Rss | GoogleNews)                            │
                                              ▼
                                   CivicContentGenerationService
                                   (Haiku gate → Sonnet write)
                                              │
                                              ▼
                                   Briefings (+ ThinkDeeper, Concept, Quiz)
                                              │
                                              ▼
                                   GET /api/briefings  ──▶  feed
```

1. **Ingest** — `NewsIngestionService` (every `News:IngestIntervalHours`, default 2h)
   pulls each configured source via the shared `AggregateNewsFeed` (sources built by
   `NewsSourceFactory` from the typed `News:Sources` descriptors), dedupes, clamps
   over-long fields, and upserts `NewsItem` rows (idempotent by `ExternalId`, plus a
   headline dedupe window — see below). The national feed is tagged `Locality = null`;
   per-locality feeds from `News:LocalSources` are tagged with their state code.
2. **Generate** — `CivicContentGenerationService` (every
   `News:GenerationIntervalMinutes`, default 120m) selects pending `NewsItem`s and
   produces a `Briefing` (+ paired `ThinkDeeper`, and optionally a `Concept` /
   `QuizQuestion`) via the shared `ILlmClient`. Honors `News:MaxItemsPerDay`
   (default 10) and `News:BatchSize` (default 5).
3. **Serve** — `BriefingsController.List` returns national briefings plus the
   caller's own locality, ordered by `IssueOrder` then recency.

Only **Briefings** are shown in the feed. A `NewsItem` that is ingested but never
generated is invisible to readers — which is the root of the two bugs below.

## Configuration (`News:*`)

| Key | Default | Meaning |
|---|---|---|
| `IngestIntervalHours` | 2 | How often ingestion runs |
| `GenerationIntervalMinutes` | 120 | How often generation runs |
| `BatchSize` | 5 | Items processed per generation tick |
| `MaxItemsPerDay` | 10 | Hard cap on briefings generated per trailing 24h |
| `MaxStoryAgeDays` | 14 | Stories whose `PublishedAt` is older are never generated |
| `HeadlineDedupeWindowDays` | 3 | Incoming items whose headline matches one ingested this recently are skipped (0 disables) |
| `Sources` | NPR + 3 Google News channels | National sources (`name → { Kind, ... }` descriptor) |
| `LocalSources` | WA, MD, CA | Per-state sources (`state → {name → descriptor}`), each a local outlet + a Google News geo channel |

### Source descriptors and providers

Every source entry is a typed `NewsSourceConfig` (`shared/Arena.Shared/News/`):
the entry's key is the source name (which becomes `NewsItem.Source` and its own
selection bucket), and `Kind` picks the provider:

- `"Kind": "Rss"` — plain RSS/Atom; needs `Url`.
- `"Kind": "GoogleNews"` — Google News RSS; `Feed` is `Top`, `Topic` (+`Topic` id,
  e.g. `POLITICS`/`NATION`), `Geo` (+`Location`), or `Search` (+`Query`). Pinned to
  the US English edition. **Geo gotcha:** locations must be city-level —
  state names ("Maryland", "California") return a 200 with an *empty* feed. We
  use state capitals ("Olympia, Washington", "Annapolis, Maryland",
  "Sacramento, California"), which also skew toward state-government news.

Optional on any source: `Enabled` (default true), `MaxEntries` (default 15).
Invalid entries (unknown `Kind`, missing `Url`/`Topic`/...) are skipped with a
warning — a config typo never takes down ingestion. **BBC (UK front page) and
Cascade PBS were dropped** when the Google News channels were added (2026-07).

**Adding a news provider:** implement `INewsSourceBuilder` (build an
`INewsSource` from a `NewsSourceConfig`, return null on bad config), register it
in `NewsServiceCollectionExtensions.AddArenaNewsSources()`, and reference its
`Kind` from config. Nothing in `Program.cs` or `NewsIngestionService` changes.

### Publisher attribution and headline dedupe

Aggregator items carry the real outlet in `NewsItem.Publisher` (e.g. Source
`"Google News Politics"`, Publisher `"Reuters"`); direct-feed items leave it
null. Everything reader-facing shows `Publisher ?? Source` (the
`SourcePublisher` DTO field / `SourceBadge`).

Aggregators also re-surface stories the direct feeds already delivered (and the
GN channels overlap each other), with different `ExternalId`s — so ingestion
additionally skips any item whose headline (case-insensitive) matches one
ingested within `HeadlineDedupeWindowDays`. First copy wins; national ingests
before locals within a tick.

## Model tiers (Haiku vs Sonnet)

Two tiers only (`LlmModelTier`, `shared/Arena.Shared/Llm/ILlmClient.cs`), mapped to
models in `ClaudeLlmClient` (`HaikuModel`, `SonnetModel` in `AnthropicOptions`).

The news generation path (`CivicContentGenerationService.GenerateForItemAsync`)
**already follows a cheap-gate-first pattern**: a Haiku eligibility call runs before
any Sonnet work and short-circuits ineligible stories.

| Step | Model | Call | Purpose |
|---|---|---|---|
| 0 | **Haiku** (128 tok) | `RelevanceGate` | Eligibility gate — is this civic? If not, skip before any Sonnet. |
| 1 | Sonnet | `Briefing` | The briefing |
| 2 | Sonnet | `ThinkDeeper` | Paired analysis |
| 3 | **Haiku** (256 tok) | `ContentJudge` | Should we also make a Concept/Quiz? |
| 4 | Sonnet ×0–2 | `Concept` / `QuizQuestion` | Only if step 3 approves |

So an *eligible* item costs ≈ **2 Haiku + 2–4 Sonnet** calls; Sonnet dominates.
Ineligible items cost a single Haiku call and are marked `Skipped`.

Haiku is also used outside this pipeline: the Coalition judges (`CoalitionJudge`,
`TwoFramingsService`) and `AgentProfileMapper`. Sonnet is reserved for generative /
quality work (briefings, think-deeper, concepts/quizzes, provision birth, campaign
posts, extraction).

## Selection & balancing (`SelectBalancedBatchAsync`)

**Why it exists.** The original selection was plain FIFO —
`WHERE Status=Ingested ORDER BY IngestedAt ASC LIMIT BatchSize` — under the
`MaxItemsPerDay` cap, with no balancing. Because ingestion volume vastly exceeds the
generation cap, two failure modes resulted (both confirmed in prod 2026-06):

- **Local content starved.** The national feed is ingested first each tick, so
  national items always have an earlier `IngestedAt`. The 10/day cap was consumed
  entirely by a perpetually-growing national backlog (24 days deep), and
  locality-tagged items (167 WA ingested, **0 generated**) never surfaced.
- **High-volume source dominated.** BBC's feed churns far faster than NPR's
  (1600 vs 588 ingested → 195 vs 96 briefings, ~2:1), because FIFO/volume selection
  is proportional to raw volume with no per-source quota.

**The fix.** Selection now buckets pending items by `(Locality, Source)` and
round-robins one item from each bucket:

1. **Local guaranteed** — every locality gets a turn each pass.
2. **Source balanced** — within national, BBC and NPR alternate instead of BBC
   monopolizing the budget.
3. **Freshness** — items are taken newest-first by `PublishedAt`, and anything
   older than `MaxStoryAgeDays` is excluded so a stale backlog can't keep crowding
   out fresh and local stories. Items past the age window are left `Ingested`
   (not deleted) and simply never selected.

National source-buckets are ordered first (most readers are national-only), then each
locality — but the round-robin still gives every bucket a turn. Per-bucket
over/under-representation evens out across the day's batches as buckets deplete.

This is go-forward only; existing briefings are untouched.

## Source moniker

Each feed card shows a small per-source badge (`SourceBadge.tsx`: colored dot + short
label, e.g. NPR / WA Standard / the real outlet behind a Google News item) so readers
can tell outlets apart. Backed by `BriefingSummaryDto.SourcePublisher` =
`NewsItem.Publisher ?? NewsItem.Source`, resolved in `BriefingsController.List` via a
single bounded lookup against `NewsItems`. The cover story shows the publisher inline.

## Monitoring after source changes

The Google News **Top** channel carries more non-civic content (sports,
entertainment) than the direct feeds, so the Haiku relevance gate's skip rate
rises — skipped items burn generation batch slots for that tick (not the daily
cap). Watch the `"Skipping non-civic NewsItem"` log rate; if the Top channel
skips too often, the levers are config-only: `"Enabled": false` on it, or lean on
the pre-filtered Politics/Nation/Geo channels.

## Future: raising the cap

`MaxItemsPerDay = 10` is deliberately low (readers won't consume more). Raising it is
viable, but the cap is what throttles how many gate-passing items reach the expensive
Sonnet stage, so a much higher cap means a roughly linear Sonnet bill. The
cheap-gate-first pattern is already in place; the lever to scale without that linear
cost is to make the **Haiku stage do more triage** so Sonnet only fires on the best
stories. Two candidate extensions (not yet built):

1. **Haiku importance ranker.** Extend the step-0 gate to also return a cheap
   newsworthiness/importance score, then generate Sonnet briefings only for the
   top-N of the eligible pool per day. This decouples "how many we ingest + screen"
   (cheap, can be large) from "how many we fully write" (Sonnet, budget-bounded), and
   composes with the bucket round-robin above (rank *within* each bucket).
2. **Demote a Sonnet step.** Try `ThinkDeeper` on Haiku; keep briefings on Sonnet.
   Worth an A/B on quality before committing.

Recommended starting point if/when we raise the cap: option 1 layered onto the
existing bucket round-robin.
