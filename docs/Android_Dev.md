# Civersify Android (Capacitor) — Dev Guide

The Android app is the existing `frontend-civic` React app packaged in a
Capacitor shell (`frontend-civic/android/`). Web and mobile share one codebase:
`npm run build` produces the same bundle both deploy targets use.

## One-time setup

1. Install the Android toolchain (SDK, JDK 21, emulator, adb) — full guide,
   including a scriptable no-GUI path for CI/agents: [Android_Setup.md](Android_Setup.md).
2. `cd frontend-civic && npm install` (Capacitor packages are in package.json).
3. No `.env` needed for local dev — the localhost API defaults work because of
   `adb reverse` (below).

## Daily dev loop (live reload — recommended)

Terminal 1 — backends (from repo root):

```bash
docker start arena-postgres
dotnet run --project backend --urls "http://localhost:5000"   # identity/auth
dotnet run --project backend-civic                            # civic API :5050
```

Terminal 2 — frontend:

```bash
cd frontend-civic
npm run dev                # Vite on :5175
```

Terminal 3 — the app (emulator already started from Android Studio, or it
will prompt you to pick one):

```bash
cd frontend-civic
npm run android:reverse    # maps device localhost:5175/5050/5000 → your PC
npm run android:dev        # builds shell, installs, launches; hot-reloads from Vite
```

Edits to `src/` hot-reload inside the emulator exactly like the browser.

> `adb reverse` mappings reset when the emulator restarts — rerun
> `npm run android:reverse` after a reboot. It also works on a physical phone
> over USB (enable USB debugging).

## Testing the real bundled app against local backends

Live reload serves from Vite; to test what actually ships (bundle baked into
the APK, served from `https://localhost`):

```bash
npm run android:sync:devhttp   # build + sync with mixed-content allowed (dev only)
npm run android:open           # opens Android Studio → Run ▶
npm run android:reverse        # still needed for :5050/:5000
```

`CAP_DEV_HTTP=1` allows the https-origin WebView to call the http dev
backends. A plain `npm run android:sync` (no env vars) is the secure release
configuration.

## Debugging

- **chrome://inspect** in desktop Chrome → full DevTools (console, network,
  sources) attached to the app's WebView.
- Native logs: Logcat in Android Studio (filter tag `Capacitor`).
- Backend cold start: both APIs hold traffic with 503 + Retry-After while
  migrating/seeding. The app no longer logs you out on this (fixed in
  AuthContext), but data screens may briefly show empty/error states — wait
  for `/health` to go green.

## Release builds (later)

- Build with prod API URLs baked in:
  `VITE_CIVIC_API_URL` / `VITE_ARENA_API_URL` set to the prod endpoints, then
  `npm run android:sync` and build a signed AAB from Android Studio
  (Build → Generate Signed Bundle).
- **Prod CORS**: add `https://localhost` (the Capacitor WebView origin) to the
  `Cors:Origins` app setting on BOTH Azure App Services (civic + arena/auth).
- **App Links**: `frontend-civic/public/.well-known/assetlinks.json` ships with
  the web app; replace the placeholder SHA-256 with the release-signing
  fingerprint (`keytool -list -v -keystore <keystore>`) or App Links will not
  verify. Debug builds use your machine's debug keystore fingerprint
  (`keytool -list -v -keystore %USERPROFILE%\.android\debug.keystore -alias androiddebugkey -storepass android`).
- The app checks `GET /api/meta` on launch (native only) and shows a blocking
  update prompt when `minAndroidAppVersion` exceeds the installed
  `versionCode` — bump `minAndroidAppVersion` in backend-civic config only
  when an API change genuinely breaks older bundles.

## Sync discipline (web ↔ mobile)

The Play Store adds days of lag: old app bundles keep hitting the newest API.

- Keep API changes **additive** — never remove/rename fields or endpoints a
  shipped bundle reads.
- If a breaking change is unavoidable, bump `Meta:MinAndroidAppVersion`
  (backend-civic) to force-upgrade older installs, and release the new APK
  first.
