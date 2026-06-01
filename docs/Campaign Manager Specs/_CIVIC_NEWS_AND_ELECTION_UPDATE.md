# Campaign Manager — Election-tie + News-Response Update (authoritative)

Supersedes the duration/action parts of `_CIVIC_CLARIFICATION.md`. Decisions confirmed 2026-06-01.

## 1. Tie campaign duration to the live election (remove custom duration)

- Campaigns no longer take a custom length. On create, the campaign **snaps to the next upcoming
  national `Election`** — the same one the home-page countdown shows via `/api/elections/next?scope=National`
  (currently **2026-11-03**). NOTE: this is the `Election` table, not `ElectionCycle` (which is 2028).
- **Turns are daily. 2 actions per day.** `TotalDays = max(1, (electionDate - startDate).Days)`,
  capped by `CivicCampaignOptions.MaxCampaignDays` as a safety bound (does not change the end date
  shown; just bounds the playable run). `CurrentDay` is 1-based. Advancing a day refills actions to
  `ActionsPerDay`, drifts opponents (daily-scaled), renormalizes standings; reaching the election date
  finalizes the campaign (winner = highest support share).
- Model: `CivicCampaign` drops `TotalWeeks`/`CurrentWeek`; adds `ElectionId`, `ElectionName`,
  `ElectionDate`, `CurrentDay`, `TotalDays`. `CivicCampaignWeek.WeekNumber` → `DayNumber`;
  `CivicCampaignAction.WeekNumber` → `DayNumber`.
- Frontend: remove the weeks slider from CampaignCreate. Dashboard shows the election name + a
  days-until-election countdown and "Day X" instead of "Week X / N".

## 2. News-response as the primary mechanic

Real incoming news (RSS → `NewsItem` → synthesized `Briefing` by the existing pipeline) drives play.
Seeded briefings (4) make this work offline; live ingestion adds more over time.

- **Per-candidate response options** are generated **lazily and cached**: the first time a campaign's
  candidate is offered a briefing, generate **2-3 distinct response options** for that candidate and
  cache them in a new `CandidateNewsResponse` row (unique on candidate+briefing; reused across
  views/players). LLM-guarded (`ILlmClient.GenerateStructuredAsync<GeneratedNewsResponsesDto>`) with a
  deterministic templated fallback derived from the candidate's planks + the briefing's values.
- An option = `{ id, label, angle, tone, body }` — `label`/`angle` describe the strategic stance
  (e.g. "Go on offense", "Stay disciplined", "Pivot"); `body` is a ready ≤160-char post.
- **Campaign detail** surfaces up to `NewsItemsToOffer` (6; keep 5-7) of the most recent briefings the
  campaign hasn't responded to, each with its 2-3 options. The manager **picks which item + which option.**
- Responding (`actionType=RespondToNews`, `briefingSlug`, `optionId`): spends one action, computes a
  support delta (fit of the briefing's issues × salience × momentum × the option's modifier), generates
  a real `CampaignPost` from the option body (LLM or templated), records the action with
  `RespondedBriefingSlug`, and removes that briefing from the campaign's offered list.
- New enum value `CivicCampaignActionType.RespondToNews`. The generic `PublishPost`/`RapidResponse`
  options are no longer offered (enum values retained for compatibility). `TargetIssue` and
  `ShoreUpAxis` remain as **secondary** "budgeting tools."

## API

- `POST /campaign-manager/campaigns` — body `{ candidateSlug, difficulty }` (no totalWeeks).
- `GET  /campaign-manager/campaigns/{id}` — detail now includes `electionName`, `electionDate`,
  `daysRemaining`, `currentDay`, `totalDays`, and `newsItems: [{ briefingSlug, headline, summary,
  valuesInConflict, options: [{ id, label, angle, tone }] }]`.
- `POST /campaign-manager/campaigns/{id}/actions` — `{ actionType, briefingSlug?, optionId?, target?, tone? }`.
- `POST /campaign-manager/campaigns/{id}/advance` — advances one day.
- `GET  /campaign-manager/campaigns/{id}/results` — unchanged shape (support trend now per recorded day).

## Tests

- Unit: news-response support math, daily opponent drift scaling, TotalDays computation, finalize.
- API: create snaps to the national election; news items + options surface (templated, no network);
  responding spends an action / publishes a post / removes the item; advance increments the day and
  refills actions; finalize at election day decides a winner; ownership 404.

## Out of scope (this update)
- Eager option generation for all candidates during synthesis (we do lazy+cached instead).
- Calendar UI of the full day-by-day schedule; we show a countdown + current day.
