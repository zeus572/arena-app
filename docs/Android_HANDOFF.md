# Civersify Mobile — Work-in-Progress Handoff

Resume point for the Android/iOS effort. This travels with the branch so a new
session (or a rebooted machine) can pick up without re-deriving context.

- **Branch:** `feature/android-capacitor` (pushed to origin; no PR yet)
- **Worktree:** `.claude/worktrees/android-capacitor`
- **Approach:** Capacitor shell around the existing `frontend-civic` React app —
  one codebase, native Android + iOS build targets. iOS builds are CI-only
  (macOS required).
- **Setup guide:** [Android_Setup.md](Android_Setup.md) · **Dev loop:** [Android_Dev.md](Android_Dev.md)

## Status: what's DONE

All six planned steps are implemented, committed, and pushed. Web build + unit
tests (`npm run build`, `npm test` in `frontend-civic`) pass; a local
`gradlew assembleDebug` produced a 4.7 MB APK on 2026-07-06.

| Commit | What |
|---|---|
| `e39ef4f` | Auth: stop logging users out on transient 503/offline errors (only 400/401/403 clears tokens). Fixes the long-standing "data vanished" web bug too. |
| `a93a17b` | Durable token/identity storage: `persistentStorage.ts` mirrors localStorage → Capacitor Preferences, hydrated before first render. |
| `7c9f2c1` | Capacitor scaffold (`com.civersify.app`, committed `android/`) + `https://localhost` added to CORS on both backends. |
| `92366ad` | Emulator dev loop: `android:reverse` / `android:dev` npm scripts + docs. |
| `6e7bb7b` | Android App Links for verify-email / reset-password / league invites + `DeepLinkListener`. |
| `8193066` | `GET /api/meta` min-version gate + `UpdateGate` + Android CI job. |
| `8251994`, `69b05d9` | `Android_Setup.md` (GUI + scriptable paths), corrected after a real Path B run. |
| `e60459b` | iOS: `@capacitor/ios` + unsigned `ios-build` CI job (macOS runner). |

## Status: FIRST SMOKE TEST DONE (2026-07-09)

The emulator is installed and the first full smoke test **passed on emulator-5554**
(Pixel 7, android-36 google_apis x86_64). All checks green:

| Check | Result |
|---|---|
| App launches, WebView loads (Vite live-reload) | ✅ |
| Home feed renders (civic API :5050) | ✅ |
| `/shorts` feed (client feed mixer) | ✅ |
| Login reaches auth :5000 (proper 401 on bad creds, not a network error) | ✅ |
| Deep link `https://civersify.com/leagues/join/…` → in-app route + :5050 lookup | ✅ |
| Sign-up (invite-gated) → authenticated session | ✅ |
| **Token persistence across a full `am force-stop` + relaunch** | ✅ |
| Coalition write → verified-email gate modal (correct 403 handling) | ✅ |

Two things learned during the run (both now baked into the setup):

- **Emulator disk:** `emulator` + system image need ~7.4 GB free to create the
  userdata partition; the `pixel_7` device profile re-stamps
  `disk.dataPartition.size = 6 GB` in `config.ini` on every launch, so shrinking
  it there does NOT stick. Free host disk instead (clearing `npm cache clean
  --force` / NuGet caches is the easy win).
- **Vite over adb reverse:** modern Node binds `localhost` to IPv6 `::1` only, but
  `adb reverse tcp:5175` forwards to IPv4 `127.0.0.1` → WebView shows
  `net::ERR_EMPTY_RESPONSE` while `curl localhost:5175` (also ::1) looks fine. Run
  the dev server as **`npm run dev -- --host 127.0.0.1`** (consider baking
  `server.host: '127.0.0.1'` into `vite.config.ts`).
- **Sign-up is invite-gated:** the register form's invite field is `required` and
  the arena backend validates it against `Auth:InviteCode` (dev value `ARENA7X`).
  A throwaway dev account `smoke0709@example.com` was created in the dev DB.

## Status: what's PENDING (the resume TODO list)

Nothing is half-written — these are deliberate next actions, roughly ordered:

1. **Open the PR** — CI's `android-build` and `ios-build` jobs have NOT run yet
   (they trigger on PR / master push). The iOS `xcodebuild` invocation is
   unverified until then; expect a possible tweak (scheme/CocoaPods).
2. **Before any real release build:**
   - Add `https://localhost` to the `Cors:Origins` app setting on BOTH prod
     Azure App Services (civic + arena/auth).
   - Replace the placeholder SHA-256 in
     `frontend-civic/public/.well-known/assetlinks.json` with the
     release-signing fingerprint, or App Links won't verify.
   - Build with prod `VITE_CIVIC_API_URL` / `VITE_ARENA_API_URL` baked in.
3. **iOS on a device / TestFlight** — needs an Apple Developer account ($99/yr)
   + signing + a Mac (or cloud Mac / Ionic Appflow) for Simulator testing.
   Deferred; CI only proves it compiles.

## This machine's toolchain (installed 2026-07-06, Path B)

- **JDK 21.0.11** (portable Temurin): `%LOCALAPPDATA%\Java\jdk-21.0.11+10`
- **Android SDK:** `%LOCALAPPDATA%\Android\Sdk` — platform-tools, platform
  android-36, build-tools 36.0.0. **No emulator/system-image yet** (see pending #1).
- User env vars `JAVA_HOME`, `ANDROID_HOME`, PATH are set — open a **new
  terminal** for them to apply.

## Resume the dev loop (after setup)

From repo root, three terminals (full detail in Android_Dev.md):

```
docker start arena-postgres
dotnet run --project backend --urls "http://localhost:5000"   # auth/identity
dotnet run --project backend-civic                            # civic API :5050
cd frontend-civic && npm run dev                              # Vite :5175
# emulator running (or phone via USB):
cd frontend-civic && npm run android:reverse && npm run android:dev
```

`adb reverse` state dies on emulator restart — rerun `android:reverse`.

## Key files (where things live)

- `frontend-civic/capacitor.config.ts` — appId, webDir, env-gated dev server URL
- `frontend-civic/src/lib/persistentStorage.ts` — storage adapter
- `frontend-civic/src/lib/DeepLinkListener.tsx` — App Link → react-router
- `frontend-civic/src/lib/UpdateGate.tsx` — min-version launch gate
- `frontend-civic/src/auth/tokenManager.ts` — `errorStatus`/`isDefinitiveAuthRejection` (the 503 fix)
- `frontend-civic/android/app/src/main/AndroidManifest.xml` — App Link intent-filter
- `frontend-civic/public/.well-known/assetlinks.json` — **placeholder fingerprint**
- `backend-civic/Controllers/Api/MetaController.cs` — `/api/meta`
- `backend/Program.cs`, `backend-civic/Program.cs` — CORS `https://localhost`
- `.github/workflows/ci.yml` — `android-build` + `ios-build` jobs

## Gotchas learned (don't rediscover these)

- Piping `y` into `sdkmanager --licenses` **fails on Windows** — pre-write the
  license hash files (done in Android_Setup.md).
- The winget Temurin MSI hangs a non-interactive session on a UAC prompt — use
  the portable zip.
- Login can pass on web but fail in the app if the **arena backend (:5000)** is
  down or missing the `https://localhost` CORS origin — auth lives there, not
  on :5050.
- `ios/` is intentionally **not committed** (can't `cap add ios` on Windows);
  CI regenerates it. `android/` **is** committed.
