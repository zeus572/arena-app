# LLM Cost & Model Mapping — Claude ↔ OpenAI GPT

Civersify (and the shared `Arena.Shared.Llm` stack) run on Anthropic Claude as the
primary LLM. OpenAI GPT‑5.6 is wired in as a **runtime backup**: when a Claude call
fails, is rate‑limited, runs out of credits, or times out, the same structured‑JSON
request is re‑issued against GPT (see `FallbackLlmClient`). This file is the reference
for which GPT model backs which Claude tier and what each costs.

> Prices are per **1M tokens** (input / output), current as of **July 2026**. Verify
> against the source links before relying on them for billing — provider pricing moves.

## Tiers in use

The app never hardcodes a model per feature. Every call goes through `ILlmClient`
and picks one of two **tiers** (`LlmModelTier`), resolved from config:

| Tier | Used for | Claude model (`Anthropic:*`) | GPT backup (`OpenAI:*`) |
|---|---|---|---|
| **Sonnet** (default) | Content generation — bills, explanations, campaign posts, zeitgeist | `SonnetModel` = `claude-sonnet-4-6` | `SonnetBackupModel` = `gpt-5.6-terra` |
| **Haiku** (cheap) | Boolean judges, contradiction detection, small decisions | `HaikuModel` = `claude-haiku-4-5-20251001` | `HaikuBackupModel` = `gpt-5.6-luna` |

## Cost mapping

| Tier | Primary (Claude) | in / out $/1M | → Backup (GPT) | in / out $/1M | Context | Notes |
|---|---|---|---|---|---|---|
| **Sonnet** | `claude-sonnet-4-6` | **$3 / $15** | `gpt-5.6-terra` | **$2.50 / $15** | ~1M | OpenAI's *balanced* mid‑tier — same role as Sonnet, ~same price, ~1.05M context / 128K output. |
| **Haiku** | `claude-haiku-4-5` | **$1 / $5** | `gpt-5.6-luna` | **$1 / $6** | 200K (Claude) | OpenAI's *fast* tier — same role as Haiku, near‑identical price. |

### Why these pairings

- **Terra ↔ Sonnet 4.6** — both are the "balanced" default tier of their family:
  strong reasoning + JSON generation at mid‑tier price. Terra is the closest match on
  both role and cost, with matching ~1M context.
- **Luna ↔ Haiku 4.5** — both are the fast/cheap tier used for high‑volume boolean
  judges and small decisions. Prices line up almost exactly ($1/$6 vs $1/$5).

Staying inside a single GPT family (5.6) keeps the backup consistent and easy to reason
about.

### ⚠️ Pin the explicit model IDs

The bare **`gpt-5.6`** alias routes to **Sol** — the $5/$30 flagship (Opus‑class). Always
use `gpt-5.6-terra` and `gpt-5.6-luna` explicitly, or the "cheap" tier silently bills at
flagship rates.

## Full GPT‑5.6 family (reference)

| GPT‑5.6 model | in / out $/1M | Role | Claude analog |
|---|---|---|---|
| `gpt-5.6-sol` | $5 / $30 | Deepest reasoning (flagship / `gpt-5.6` alias) | Opus tier *(not used by Civic)* |
| `gpt-5.6-terra` | $2.50 / $15 | Balanced | **Sonnet 4.6** ← in use |
| `gpt-5.6-luna` | $1 / $6 | Fast / cheapest | **Haiku 4.5** ← in use |

Cheaper alternative for the Haiku tier only: `gpt-5.4-nano` (~$0.20 / $1.25) is far
cheaper than Luna but a real capability step down — riskier for the structured‑JSON
judges. Keep Luna as the default backup; treat nano as a cost lever if judge volume
becomes the cost driver.

## Configuration

Model IDs live in `appsettings.json`; **secrets and kill‑switches do not** (dev
user‑secrets / prod env vars only), mirroring the existing Anthropic pattern.

```jsonc
// backend-civic/appsettings.json
"Anthropic": {
  "ApiKey": "",                                  // set via user-secrets / env
  "SonnetModel": "claude-sonnet-4-6",
  "HaikuModel":  "claude-haiku-4-5-20251001"
},
"OpenAI": {
  "ApiKey": "",                                  // set via user-secrets / env
  "SonnetBackupModel": "gpt-5.6-terra",
  "HaikuBackupModel":  "gpt-5.6-luna"
}
```

Set the OpenAI key locally without committing it:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project backend-civic
```

### Kill switches

Both providers have a symmetric `Enabled` flag (default **`true`**, never commit `false`
to prod config). When a provider is `Enabled=false` or keyless it is treated as
*unavailable by design* — callers fall back to their heuristics rather than erroring.

- **Pause Claude only** (let GPT serve) → `Anthropic:Enabled=false`.
- **Pause ALL LLM spend on a dev box** → set **both** `Anthropic:Enabled=false` **and**
  `OpenAI:Enabled=false`.

```bash
dotnet user-secrets set "OpenAI:Enabled" "false" --project backend-civic
```

## How the fallback behaves

`FallbackLlmClient` tries Claude first and re‑issues the request on GPT when Claude:

- returns an API error / is rate‑limited / out of credits (`LlmFailureKind.CallFailed`),
- returns unparseable content even after the JSON retry (`BadResponse`),
- is disabled or keyless (`Unavailable`) — a turned‑off Claude routes to GPT,
- throws a transport error (`HttpRequestException`) or times out.

Caller‑initiated cancellation is **never** retried — it propagates untouched.

If **both** providers fail, the surfaced exception carries the more severe failure kind
(`CallFailed` > `BadResponse` > `Unavailable`), so downstream behavior is unchanged: a
genuine outage still tells batch jobs to bail, while "both unavailable" still lets
on‑demand callers fall back to heuristics. With OpenAI unconfigured, the whole thing
degrades to Claude‑only.

### Caching note

Claude caching uses an explicit ephemeral `cache_control` breakpoint on the system
prompt. **OpenAI caches repeated prompt prefixes automatically** (~10% cache‑read rate,
no `cache_control` field), so the GPT backup path needs no cache plumbing — reused system
prompts cache on their own.

## Sources

- [GPT‑5.6 — OpenAI announcement](https://openai.com/index/gpt-5-6/)
- [GPT‑5.6 Sol / Terra / Luna — DataCamp](https://www.datacamp.com/blog/gpt-5-6-sol-luna-terra)
- [GPT‑5.6 Terra: price, model ID, comparison — Coursiv](https://coursiv.io/blog/gpt-5-6-terra)
- [How to use the GPT‑5.6 API — Apidog](https://apidog.com/blog/how-to-use-gpt-5-6-api/)
- [OpenAI API Pricing (July 2026) — BenchLM](https://benchlm.ai/openai/api-pricing)
- [OpenAI API Pricing docs](https://developers.openai.com/api/docs/pricing)
