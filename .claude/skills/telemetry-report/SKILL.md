---
name: telemetry-report
description: Query Azure Application Insights (appi-arena) for the Arena/Civic prod apps and produce a health + usage report — request volume, failures, slow endpoints, exceptions, dependency health, frontend page views, and visitor breadth (geography + real-human-vs-bot/scanner split, i.e. "has the site been discovered / by whom"). Run this when the user asks for a telemetry report, prod health, "how are the apps doing", error rates, traffic, or who's visiting/discovering the site over some window. Accepts an optional time window (e.g. "6h", "last 3 days"; default 24h).
---

# Telemetry Report

Produce a prod telemetry report for the Arena (debate) + Civic apps from the
shared Application Insights component `appi-arena`.

## Facts (this project)

- **Subscription**: `71302e2a-e20c-4d21-8f68-39729d74ecc6` (Visual Studio Enterprise)
- **Resource group**: `rg-arena`
- **App Insights component**: `appi-arena` (workspace-based)
- **Log Analytics workspace**: `log-arena`, customerId (GUID) `630c49f2-ea4f-47be-b938-ad152a6aac9d`
- **Role names** (`AppRoleName` / `cloud_RoleName`):
  - `civic-api-fexzo2` — Civic backend
  - `arena-api-2af326` — Debate backend
  - browser role (e.g. a hostname) — Civic frontend page views
- Data lives in the Log Analytics `App*` tables: `AppRequests`, `AppDependencies`,
  `AppExceptions`, `AppTraces`, `AppPageViews`, `AppPerformanceCounters`.

## Important gotchas

- **Query the workspace directly, NOT the classic `az monitor app-insights query` API.**
  The classic API lags/returns empty against this workspace-based component. Always use
  `az monitor log-analytics query -w <GUID> ...`.
- Use the PowerShell tool (or Bash with care) — the leading `/` in resource IDs gets
  mangled by Git Bash MSYS path conversion. Plain workspace-GUID queries are safe either way.
- **Frontend page views**: if `AppPageViews` is empty, the civic frontend hasn't been
  redeployed since the `VITE_APPINSIGHTS_CONNECTION_STRING` secret was set — the connection
  string is compiled in at build time. Report that as the likely cause, not "no traffic".
- `503`s on `AppRequests` during a recent window are usually the startup readiness gate
  during a cold start/deploy, not a real outage — note this if 503s cluster in time.
- **`ClientIP` is fully zeroed** (`0.0.0.0` / `::`) — App Insights masks it entirely, so you
  CANNOT count distinct visitors or exclude your own IP by `ClientIP`. Geo fields
  (`ClientCity`, `ClientStateOrProvince`, `ClientCountryOrRegion`) ARE populated from
  `X-Forwarded-For` at ingestion — group visitor analysis by city/country, not IP.
- **`ClientBrowser`/`ClientOS` are blank** (UA not parsed) — classify visitors by *behavior*
  (paths hit, 404 ratio, preflights, timing), not user-agent.

### KQL syntax gotchas (these silently break queries)
- **Pass each query as a SINGLE-LINE string.** Multi-line here-strings get mangled by the
  CLI and either error or (worse) run a truncated query that returns raw unsummarized rows.
- **Reserved words as column aliases fail with `SYN0002`.** `views` and `kind` are both
  reserved — use `hits`, `visitorType`, etc. When in doubt, pick a non-obvious alias.
- **Don't nest aggregates in a scalar function inside `summarize`.** e.g.
  `datetime_diff('second', max(TimeGenerated), min(TimeGenerated))` returns EMPTY silently.
  Emit `firstSeen=min(...)`, `lastSeen=max(...)` in the summarize, then compute the span in a
  following `| extend spanSec=datetime_diff('second', lastSeen, firstSeen)`.

## Steps

1. **Resolve the workspace GUID** (don't hardcode blindly — confirm it):
   ```
   az monitor log-analytics workspace show -g rg-arena -n log-arena --query customerId -o tsv
   ```
   If this fails, fall back to the documented GUID above. Confirm `az account show`
   is on subscription `71302e2a-...`; if not, `az account set --subscription 71302e2a-e20c-4d21-8f68-39729d74ecc6`.

2. **Parse the time window** from the user's args (default `24h`). Convert to a KQL
   `ago(...)` duration: e.g. "6h" → `6h`, "last 3 days" → `3d`, "week" → `7d`.
   Use the same `WINDOW` in every query below.

3. **Run the queries** with `az monitor log-analytics query -w <GUID> --analytics-query "<KQL>" -o table`.
   Run them in parallel where possible. Substitute your `WINDOW` for `24h`.

   **Request volume + success rate per backend:**
   ```kql
   AppRequests | where TimeGenerated > ago(24h)
   | summarize requests=count(), failures=countif(toint(ResultCode) >= 500),
       clientErrors=countif(toint(ResultCode) between (400 .. 499)),
       p50=round(percentile(DurationMs,50)), p95=round(percentile(DurationMs,95))
     by AppRoleName
   | order by AppRoleName asc
   ```

   **Top failing operations (5xx):**
   ```kql
   AppRequests | where TimeGenerated > ago(24h) and toint(ResultCode) >= 500
   | summarize count() by AppRoleName, Name, ResultCode
   | order by count_ desc | take 15
   ```

   **Slowest endpoints (p95):**
   ```kql
   AppRequests | where TimeGenerated > ago(24h)
   | summarize calls=count(), p95=round(percentile(DurationMs,95)), maxMs=round(max(DurationMs))
     by AppRoleName, Name
   | where calls >= 2 | order by p95 desc | take 15
   ```

   **Exceptions:**
   ```kql
   AppExceptions | where TimeGenerated > ago(24h)
   | summarize count() by AppRoleName, ProblemId, OuterMessage
   | order by count_ desc | take 15
   ```

   **Dependency health (DB/HTTP calls — failures + slow):**
   ```kql
   AppDependencies | where TimeGenerated > ago(24h)
   | summarize calls=count(), failures=countif(Success == false),
       p95=round(percentile(DurationMs,95)) by AppRoleName, DependencyType, Target
   | order by failures desc, p95 desc | take 15
   ```

   **Frontend page views** (NB: alias must not be `views` — that collides with a
   KQL keyword and throws SYN0002; use `hits`):
   ```kql
   AppPageViews | where TimeGenerated > ago(24h)
   | summarize hits=count() by Name
   | order by hits desc | take 15
   ```

   **Traffic over time (hourly, for a trend line):**
   ```kql
   AppRequests | where TimeGenerated > ago(24h)
   | summarize requests=count(), failures=countif(toint(ResultCode) >= 500)
     by bin(TimeGenerated, 1h), AppRoleName
   | order by TimeGenerated asc
   ```

3b. **Visitor breadth ("has the site been discovered, and by whom").** Answers geography
   + a real-human-vs-bot/scanner split. Key reality: this is BACKEND telemetry, so it only
   sees API traffic; true visitor breadth (referring domains, sessions, unique-visitor
   counts) needs FRONTEND page-view telemetry, which is dark until the civic frontend is
   redeployed. Say so. And remember `ClientIP` is zeroed — everything here is geo-grained.

   **Geography breadth (distinct cities/countries + volume):**
   ```kql
   AppRequests | where TimeGenerated > ago(24h)
   | summarize requests=count(), cities=dcount(ClientCity)
       by ClientCountryOrRegion | order by requests desc
   ```

   **Real-human-vs-bot classification (per city — pass as ONE line):**
   ```kql
   AppRequests | where TimeGenerated > ago(24h) | summarize hits=count(), routes=dcount(Name), notFound=countif(toint(ResultCode)==404), preflights=countif(Name startswith 'OPTIONS'), firstSeen=min(TimeGenerated), lastSeen=max(TimeGenerated) by ClientCity, ClientStateOrProvince, ClientCountryOrRegion | extend spanSec=datetime_diff('second', lastSeen, firstSeen) | extend visitorType=case(notFound>=10 and routes>=10,'scanner', routes>=6 and spanSec<=5,'bot/crawler(burst)', preflights>0 and spanSec>60,'likely-human(browser)','unclassified') | project visitorType, ClientCity, ClientStateOrProvince, ClientCountryOrRegion, hits, routes, notFound, preflights, spanSec | order by hits desc | take 30
   ```

   **Scanner-probe detail (what junk paths are being hit, 404s):**
   ```kql
   AppRequests | where TimeGenerated > ago(24h) and toint(ResultCode)==404
   | summarize probes=count() by Name, ClientCity, ClientCountryOrRegion
   | order by probes desc | take 25
   ```

   **Interpreting the classifier (heuristics, not gospel — explain, don't just dump):**
   - `scanner` = many distinct 404 paths from one city (`/.env`, `/wp.php`, `/shell.php`…):
     internet background radiation. Every public host gets it within hours; NOT user discovery.
   - `bot/crawler(burst)` = several real routes fetched in ≤5s: an automated crawler/indexer.
   - `likely-human(browser)` = OPTIONS preflights (a real browser calling the API cross-origin)
     + real routes spread over >60s. This is the genuine-visitor signal.
   - **BUT** the classifier can't see that some cities are cloud datacenters. Treat a
     `likely-human` label from a known cloud-DC city (**Council Bluffs, Mountain View,
     Ashburn, Boydton, Boardman, Des Moines, Dublin, The Dalles**) with suspicion — it's more
     likely a headless-browser bot / link-preview fetcher. Real humans are usually on
     *residential-ISP* cities.
   - **Self-traffic:** the operator's own testing/browsing shows up under their home metro.
     `ClientIP` can't identify it, and third-party geo-IP of the operator's public IP may name
     a *neighboring* city than the one App Insights recorded (observed: ip-api said Issaquah,
     App Insights said Bellevue — same Seattle eastside). So DON'T hard-filter self; instead
     name the operator's likely metro and tell them to discount it. To find it, optionally:
     `$ip=(irm https://api.ipify.org?format=json).ip; (irm "http://ip-api.com/json/$ip").city` —
     then treat that city (and its neighbors/state) as probably-you.

4. **Write the report** as markdown. Structure:
   - **Window & health headline** — one line per backend: requests, success rate, p95.
     Flag anything alarming (success rate < ~95% outside a known deploy window, p95 spikes).
   - **Failures** — top 5xx operations, with the likely readiness-gate caveat if 503s cluster.
   - **Slowest endpoints** — p95 leaders (note the debate B1 cold-query trait is expected).
   - **Exceptions** — top exception types, or "none".
   - **Dependencies** — any failing/slow DB or outbound HTTP.
   - **Frontend** — page views, or the "not redeployed yet" note if empty.
   - **Visitor breadth** — geography (countries/cities), and the real-human-vs-bot/scanner
     split. Lead with the honest headline (usually: mostly scanners + crawlers, plus N
     apparent real human session(s) from <city>). Call out any residential `likely-human`
     cities by name, discount the operator's metro and cloud-DC cities, and note that richer
     visitor data awaits the frontend telemetry redeploy.
   - **Trend** — a sentence on whether traffic/errors are rising/falling over the window.
   - Close with a **portal link** for drill-down:
     `https://portal.azure.com/#@a85d5a36-d4d5-4c6f-a91f-18188bbc4512/resource/subscriptions/71302e2a-e20c-4d21-8f68-39729d74ecc6/resourceGroups/rg-arena/providers/Microsoft.Insights/components/appi-arena/overview`

5. Keep it scannable — tables/short bullets, most important signal first. If the user
   asked about a specific app or symptom, lead with that and trim the rest. Offer to
   publish it as an artifact only if the user wants a shareable page.
