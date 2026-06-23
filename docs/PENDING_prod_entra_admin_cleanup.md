# PENDING: revoke temporary Entra admin on `arena-pgserver`

**Status:** ⚠️ Open loose end — created 2026-06-23. The main task (deleting dead-data
coalitions in prod) is **complete**; this is leftover access cleanup only.

## What happened

To delete 4 heuristic-born "dead" coalition provisions from the prod `civic` database
(no delete endpoint exists; the DB is Entra/MI passwordless), we temporarily:

1. Opened a firewall rule `temp-dead-cleanup` on `arena-pgserver` for the dev machine IP.
2. Registered the dev user as a **Microsoft Entra administrator** on the server.
3. Connected via the local `arena-postgres` Docker container's `psql` (tunnel) and ran the
   transactional delete (`docs`/script: `/tmp/delete_dead_provisions.sql`). **Verified 0 rows remain.**

On teardown:
- ✅ Firewall rule `temp-dead-cleanup` was **removed**.
- ❌ Entra admin removal **FAILED**:
  ```
  AadAuthPrincipalDropFailed — role "sameer_bedekar_hotmail.com#EXT#@sameerbedekarhotmail.onmicrosoft.com"
  cannot be dropped because some objects depend on it
  ```
  Connecting as the user created a Postgres role with a lingering dependency (likely default
  ACLs / privileges), so `az ... microsoft-entra-admin delete` could not drop it.

**Net state today:** the dev user is still a standing Entra admin on `arena-pgserver`
(shared with the debate DB). Not externally reachable (no firewall rule), but a standing
privilege that should be revoked.

## Key values

| Thing | Value |
|---|---|
| Resource group | `rg-arena` |
| PG server | `arena-pgserver` (shared: `arena` + `civic` DBs) |
| Database used | `civic` |
| Entra admin objectId | `6dcb488d-3c2f-4daf-ab33-7c7ff2fe706b` |
| Entra admin UPN (psql `user=`) | `sameer_bedekar_hotmail.com#EXT#@sameerbedekarhotmail.onmicrosoft.com` |
| Firewall rule name | `temp-dead-cleanup` (already removed) |
| Token resource | `az account get-access-token --resource-type oss-rdbms` |

> ⚠️ The dev machine public IP (was `66.114.154.112`) can change — **re-check before re-opening
> the firewall**: `curl -s https://api.ipify.org`.

## Finish-up procedure

```bash
IP=$(curl -s https://api.ipify.org)

# 1. Re-open the firewall for the current machine
az postgres flexible-server firewall-rule create -g rg-arena -n arena-pgserver \
  --rule-name temp-dead-cleanup --start-ip-address "$IP" --end-ip-address "$IP"

# 2. Clear the role's dependencies (run via the local container's psql)
docker start arena-postgres >/dev/null 2>&1 || true
TOKEN=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)
CONN="host=arena-pgserver.postgres.database.azure.com port=5432 dbname=civic user=sameer_bedekar_hotmail.com#EXT#@sameerbedekarhotmail.onmicrosoft.com sslmode=require"
# Inspect what the role owns first (optional):
docker exec -i -e PGPASSWORD="$TOKEN" arena-postgres psql "$CONN" \
  -c "SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_roles r ON r.oid=c.relowner WHERE r.rolname = current_user;"
# Then clear ownership + privileges:
docker exec -i -e PGPASSWORD="$TOKEN" arena-postgres psql "$CONN" \
  -c 'DROP OWNED BY "sameer_bedekar_hotmail.com#EXT#@sameerbedekarhotmail.onmicrosoft.com";'

# 3. Retry the admin drop (control-plane; doesn't need the firewall)
az postgres flexible-server microsoft-entra-admin delete -g rg-arena -s arena-pgserver \
  --object-id 6dcb488d-3c2f-4daf-ab33-7c7ff2fe706b --yes

# 4. Close the firewall again
az postgres flexible-server firewall-rule delete -g rg-arena -n arena-pgserver \
  --rule-name temp-dead-cleanup --yes
```

Note: `microsoft-entra-admin` is the current CLI subcommand (older `ad-admin` was renamed).

## Related

- Code fix that stops these dead coalitions being born again: **PR #32**
  (`fix/bail-synthesis-on-llm-failure`) — bail synthesis on live LLM (`CallFailed`) failures.
- Dead-data identification signature: provisions with stub sub-questions
  "Who/what is covered?" / "Who should decide?" (keys `scope`/`authority`) and a raw
  news-headline title; 0 human participants.
