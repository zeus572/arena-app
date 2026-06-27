# SocialPublisher — Build Log

Unattended phased build per `SocialPublisher_Spec.md`. Launch target: **Bluesky only**.
Toolchain: .NET SDK 8.0.128 (installed via apt; `global.json` pins 8.0.101 `rollForward: latestFeature`).

---

## Phase 1 — Data model + dedup ✅

- Added `SocialContentType`, `SocialPostStatus` enums + `SocialPost` entity
  (`backend/Models/Social/SocialPost.cs`).
- Registered `DbSet<SocialPost>` and the dedup index in `ArenaDbContext`:
  - Unique filtered index `IX_SocialPosts_Dedup` on `(ContentType, ContentId, Platform)`
    `WHERE "ContentId" IS NOT NULL` — FeaturePost seeds (null ContentId) are exempt.
  - The filter predicate quotes the column with `"` so it is valid on **both** PostgreSQL
    (prod) and SQLite (Gate 1 test provider).
- EF migration: **`20260627000638_AddSocialPublisher`**.

**Gate 1 (3/3 passed)** — run against real EF/SQLite (InMemory provider does NOT enforce
unique indexes, so the test project gained `Microsoft.EntityFrameworkCore.Sqlite`):
1. duplicate `(ContentType, ContentId, Platform)` → `DbUpdateException`.
2. same content, different platform → succeeds.
3. two `ContentId == null` FeaturePost rows, same platform → coexist (exempt).

---

## Phase 2 — Selection logic (LLM-free) ✅

### §2.1 discovery result — PATH TAKEN: **deterministic FALLBACK**

Inspected the codebase (read-only) for an existing coalition breadth signal and per-agent
values-axis positions:

| Looked for | Found? | Notes |
|---|---|---|
| Coalition entity | ❌ | No coalition table. Closest analog: a completed `common_ground`-format debate; its members are the two agents {Proponent, Opponent}. A `coalitionId` is therefore a `Debate.Id`. |
| Breadth / spread signal (`BreadthScore`, `CoalitionBreadth`, `ValuesSpread`…) | ❌ | `DebateAggregate.DiversityScore` exists but is hardcoded (RankingService MVP), not a values spread. |
| Per-agent values-axis / Values Profile position | ❌ | No values axis on `Agent`. Only per-agent numeric vector available is the five personality traits (Aggressiveness, Eloquence, FactReliance, Empathy, Wit), 0..10. `User.PoliticalLeaning` is unstructured free text and not on agents. |

→ No real signal exists, so the **documented deterministic fallback** (§2.1 step 3) was implemented in
`CoalitionSignalProvider`:
- `ComputeBreadth(memberPositions)` = max pairwise Euclidean distance over the axes, normalized by
  the unit-hypercube diameter `sqrt(numAxes)` → 0..1.
- `IsBipartisanInternal` = members straddle at least one axis midpoint (0.5).
- `GetValuesPosition(Agent)` is the **single swap point**: it currently maps the five personality
  traits (normalized /10) as the values vector. **FLAGGED for Sam:** these are rhetorical traits, a
  placeholder proxy — when the real Values Profile geometry lands, rebind only this method; the rest
  is unchanged. Candidacy = `breadth ≥ CoalitionBreadthMin` **and** bipartisan.

### Bindings
- `IRankingScoreProvider` → `RankingScoreProvider`, a pure read of the latest `DebateAggregate` row
  for the debate (no model call). Coalition/Debate ContentId = Debate.Id; Briefing/Feature → null.
- `IHighlightSelector` → `HighlightSelector`. PostScore = `WQuality·q + WEngagement·e + WNovelty·n +
  WRecency·r − penalties`, each component normalized by `RankingComponentMax` (10). FeaturePost with
  no ranking score → `FeaturePostBaseScore`.
- DebateHighlight "top turn engagement" is bound to the **debate-level** normalized ranking
  Engagement (no per-turn engagement signal exists). **FLAGGED.**
- BriefingAnnounce source is an intentionally empty gatherer — no backend Briefing entity exists
  (frontend-civic prototype only). **FLAGGED (§3 stub).**
- FeaturePost source = `IFeaturePostProvider` (default `EmptyFeaturePostProvider`); seeds are
  admin/config authored, ContentId stays null (§5).

**Gate 2 (9/9 passed):**
- Breadth fallback golden values (5 tests): delta-0.6 → 0.6; identical → 0; opposite corners → 1;
  bipartisan straddle true; same-side false.
- Selector golden ordering (1 test): exact `[Coalition D1 (0.70, review), Debate D4 (0.80, no
  review), Feature (0.40, review)]`. D2 (engagement 0.3 < 0.5) excluded; D3 (already posted)
  excluded via dedup.
- Debate-below-engagement excluded; already-posted excluded (2 tests).
- No-LLM structural guard: `HighlightSelector` has no LLM-typed constructor dependency (1 test).

---

## Phase 3 — Platform adapter (Bluesky) ✅

- `BlueskyClient : IPlatformClient` (AT Protocol). The ONLY adapter. X registration site carries the
  `// Deferred: XClient — see §4.2` marker (Program.cs); no stub class, no config, no credentials.
- Auth: app password → `createSession` JWT, `refreshSession` on 401, retry once.
- Post: `createRecord` (app.bsky.feed.post); image via `uploadBlob` embed with alt text.
- Length: grapheme-aware via `StringInfo` text elements (NOT `.Length`), limit `BlueskyMaxGraphemes`.
- Links: deterministic UTF-8 byte-range facets (`BlueskyText.ComputeFacets`).
- Expected failures (length/auth/rate-limit/5xx/network) return a `PublishResult` with an ErrorCode;
  never throw across the boundary (§4.3). `GetRateLimitStatus()` parses `ratelimit-*` headers.
- Credentials come from the `Bluesky` config section / secret store — never hardcoded, never logged.

**Gate 3 (8/8 passed), no live network:**
- ZWJ-emoji counts as 1 grapheme (grapheme-aware proof).
- Over-limit text → `LENGTH_EXCEEDED` before any network call.
- 300-grapheme emoji string (huge UTF-16 length) passes the length gate.
- Facet golden: `"héllo https://x.com end"` → byteStart 7 / byteEnd 20 (byte-aware, not char index).
- createSession → createRecord happy path returns the post uri.
- 401 → refreshSession → retry → success (refresh hit once, createRecord twice).
- Rejected credentials → `AUTH_INVALID`, no createRecord.
- 429 → `RATE_LIMITED` (retryable).

> ⚠️ Live publish is UNVERIFIED until the Phase 7 manual smoke test with the real app password.

---

## Phase 4 — Card renderer ✅

- `HtmlCardRenderer : ICardRenderer` — deterministic `{{token}}` substitution over one embedded HTML
  template per content type (feature/debate/coalition/briefing), HTML-encoding text values, then
  throwing if any `{{ }}` remains. Rasterization is delegated to `IHtmlRasterizer`.
- Default `SolidColorPngRasterizer` emits a valid 24-bit RGB PNG of exactly the requested dimensions
  with **no browser and no network** (hand-rolled PNG encoder: signature + IHDR + zlib IDAT via
  `ZLibStream` + IEND, CRC-32 per chunk). This keeps Gate 4 and the run path offline (§4.4).
  **DESIGN NOTE / FLAGGED for Sam:** production card *visual fidelity* (actual HTML layout) is a
  drop-in — implement `IHtmlRasterizer` with a headless-Chrome (Playwright/Puppeteer) backend and
  register it instead of the solid-colour default. The substituted HTML is identical either way.

**Gate 4 (7/7 passed):**
- Golden HTML snapshot for the feature template (exact match) with no leftover tokens.
- All four templates leave no `{{ }}` tokens after substitution.
- `RenderAsync` returns a non-empty PNG whose IHDR width/height equal the requested 1200×675 and
  1080×1080.

---

## Phase 5 — Job integration + review queue ✅

- `SocialPublisher.RunOnceAsync` = (1) select + persist new candidates as SocialPost rows
  (Pending or AwaitingReview), then (2) publish the due queue.
- Integrated into `BotHeartbeatService` via `SocialHeartbeatHook` at `PublishEveryNTicks`
  (downsampled). The publisher OWNS NO TIMER — it rides the heartbeat. The hook swallows-and-logs
  any escaped error; the heartbeat wraps it again (belt-and-braces, §4.4).
- Review queue: `SocialReviewService` + `SocialController` endpoints
  `GET /api/social/review`, `POST /api/social/review/{id}/approve`, `.../reject`.
- Per-candidate try/catch, per-tick cap (`MaxPostsPerTick`), proactive daily cap + rate-limit checks.
- DI wired in Program.cs (singletons for cross-tick state: breaker registry, platform client/session,
  clock; scoped for the DbContext-touching selection path). X registration site carries the
  `// Deferred: XClient — see §4.2` marker.
- NOTE: DateTimeOffset retry-gate/ordering and daily-count comparisons are evaluated in memory so the
  query is provider-agnostic (Npgsql translates them; SQLite, the test provider, does not).

**Gate 5 (4/4 passed):**
- Auto item Published (client called once) + review item AwaitingReview (no client call).
- Daily cap of 1 publishes one, defers the other Pending (not Failed).
- Forced adapter failure → Failed with error code; the other candidate still Published (one call each).
- Approve transitions AwaitingReview → Approved; next tick publishes it.

---

## Phase 6 — Resilience hardening ✅ (REQUIRED gate)

Implemented in the publisher/hook/breaker built in Phase 5:
- Per-platform `CircuitBreaker` (Closed/Open/HalfOpen) + singleton `CircuitBreakerRegistry`
  (independent breakers — one platform down never affects another).
- Retry-with-backoff (exponential + jitter, capped at `MaxBackoffMinutes`); retryable vs terminal
  classification via `SocialErrorCodes`.
- Proactive daily-cap + rate-limit deferral (no request fired when known-exhausted).
- Idempotency guard (rows carrying a `PlatformPostId` are never re-published).
- Heartbeat-level swallow (`SocialHeartbeatHook.RunSafelyAsync`) + in-publisher wall-clock time-box
  (`PublisherTickBudgetMs`).
- `GET /api/social/health` (`SocialHealthService`): per-platform breaker state, last error,
  today's published/failed counts.

**Gate 6 (10/10 passed)** — two fake clients (`bluesky` + synthetic `fake-2`):
1. Throwing publisher swallowed; core work continues; logged not propagated.
2. Breaker Opens after `CircuitFailureThreshold` (3) consecutive fails; remaining `fake-2` candidates
   left Pending, no further calls during cooldown (0 Failed, 5 Pending, 3 calls).
3. With `fake-2` Open, `bluesky` still publishes in the same tick (independent breakers).
4. 429 → Pending + RetryCount++ + future NextRetryAt; LENGTH_EXCEEDED → Failed immediately (no retry).
5. NextRetryAt in the future is skipped; once time passes it is retried (clock advanced).
6. After `MaxRetries` the post is Failed and never re-selected.
7. Exhausted rate-limit → no publish call, candidate deferred Pending.
8. Row already carrying a PlatformPostId is not re-published (0 calls).
9. Work exceeding `PublisherTickBudgetMs` (1ms vs 60ms client) stops cleanly; tick completes; rest deferred.
10. Health endpoint reflects `fake-2` Open + last error and `bluesky` Closed + publishedToday=1.

---

## Phase 7 — Manual smoke test (human-in-loop) — NOT RUN (requires live credentials)

Per the kickoff prompt, the live smoke test was **not attempted** (it needs the real Bluesky app
password). Manual checklist for Sam is in this repo at the end of this log and reproduced in the
final report.

---

## Test totals

`dotnet test backend-tests/Arena.UnitTests` → **56 passed / 0 failed** (41 new SocialPublisher gate
tests across Gates 1–6, plus the 15 pre-existing tests still green). Full solution builds clean.

## §3 interface bindings that were stubbed / flagged for review

1. **BriefingAnnounce source** — no backend Briefing entity exists (frontend-civic prototype only).
   The selector's briefing gatherer returns empty. Bind a Briefing read model when one lands.
2. **`ICoalitionSignalProvider.GetValuesPosition`** — fallback uses the five Agent personality traits
   as a stand-in values vector (no real Values Profile axis exists). Rebind this one method to the
   real geometry signal when available; `ComputeBreadth`/bipartisan logic stays.
3. **DebateHighlight engagement** — bound to the debate-level normalized ranking Engagement; there is
   no per-turn engagement signal.
4. **Card rasterization** — default `SolidColorPngRasterizer` (offline, deterministic). For production
   visual fidelity, implement `IHtmlRasterizer` with a headless-Chrome backend and register it.
5. **FeaturePost source** — `IFeaturePostProvider` defaults to empty; seed admin posts via a real
   provider when desired.

---

## Phase 7 — Manual Bluesky smoke-test checklist

Prereq: real Bluesky account created; app password in the secret store (NOT committed).

```bash
# 1. Configure credentials (user-secrets; never appsettings/committed)
dotnet user-secrets set "Bluesky:Handle" "<your-handle>.bsky.social" --project backend
dotnet user-secrets set "Bluesky:AppPassword" "<app-password>" --project backend
# (optional) dotnet user-secrets set "Bluesky:Service" "https://bsky.social" --project backend
```

1. Seed/queue one `FeaturePost` (via an `IFeaturePostProvider` seed or by inserting a `SocialPost`
   row Status=Pending, Platform="bluesky", ContentId=null).
2. Run the backend in a staging environment; let the heartbeat tick reach `PublishEveryNTicks`
   (or call `ISocialPublisher.RunOnceAsync` once).
3. Confirm the post is **live on Bluesky** and the `SocialPost` row has `Status=Published` and a
   non-null `PlatformPostId` (the at:// uri).
4. Hit `GET /api/social/health` → `bluesky` breaker `Closed`, `PublishedToday >= 1`.
5. **Isolation test:** deliberately invalidate the Bluesky credential (set a wrong app password) and
   restart. Confirm:
   - the publisher logs the auth failure ("Bluesky credentials invalid — skipping Bluesky…"),
   - the `bluesky` breaker goes `Open` (`GET /api/social/health`),
   - queued Bluesky posts stay `Pending` (NOT `Failed`),
   - **the core platform — heartbeat, debates, coalitions, briefings — runs completely normally.**
6. Restore the correct credential; confirm the breaker recovers (HalfOpen probe → Closed) and the
   pending post publishes on a later tick.

> This is the only step requiring live credentials and is explicitly outside the automated gates.
