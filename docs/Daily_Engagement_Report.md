# Daily Engagement Report

One operator email per day: **what happened yesterday** across both apps, then **where things
stand**. Sent by the Arena backend, because that's where the ACS mail path lives.

## What's in the email

1. **Opener** — a headline + 2–4 sentences. Written by Claude when available, from the exact
   counts below (it is told never to invent a number). Falls back to a computed sentence
   whenever the model is off, keyless, rate-limited, or returns junk — the email always goes.
2. **Who showed up** — signups (and how many verified), active people vs. yesterday, anonymous
   arrivals.
3. **Activity per app** — for each tracked action: today, distinct people behind it, yesterday,
   the 7-day average, and the all-time total.
4. **Went quiet today** — anything that did zero today but normally averages ≥1/day. A digest
   that only lists what happened can't show you what stopped.
5. A flag if Civic's numbers couldn't be fetched, so a missing app is never silently absent.

### The day it covers

The most recently **completed UTC day**. A report sent at 14:00 UTC on the 1st describes the
31st, start to finish — it never reports a day still in progress.

## How it runs

```
DailyEngagementReportService (BackgroundService, Arena)
  └─ every 10 min: is it past DailyReport:HourUtc, and has nothing gone out today?
       └─ DailyReportSender (scoped)
            ├─ ArenaDailyStatsService      → Arena's day slice, straight from ArenaDbContext
            ├─ CivicStatsClient            → GET {CivicBaseUrl}/api/admin/daily-stats?date=…
            │                                 (X-Report-Secret; null on any failure)
            ├─ DailyReportComposer         → narrative (ILlmClient) + HTML/text
            ├─ IEmailSender                → ACS in prod, no-op locally
            └─ EmailSendLogs row           → the durable "already sent today" record
```

Polling plus a durable send record, rather than a sleep-until timer, is what makes this survive
how App Service actually behaves: a deploy or recycle at 13:59 doesn't lose the day's report,
because the first tick after boot sees the hour has passed and nothing has been sent. The
trade-off — no report at all if the app is asleep all day — is accepted; this is a digest, not
an alerting path.

**One report per UTC day.** The scheduler only ever asks for yesterday, so "has a report gone
out since midnight?" is the whole rule. A forced send counts as that day's send.

## Where the numbers come from

Both apps' DBs, via the same definitions their own admin surfaces use — no separate metrics
pipeline to drift.

- **Arena** — `ArenaDailyStatsService` reads `ArenaDbContext` directly. Signups are
  non-anonymous `Users` rows; activity covers votes, reactions, predictions, crowd questions,
  topic proposals/votes, and user-started debates. Bot-generated volume (debates created, turns
  generated) is reported separately under *Platform* so it can't be mistaken for people.
- **Civic** — `DailyStatsBuilder` reads the shared `EngagementCatalog`, which is also what the
  admin engagement dashboard uses. Add a feature there and it appears in both.

Two things worth knowing when reading the numbers:

- **Civic has no accounts of its own.** Accounts live in the Arena DB; Civic keys off user-id
  strings. Its "signups" are first `UserProfiles` rows, and its verified count is always 0.
- **Anonymous users are volume, never people.** Arena auto-creates an anonymous user row per
  browser, so those events are counted and shown, but they never appear in "active people".

App Insights (page views, geography, bot-vs-human) is **not** in this report — that's the
`telemetry-report` skill, and a possible v2 section here.

## Configuration

Arena (`DailyReport` section). Secrets go in user-secrets / Azure App settings — **never** a
committed file:

| Key | Default | What it does |
| --- | --- | --- |
| `Enabled` | `false` | Turns on the **scheduled** send. The manual trigger works regardless. |
| `Recipient` | `""` | Where the report goes. Server-side only — no endpoint accepts a recipient. |
| `HourUtc` | `14` | Send at/after this UTC hour. 14 = 7am Pacific. |
| `CivicBaseUrl` | `""` | Civic backend base URL. Blank = Arena-only report. |
| `CivicSecret` | `""` | Must match Civic's `Reporting:Secret`. |
| `Secret` | `""` | Guards Arena's own report endpoints. Blank disables them. |
| `UseLlmNarrative` | `true` | Ask Claude for the opener. Also gated by `Anthropic:Enabled`. |

Civic: `Reporting:Secret` — same value as Arena's `DailyReport:CivicSecret`. Blank disables
Civic's daily-stats endpoint entirely.

### Turning it on in prod

```bash
az webapp config appsettings set -g rg-arena -n arena-api-2af326 --settings \
  DailyReport__Enabled=true \
  DailyReport__Recipient='you@example.com' \
  DailyReport__CivicBaseUrl='https://civic-api-fexzo2.azurewebsites.net' \
  DailyReport__CivicSecret="$SECRET" \
  DailyReport__Secret="$ARENA_SECRET"

az webapp config appsettings set -g rg-arena -n civic-api-fexzo2 --settings \
  Reporting__Secret="$SECRET"
```

Locally, use user-secrets so nothing lands in a tracked file:

```bash
dotnet user-secrets set "DailyReport:Enabled" "true" --project backend
dotnet user-secrets set "DailyReport:Recipient" "you@example.com" --project backend
```

With `Email:Provider` unset locally, the no-op sender just logs the message — the whole path is
exercisable on a dev box without sending real mail.

## Operator endpoints

Both take `X-Report-Secret` and are disabled when their secret is blank. Counts only — no user
ids or emails in any response.

```bash
# Arena's raw day slice — for checking a number that looks wrong
curl -H "X-Report-Secret: $ARENA_SECRET" \
  "https://arena-api-2af326.azurewebsites.net/api/admin/daily-stats?date=2026-07-31"

# Send the report now, without waiting for the scheduled hour
curl -X POST -H "X-Report-Secret: $ARENA_SECRET" \
  "https://arena-api-2af326.azurewebsites.net/api/admin/daily-report/send"

# Civic's slice (what Arena fetches)
curl -H "X-Report-Secret: $SECRET" \
  "https://civic-api-fexzo2.azurewebsites.net/api/admin/daily-stats?date=2026-07-31"
```

`date` defaults to the last completed UTC day on all three.

## Adding a metric

- **Civic**: add an entry to `EngagementCatalog.For(...)`. It lands in both the daily report and
  the admin dashboard.
- **Arena**: add an entry to `ArenaDailyStatsService.Metrics()`, choosing `People` or `Platform`.

Nothing else needs touching — the email renders whatever the slices contain.
