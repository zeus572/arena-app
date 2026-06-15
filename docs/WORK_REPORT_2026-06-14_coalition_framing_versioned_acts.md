# Work Report — Coalition page: framing dedup + version-aware acts

**Date:** 2026-06-14
**Branch:** `fix/coalition-framing-and-versioned-acts`
**Scope:** Two coalition-provision-detail page fixes.

---

## Issues addressed

1. **Redundant governance framing.** The "Governance framing" card duplicated the
   subtitle under the title. Root cause: in the heuristic fallback path,
   `TwoFramingsService.Fallback` sets `governanceFrame = neutralText` — the exact
   text rendered as the subtitle. (When the premium LLM path runs, the governance
   frame is a genuinely distinct reframing, so the duplication is fallback-only.)

2. **Daily acts out of context.** The daily acts (react-with-reason, steelman)
   floated free of any specific proposal wording — you reacted "Workable" /
   "Unworkable" against nothing in particular. The backend even judged steelmen
   against `Provision.NeutralText`, never the version the user was looking at.

---

## What changed

### Issue 1 — drop the redundant subtitle (frontend only)
- `frontend-civic/src/prototypes/magazine/pages/CoalitionProvisionDetail.tsx`
  - Removed the `<p>{d.neutralText}</p>` subtitle under the title.
  - Nothing is lost: the proposition still appears in the governance-framing card
    and in the "Prevailing coalition position" box. Both framings always render.

### Issue 2 — make acts version-aware (full stack, chosen over frontend-only)
Acts are now attributable to, and judged against, a specific provision version
(the prevailing coalition wording shown on the page).

- **Model** — `backend-civic/Models/Coalition/CoalitionAct.cs`
  - Added nullable `Guid? VersionId` (mirrors the existing loose `ProvisionId`,
    no FK navigation).
- **DTO** — `backend-civic/Services/Coalition/Product/CoalitionDtos.cs`
  - `ActRequest` gained optional `Guid? VersionId = null`.
- **Controller** — `backend-civic/Controllers/Api/CoalitionProvisionsController.cs`
  - The per-provision `POST {id}/acts` endpoint threads `req.VersionId` into
    `RecordActAsync`. (The global `/api/coalition/acts` endpoint is unchanged.)
- **Service** — `backend-civic/Services/Coalition/Product/CoalitionLoopService.cs`
  - `RecordActAsync` takes an optional `versionId`. When supplied, it loads that
    version, validates it belongs to the provision, and **judges the act against
    the version's `Text` instead of `NeutralText`**. Unknown/mismatched versions
    are dropped to `null` so no dangling reference is stored. `VersionId` is
    persisted on the `CoalitionAct` row.
- **Migration** — `backend-civic/Migrations/20260613120000_AddCoalitionActVersionId.{cs,Designer.cs}`
  plus the `CivicDbContextModelSnapshot.cs` update.
  - Adds a nullable `uuid` `VersionId` column to `CoalitionActs`.
  - **Hand-authored** (the SDK wasn't available when first written) but since
    **verified** against the model — see below.
- **Frontend API** — `frontend-civic/src/api/coalition.ts`
  - `recordAct(id, type, payload?, versionId?)` sends `versionId`.
- **Frontend page** — `CoalitionProvisionDetail.tsx`
  - The daily-acts card now shows a context box ("Reacting to the leading wording"
    / "…the proposal as proposed") with the prevailing version's text.
  - Reaction buttons and the steelman submit pass `leadingVersion?.id`.

---

## Verification done on this machine

The .NET 8 SDK (8.0.422) and `dotnet-ef` (8.0.28) were installed mid-session;
frontend deps were installed too.

| Check | Command | Result |
|---|---|---|
| Backend build | `dotnet build` (in `backend-civic`) | ✅ 0 warnings, 0 errors |
| Migration recognized | `dotnet-ef migrations list` | ✅ `20260613120000_AddCoalitionActVersionId` is latest |
| Migration ↔ model in sync | `dotnet-ef migrations has-pending-model-changes` | ✅ "No changes have been made to the model since the last migration" (exit 0) |
| Frontend type-check | `npx tsc --noEmit` (in `frontend-civic`) | ✅ 0 errors |

The `has-pending-model-changes` pass is the important one: it confirms the
hand-authored migration `.cs` / `.Designer.cs` / snapshot exactly match what EF
would have generated, so the migration is safe to apply.

---

## NOT done — next steps (run on a machine with the full toolchain)

The only thing that couldn't run here is applying the migration to a database,
because **Docker is not installed on this machine** (no `docker` CLI). On a machine
with Docker + the SDK:

1. **Apply the migration to the dev DB:**
   ```bash
   docker start arena-postgres
   cd backend-civic
   dotnet-ef database update      # applies AddCoalitionActVersionId
   ```
   Expected: adds the nullable `VersionId` column to `CoalitionActs`. Reversible —
   the migration has a `Down` that drops the column.

2. **Smoke-test the behavior** (optional but recommended):
   - Run backend (`dotnet run --urls http://localhost:5000`) + frontend
     (`npm run dev`).
   - Open a coalition provision detail page. Confirm:
     - the subtitle under the title is gone, both framing cards still render;
     - the daily-acts card shows the prevailing wording in its context box;
     - reacting / submitting a steelman succeeds, and the new `CoalitionActs` row
       has `VersionId` populated (query the DB to confirm).

3. **Run backend tests** to be safe:
   ```bash
   cd backend-civic-tests
   dotnet test
   ```
   No tests were changed; existing coalition act tests should still pass since
   `VersionId` is optional and defaults to `null`.

---

## Possible follow-up (deferred — not in this branch)

The internal callers that already operate on a specific version still record their
acts **without** a `VersionId`:
- `CoSign` (in `CastAcceptanceAsync`) — has the accepted version id.
- `Amend` (in `ProposeAmendmentAsync` / `ProposeFreeformAmendmentAsync`) — creates a version.

Wiring those through `RecordActAsync(..., versionId:)` would make the entire act
ledger version-attributable, not just the daily acts. Left out to keep this change
scoped to the two reported issues.
