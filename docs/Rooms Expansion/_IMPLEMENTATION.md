# Topic Rooms — implementation status

**Branch:** `feature/civic-topic-rooms` · **PR:** [#97](https://github.com/zeus572/arena-app/pull/97) (draft)
**Last updated:** 2026-08-07

Read this first if you are picking the work up cold. The PRDs in this directory are the
requirements of record; `design_handoff_topic_rooms/SCREENS.md` is the per-screen spec.

---

## Scope decided (do not re-litigate without a reason)

| Decision | Choice |
|---|---|
| Depth | MVP spine (PRD 08 phases 0–3) **plus a read-only Money Trail** |
| Conversation Map (PRD 03) | **Not built.** PRD 08 Gate 3 forbids it without a compliant connector, deletion reconciliation, a PII filter and a T&S owner. None exist. |
| Content | Hand-seeded pilot room **and** an LLM-drafts → admin-approves pipeline behind it |
| Density | Separate `/rooms/:slug?view=board` destination, not a three-state dial per module |
| Visual | Zero border-radius scoped to rooms (`.rooms-square`); rest of Civersify unchanged |
| Pilot theme | **Federal appropriations**, not the handoff's U.S.–Iran sample copy |

On the pilot theme: the handoff says of its own copy that it "is not reporting and must not
ship as fact", and PRD 07 requires senior review for casualty and attribution claims. There
is no editorial team. Appropriations exercises the same ten sections at a fraction of the
risk and gives the Money Trail real content.

---

## What is done

| Phase | Commit | Delivers |
|---|---|---|
| R0 | `a2f009e` | `ObjectLink` graph, `Claim`, `SourceRef`, `ClaimStatusHistory`, `LinkSchema`, `ObjectResolver`, `ClaimsController` |
| R1 | `b023166` | `Room` TPH + `ThemeRoom`/`StoryRoom`, `RoomRevision`, `ChangeLogEntry`, `UserRoomState`, delta, `RoomsController` |
| R2 | `f428d21` | `Actor`, `ActorRoomRole`, `TimelineEvent`, `Development`, `Concept` extension, pilot seed, contested terms |
| F1 | `db28932` | Room front door — designs 1a/1d/1g/1h/1i/1l/1m; `EvidenceMark`; tokens; `.rooms-square` |
| R3 | `7c6ab26` | `ReviewFlag`, `PublishGateResult`, 9 publish gates, `CorrectionPropagationService`, `RoomVisibility`, `AdminRoomsController` |
| R4/R5 | `77646d9` | `Interaction` + plays, `Prediction` + `UserPrediction`, Brier scoring, redaction, two controllers |
| R6 | `dc44d97` | `MoneyItem` + `MoneyStageEntry`, `MoneyMath` |
| Seed | `d69887b` | Pilot rebuilt from 300 live briefings — 20 sources, 12 concepts, 14 actors, 25 claims, 12 timeline events, 12 developments |
| — | `acb12a3` | Decision-scoped actor tiering (the People & Power selector re-sorts) |
| — | `741818f` | `/claims/:slug` page, `GET /rooms/{slug}/sources`, `ObjectResolver` un-parked |
| F2 | `f098893` | Story Room pages; `/rooms/:slug` discriminates on kind |
| F4 | `21fa4b4` | Money Trail — seed, `GET /rooms/{slug}/money`, five-rung ladder |
| F5 | `ad7c0e4` | Situation Board, claims ledger + `GET /rooms/{slug}/claims`, 44px targets |
| F3 | `9204ea5` | Four seeded interactions + their UI |
| R7 | `80ff66e` | Candidate pass, draft pass, claim extraction, `GET /admin/rooms/pipeline` |

Six migrations: `AddRoomsGraph`, `AddRooms`, `AddRoomContentObjects`, `AddRoomEditorial`,
`AddRoomInteractionsAndPredictions`, `AddMoneyTrail`.

**Every phase of the plan is now built.** `Civic.UnitTests` 380/380 · `Civic.ApiTests` 383
passed (1 pre-existing `BriefingsControllerTests` failure) · `npm run build` clean ·
68/68 vitest · 21/21 `e2e/rooms.spec.ts`.

## The pilot content

The room is built from **300 briefings pulled from the live civic API**, covering
2026-06-04 to 2026-08-05. 177 fall inside the 23-day development window; 13 are U.S.
federal appropriations stories; 12 are logged as developments. Publisher, URL and
publication time come off those records, so the source list cites AP, NPR, NYT, Politico
and the Washington Post rather than our own explainers — with `fullTextAvailable: false`
on all of them, which is true and is why nothing is extracted verbatim.

Re-pull with the same method if the corpus needs refreshing; the window and the
`ArticlesConsideredCount` denominator must be recomputed together or the disclosure
under the Latest section becomes false.

---

## Running the drafting pipeline

```bash
# Candidate pass only — deterministic, no LLM, free. On by default.
RoomDrafting__CandidatesEnabled=true dotnet run --project backend-civic

# Add the draft pass. This SPENDS MONEY on every tick.
RoomDrafting__Enabled=true RoomDrafting__DraftBatchSize=3 dotnet run --project backend-civic
```

`RoomDrafting:Enabled` defaults to **false** everywhere. Its failure mode is not a crash, it
is a quiet retry loop that bills for nothing — which has happened on this codebase before —
so it is opt-in per environment and `MaxDraftAttempts` bounds it.

Neither pass can publish. Their terminal state is `Draft`, and review is deliberately not a
step the machinery waits on: `GET /api/admin/rooms/pipeline` is a read-only report, not a
queue. What keeps model-written text away from readers is that `Draft` is not a published
status. Turning one into a published room is still a human action and still runs the gates.

On a dev box the candidate pass will usually find nothing, because the local briefing corpus
is months older than any room's `DevelopmentWindowDays`. That is correct behaviour, not a
failure. The `RoomDraftServiceTests` suite exercises the whole chain against real Postgres.

**The corpus constraint still holds and is now enforced in code.** Civic stores headline +
RSS summary only, no article body. Extraction runs over **Bills and Briefings**, which carry
real prose — never `NewsItem`. `RoomCandidateService` will not create a candidate from one
and `RoomDraftService` fails a sourceless candidate rather than drafting from nothing, so
the rule lives where the corpus is chosen rather than in the prompt. Do not add an article
fetcher without deciding the rights question first.

**Verbatim passages are verified, not trusted.** A model asked for an exact supporting span
will sometimes return a tidied one, and a paraphrase presented as a quotation looks exactly
like the good case. `ClaimExtractionService.PassageAppearsIn` checks every passage against
the source; an unverified claim loses its evidence edge, is demoted and says so. Nothing the
pipeline drafts is ever `Confirmed` — that status means a primary document settles it, and
the pipeline holds a briefing.

---

## Decisions worth not reversing by accident

- **`ObjectLink` is one polymorphic edge table on purpose.** Correction fan-out is one
  indexed scan on `(ToType, ToId)`. Typed join tables would need a UNION edited on every new
  object type, and a missing arm produces no compiler error — only a correction that
  silently fails to propagate. `LinkSchema` + `ObjectResolver` + `/integrity` buy back the
  safety. `ObjectResolverTests` fails if a new `ObjectType` is neither resolvable nor
  explicitly parked.
- **Room copy must never cache a claim's status.** The mark renders from the `Claim` row.
  That is the whole reason the "automatic" half of fan-out is automatic, and there is a test
  that changes only the claim and asserts the front door follows.
- **`MeaningfulChange.Classify` returns an enum, not a bool.** A default arm would classify
  new `ChangeType`s as "do not notify", and a suppressed notification is invisible in testing.
- **`RoomsController` has no class-level `[AllowAnonymous]`.** One there short-circuits
  authorization and silently opens the writes. Same for any new controller mixing anonymous
  reads with gated writes — follow `PetitionsController`.
- **The six-hour rule keys on when content became *hideable*** (`flag.CreatedAt + grace`),
  not on flag creation. Keying it on creation makes the mid-session exemption dead code,
  because a 30-minute session cannot predate a six-hour-old flag. There is a regression test.
- **`MoneyMath.TotalAcrossStages` always throws.** It exists only so the mistake has a name.
- **No prediction leaderboard**, and a test asserts three plausible paths 404. XP is paid for
  forecasting, never for accuracy.
- **`InteractionRedaction` is allow-list, not deny-list**, and fails closed on an unknown
  kind. The API test asserts against the raw response body, not the DTO.

---

## Running it locally

```bash
docker start arena-postgres

# Civic API. The pilot room is OFF by default in both flags — this turns it on for dev.
Rooms__SeedPilot=true Rooms__PilotStatus=Published \
  dotnet run --project backend-civic --urls "http://localhost:5050"

# Arena API — only needed for sign-in / Follow.
dotnet run --project backend --urls "http://localhost:5000"

cd frontend-civic && npm run dev      # :5175
```

Then `http://localhost:5175/rooms`.

`Rooms:SeedPilot` defaults to **false** and `Rooms:PilotStatus` to **Draft**. Production must
never set either. The seeded room is a structural fixture and test corpus.

### Tests

```bash
dotnet test backend-civic-tests/Civic.UnitTests
dotnet test backend-civic-tests/Civic.ApiTests   # individually — never `dotnet test arena.sln`
cd frontend-civic && npm run build && npm test
npx playwright test e2e/rooms.spec.ts            # needs both backends up
```

### Known-failing, unrelated to this work

- `BriefingsControllerTests.List_ReturnsSeededBriefings` — local seed drift; passes in CI.
- `Arena.UnitTests.Social.Gate6_ResilienceTests.Assertion09_time_box_stops_cleanly` — the
  documented flaky timing test. Failed one CI run here, passed the next. Do not chase.

---

## Design-vs-reality drift found during the build

Recorded because it will bite the next person too.

1. **`frontend-civic` cannot be consumed as a dependency** — it is `private: true` with no
   exports, despite the handoff README saying to consume it as one. Build in-repo.
2. **The bundled DS is stale** (built 2026-06-18, `_ds_needs_recompile` marker present).
   `BottomTabs`, `MobileMenu`, `CoverStory`, `Term`, `CampaignPostCard`, `PlayReadToggle`
   have all changed since. Re-sync per `.design-sync/NOTES.md`.
3. **The handoff `_ds/` folder ships none of the `prompt.md` / `tokens/` / `fonts/` files its
   own README tells you to read.** Every `.d.ts` is a `[key: string]: unknown` stub. Read the
   real component source.
4. **`--bg-sunken`, `--bg-inset`, `--highlight-changed` did not exist** — added in F1.
5. **`--line` was used in 8 files and declared nowhere** (invisible borders in `Flyout`,
   `CoalitionProvisionDetail`, `About`). Also fixed in F1.
6. **Zero-radius conflicts with the shipped components** (`Button`, `ValueChip`,
   `DisclaimerBadge` are all `rounded-full`). Resolved by scoping to `.rooms-square`.
7. **`DesignProvider` is not app code** — it is `.ds-provider.tsx`, outside the tsconfig
   include. Any route outside `MagazineLayout` must apply `theme-magazine` itself.
8. **The README's `BudgetFactCard` "fetches on mount" claim is false.** Only `CountdownTimer`
   self-fetches.
9. **`Term`, `CaveatGrid`, `DisclaimerBadge` are hardcoded single-purpose**, not generic.
   `DisclaimerBadge` in particular must not be reused for evidence marks.
10. **Claim-status enum conflict:** PRD 03 §6.4 lists 7, PRD 07 §6.1 lists 8. **PRD 07 wins.**
11. **The DS omits ~36 real components** including all of `shorts/`, `daily/` and
    `featureCards/`. Reuse `ExpandableText`, `DailyCardShell`, `ShareGrid`, `WhenVisible`,
    and `newStories` + `useVisibilityPolling` (already does the "new content" ribbon) before
    rebuilding anything.

---

### Still unbuilt

- **`/bills` as a Story Room.** PRD 08 §12 calls this the recommended first product
  decision; the Story Room page now exists to receive it, but the bill experience has not
  been moved onto it.
- **Calibrated Prediction (1v)** has a backend, a payload kind and a scoring rule, but no
  seeded `Prediction` row and no component. It is the one MVP interaction kind still dark.
- **Budget Allocator (1w) and Guess-the-Funding-Stage.** Both are `InteractionKind` members
  that are named but not built.
- **Civic Sprint (1x)** — no completion surface. Note PRD 06 forbids giving it a score.
- **Diff mode (1e).** `RoomRevision.SnapshotJson` is written for meaningful revisions, so
  this stays a pure frontend decision with no schema change whenever it is wanted.
- **The delta ribbon has never rendered against real data.** The pilot is at r.1 and has no
  changelog, so `/delta` correctly returns nothing. Exercising it needs a second revision
  committed through `RoomRevisionService`.

- **`/admin/rooms` as a page.** The endpoints exist and are read-only by design; there is
  no React surface, and per the scope decision above one is not required.

## Suggested next step

**Commit a second revision to the pilot.** The delta ribbon, the changelog and
`lastMeaningfulUpdateAt` are all built and none has ever rendered against real data, because
the room is still at r.1 and `/delta` correctly returns nothing. A single correction pushed
through `RoomRevisionService` — the `$1T+` → `$1.15T` pair is a real one already in the seed
as an `Outdated` claim — would light up three surfaces at once and exercise
`CorrectionPropagationService` end to end.
