# Civic Arena — Docs

Civic Arena ("Public Lab") is a youth-first civic-literacy app: it turns current
events into balanced briefings, teaches civic concepts, runs quizzes, and helps
users build a nuanced, non-partisan **Civic Compass** by resolving value
tradeoffs. It is a sibling of the **Political Arena** debate app and shares its
login and news pipeline.

It began as a frontend-only prototype (mocked data, five visual directions). It
is now a full stack — .NET 8 + PostgreSQL backend, a React frontend wired to it,
LLM content generation from live news, shared auth with Political Arena, and an
Azure deployment.

## Start here

| Doc | Status | Read it for |
|---|---|---|
| **[ARCHITECTURE.md](./ARCHITECTURE.md)** | ✅ current | What actually exists today — stack, data model, API, frontend, auth, news pipeline, scoring, deployment, tests. **Primary reference.** |
| [civic_arena_values_profile_spec.md](../civic_arena_values_profile_spec.md) | 📘 spec (implemented) | The design intent behind the Civic Compass scoring (axes, archetypes, tradeoffs). Built and live; see ARCHITECTURE §7 for the as-built. |
| [DESIGN_BRIEF.md](./DESIGN_BRIEF.md) | 📜 vision (POC-era) | Original product vision, audience, modules, brand. The north star; still useful for *why*. |
| [SAMPLE_CONTENT.md](./SAMPLE_CONTENT.md) | 📜 reference (POC-era) | Hand-authored sample briefings/concepts/quizzes — now living in `backend-civic/Seed/*.json`. |
| [FRONTEND_PROTOTYPE_SPEC.md](./FRONTEND_PROTOTYPE_SPEC.md) | ⚠️ superseded | The original frontend-only / mocked-data prototype spec. Historical; the premise (no backend) no longer holds. |
| [V0_PROMPTS.md](./V0_PROMPTS.md) | ⚠️ historical | Prompts used to generate the original v0 visual prototypes. Not part of the current build. |

> If you're giving these to a fresh assistant as project context: **ARCHITECTURE.md
> is authoritative.** The POC-era docs describe intent and history, not the
> current implementation — don't treat their "mocked data, no backend"
> statements as true anymore.
