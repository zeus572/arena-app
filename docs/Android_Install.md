# Installing Civersify on a real phone (sideloading)

How to get a build of the Civersify Android app onto a consumer device — a
Samsung Galaxy S22, a Pixel, most modern Android phones — without the Play Store.
This is for trying the app (yourself, a friend, a tester), not for public release.

The APK is **debug-signed**, so Android treats it as an app "from an unknown
source." That is expected for sideloading; the steps below are mostly about
telling the phone to allow it once.

- **Toolchain / dev-loop setup:** [Android_Setup.md](Android_Setup.md) · [Android_Dev.md](Android_Dev.md)
- **CI that produces the installable build:** `.github/workflows/build-android-apk.yml`

---

## Step 0 — one-time prod prerequisite (CORS)

**Do this once before the first install, or the app will open to an empty feed
and failed logins on every phone.** The app's WebView serves from the origin
`https://localhost`, so both prod backends must allow that origin. As of
2026-07-17 they do **not** (verified: a preflight from `https://localhost` gets
no `Access-Control-Allow-Origin` header from either API). Add it to the
`Cors__Origins__*` app settings — this is config-only, no redeploy, just a
restart. CORS is owned entirely by these settings; do **not** use Azure platform
CORS (`az webapp cors`), which conflicts (see `infra/README.md`).

Run per app (Galaxy/any device is irrelevant here — this is server-side). The
snippet finds the next free `Cors__Origins__N` index so it appends instead of
clobbering the existing web origins:

```powershell
foreach ($app in 'civic-api-fexzo2','arena-api-2af326') {
  $existing = az webapp config appsettings list -g rg-arena -n $app `
    --query "[?starts_with(name,'Cors__Origins__')].name" -o tsv
  if ($existing -match 'https://localhost') { az webapp config appsettings list -g rg-arena -n $app --query "[?value=='https://localhost']" -o tsv; Write-Host "$app already has it"; continue }
  $next = ($existing | ForEach-Object { [int]($_ -replace '.*__','') } | Measure-Object -Maximum).Maximum + 1
  az webapp config appsettings set -g rg-arena -n $app --settings "Cors__Origins__$next=https://localhost"
  az webapp restart -g rg-arena -n $app   # settings load at startup
}
```

Verify (should now echo the origin):

```powershell
curl.exe -sS -o NUL -D - -X OPTIONS "https://civic-api-fexzo2.azurewebsites.net/api/feed" -H "Origin: https://localhost" -H "Access-Control-Request-Method: GET" | Select-String -i "access-control-allow-origin"
```

---

## Step 1 — Get the APK (from GitHub Actions)

The build runs in CI so you don't need any Android tooling on your machine.

1. Go to the repo on GitHub → **Actions** tab → **Build Civersify APK** workflow.
2. Click **Run workflow** (top-right), leave the inputs blank to build against
   **prod**, and confirm. It takes ~5–8 minutes.
   - The optional inputs let you point the build at a different backend (e.g. a
     staging URL); blank = the prod URLs the live web app uses.
3. Open the finished run → scroll to **Artifacts** → download
   **`civersify-prod-apk`**. It's a zip; unzip it to get **`Civersify.apk`**.
4. Get that `.apk` onto the phone. Any of:
   - Email/message it to your friend and open the attachment on the phone.
   - Upload to Google Drive / Dropbox and download it on the phone.
   - USB transfer, or `adb install Civersify.apk` if you have a cable + platform-tools.

> **Why not the APK from the regular `android-build` CI job?** That one bakes in
> `localhost` and can't reach any backend from a phone. Always use the
> **Build Civersify APK** workflow for on-device installs.

---

## Step 2 — Install on a Samsung Galaxy S22

Samsung's One UI (Android 13/14) asks per-app for permission to install, the
first time each app (Chrome, My Files, Gmail, Drive…) tries to install an APK.

1. Open the `Civersify.apk` however it landed on the phone — usually via
   **My Files** (Samsung's file manager) in the *Downloads* folder, or straight
   from the Gmail/Drive/browser download notification.
2. Android shows **"For your security, your phone is not allowed to install
   unknown apps from this source."** Tap **Settings**.
3. Toggle **Allow from this source** on. (This whitelists whichever app you
   opened the APK *from* — e.g. My Files — not the whole phone.)
4. Back out; tap the APK again. Tap **Install**.
5. If **Google Play Protect** pops up ("App scan" / "Send app to Google?") tap
   **Install anyway** / **Don't send**. It flags this only because the app is
   unknown to Play, not because anything's wrong.
6. Open **Civersify** from the app drawer.

### Other phones (Pixel, generic Android 12+)
Same idea, slightly different labels: opening the APK deep-links you to
**Settings → Apps → Special access → Install unknown apps**, pick the app you're
installing *from* (Files/Chrome), enable **Allow from this source**, then return
and Install. Play Protect may prompt the same way.

---

## Step 3 — Confirm it actually works

The install succeeding only proves the package is valid — not that it can reach
the backend. Have the friend check:

- The **home feed** loads real content (not a spinner or an error card) → the
  Civic API is reachable.
- **Sign in / sign up** returns a normal result (a proper "wrong password", not a
  network error) → the Arena auth API is reachable.

If the app opens but everything is empty or every action errors, jump to
**API calls fail** below.

---

## Updating to a newer build

CI generates a fresh debug signing key on every run, so a newer CI APK is signed
differently from the one already on the phone. Installing over it fails with
**"App not installed"** / `INSTALL_FAILED_UPDATE_INCOMPATIBLE`.

**Fix:** uninstall the existing Civersify first (long-press icon → Uninstall, or
Settings → Apps → Civersify → Uninstall), then install the new APK. (Local
`gradlew` builds keep a stable debug key and update in place — this only affects
CI-built APKs.)

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| No "Allow from this source" prompt, install just blocked | Enable it manually: **Settings → Apps → Special access → Install unknown apps** → the app you're installing from → **Allow**. |
| "App not installed" on an update | Signature mismatch from CI's per-run debug key — uninstall the old Civersify first (see above). |
| App opens but the feed is empty / every API call errors | The bundle didn't get prod URLs, **or** prod CORS is missing the app's origin. The APK must come from **Build Civersify APK** (not `android-build`). On the backend, prod's `Cors:Origins` app setting must include **`https://localhost`** (the Capacitor WebView origin) for *both* the civic and arena App Services — verify in Azure if calls fail. |
| Login fails but the feed loads | Auth lives on the **arena** API, not civic. Its prod `Cors:Origins` must also include `https://localhost`, and `VITE_ARENA_API_URL` must have been set at build time. |
| Play Protect won't let it install | Tap **Install anyway** / **More details → Install anyway**. Expected for a non-Play app. |
| "Do you want to send this app to Google?" | Optional — tap **Don't send**. Doesn't affect the install. |
| Friend on a very locked-down carrier phone | Some carrier builds hide "unknown sources" entirely. Installing via `adb install` over USB from a computer bypasses the UI gate. |

---

## Notes / limits of a debug APK

- **It's not a release build.** Debug-signed, debuggable, no Play Store updates,
  no crash-reporting signing. Fine for "try it out," not for public distribution.
- **It hits prod data.** Sign-ups, votes, and coalition/league writes made from a
  sideloaded build are real prod records. Note that account-bound writes still
  require a **verified email** (the app surfaces the verify-email gate).
- For a genuine release later, the open items are a stable **release keystore**,
  Play Console upload, and the Android **App Links `assetlinks.json`** fingerprint
  (tracked separately) — not needed for sideloading.
