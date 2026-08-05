# Topic Rooms — implementation status

**Branch:** `feature/civic-topic-rooms` · **PR:** [#97](https://github.com/zeus572/arena-app/pull/97) (draft)
**Last updated:** 2026-08-05

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

Six migrations: `AddRoomsGraph`, `AddRooms`, `AddRoomContentObjects`, `AddRoomEditorial`,
`AddRoomInteractionsAndPredictions`, `AddMoneyTrail`.

**Test counts at handoff:** `Civic.UnitTests` 358/358 · `Civic.ApiTests` 363 passed ·
`npm run build` clean · 68/68 vitest · 8/8 `e2e/rooms.spec.ts`.

---

## What is NOT done

### F2 — Story Room pages (designs 1o, 1p)
`StoryRoom` entities, API and DTOs all exist and the pilot seeds one
(`/rooms/how-an-appropriation-becomes-spending`). There is **no story-specific page**, so
that URL currently renders through the theme-room component and looks wrong. This is the
most visible gap.

Also unbuilt here: upgrading the existing `/bills` experience into a Story Room, which
PRD 08 §12 calls the recommended first product decision.

### F3 — Interaction UI (1o Explore, 1u, 1v, 1p Vote, 1x Sprint)
Backend is complete and tested — scoring, redaction, Brier, calibration bands, XP. No React
components. Every drag interaction needs a select-then-place keyboard fallback; that is an
accessibility publish gate, not a nicety.

### F4 — Money Trail UI (1s, 1t, 1w)
`MoneyMath` and the entities are done and tested. No ladder rendering, no Budget Allocator,
no Guess-the-Stage. **No read endpoints yet either** — `/rooms/{slug}/money` is specified in
the plan but not implemented.

### F5 — Board view + mobile (1b, 1aa, 1bb)
`?view=board` currently only widens the layout shell; the Situation Board itself is not
built. Mobile at 390px is untested beyond the existing responsive classes.

### R7 — LLM drafting + admin review UI (1y, 1z)
`RoomCandidateService`, `RoomDraftService`, `ClaimExtractionService` do not exist. The four
drafting columns are already on `Room` (`DraftModelId`, `DraftPromptVersion`,
`DraftAttemptCount`, `LastError`, `DraftedAt`) **so R7 needs no migration**.
`AdminRoomsController` exists with gates, propagation, flags, metrics and integrity — but
there is no `/admin/rooms` page.

**Critical constraint for R7:** Civic stores headline + RSS summary only, no article body.
Claim extraction must run over **Bills and Briefings**, which carry real prose — never over
`NewsItem`. Do not add an article fetcher without deciding the rights question first.

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

## Suggested next step

**F2 (Story Room pages).** It closes the most visible gap — a seeded story room that
currently renders through the wrong component — and PRD 08 §12 argues the bill-as-Story-Room
upgrade is what forces the shared model to prove itself. Everything it needs on the backend
already exists.
