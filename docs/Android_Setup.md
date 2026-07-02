# Android Toolchain Setup (Civersify)

One-time machine setup for building/running the Civersify Android app
(`frontend-civic/android/`). For the daily dev loop after setup, see
[Android_Dev.md](Android_Dev.md).

What this repo's Gradle project requires (from `frontend-civic/android/`):

| Requirement | Version | Where it's pinned |
|---|---|---|
| JDK | **21** | `app/capacitor.build.gradle` (source/targetCompatibility) |
| Android Gradle Plugin | 8.13 | `build.gradle` (Gradle downloads itself via the wrapper) |
| compileSdk / targetSdk | 36 | `variables.gradle` |
| minSdk | 24 | `variables.gradle` |

Two setup paths — pick one:

- **Path A: Android Studio** — recommended for people. GUI, emulator manager,
  Logcat, signed-bundle wizard.
- **Path B: command-line only** — fully scriptable; use this for CI runners or
  when an agent (Claude) is doing the setup non-interactively. No GUI needed
  to build APKs or run a headless emulator.

Both paths end at the same [Verification checklist](#verification-checklist).

---

## Path A: Android Studio (people)

1. **Install** — download from <https://developer.android.com/studio> or:

   ```powershell
   winget install Google.AndroidStudio
   ```

2. **First-run wizard** — accept the "Standard" setup; it installs the SDK to
   `%LOCALAPPDATA%\Android\Sdk`, the emulator, and platform-tools, and accepts
   the SDK licenses.

3. **SDK components** — in *Settings → Languages & Frameworks → Android SDK*:
   - *SDK Platforms* tab: check **Android 16 (API 36)** (matches compileSdk).
   - *SDK Tools* tab: ensure **Android SDK Platform-Tools**, **Android
     Emulator**, and **Android SDK Build-Tools** are checked. Apply.

4. **Create an emulator** — *Device Manager → Create Virtual Device* →
   **Pixel 7** (or any phone) → system image **API 35 or 36 (Google APIs,
   x86_64)** → Finish. Start it once to confirm it boots.

5. **Environment variables** (PowerShell, then restart terminals):

   ```powershell
   [Environment]::SetEnvironmentVariable("ANDROID_HOME", "$env:LOCALAPPDATA\Android\Sdk", "User")
   $p = [Environment]::GetEnvironmentVariable("Path", "User")
   [Environment]::SetEnvironmentVariable("Path", "$p;$env:LOCALAPPDATA\Android\Sdk\platform-tools;$env:LOCALAPPDATA\Android\Sdk\emulator", "User")
   ```

6. **JDK 21** — Android Studio bundles one (the "JBR"). For command-line
   Gradle builds outside Studio, point JAVA_HOME at it:

   ```powershell
   [Environment]::SetEnvironmentVariable("JAVA_HOME", "C:\Program Files\Android\Android Studio\jbr", "User")
   ```

   (Or install a standalone JDK: `winget install EclipseAdoptium.Temurin.21.JDK`.)

7. **Open the project** — in Android Studio open `frontend-civic/android`
   (NOT the repo root). First Gradle sync downloads the wrapper + deps
   (several minutes).

---

## Path B: command-line only (CI / agents / headless)

Everything here is non-interactive. On Windows run in PowerShell; the same
commands work on Linux/macOS with the obvious path changes (CI example at the
bottom).

1. **JDK 21**:

   ```powershell
   winget install --silent EclipseAdoptium.Temurin.21.JDK
   # New terminal after install, or set explicitly:
   $env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-21*" | Resolve-Path | Select-Object -First 1 -ExpandProperty Path
   ```

2. **SDK command-line tools** — download the "commandlinetools" zip from
   <https://developer.android.com/studio#command-line-tools-only> and unpack
   so the layout is `<sdk>\cmdline-tools\latest\bin\sdkmanager.bat`:

   ```powershell
   $sdk = "$env:LOCALAPPDATA\Android\Sdk"
   New-Item -ItemType Directory -Force "$sdk\cmdline-tools" | Out-Null
   Invoke-WebRequest "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip" -OutFile "$env:TEMP\cmdtools.zip"
   Expand-Archive "$env:TEMP\cmdtools.zip" "$sdk\cmdline-tools" -Force
   Rename-Item "$sdk\cmdline-tools\cmdline-tools" "latest"
   $env:ANDROID_HOME = $sdk
   ```

   (Check the download page for the current zip version number.)

3. **Accept licenses and install components**:

   ```powershell
   $sdkmanager = "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat"
   1..20 | ForEach-Object { "y" } | & $sdkmanager --licenses
   & $sdkmanager --install "platform-tools" "platforms;android-36" "build-tools;36.0.0" "emulator" "system-images;android-36;google_apis;x86_64"
   ```

   Skip `emulator` + `system-images;...` if the machine only needs to build
   APKs (e.g. CI — our `android-build` job builds without an emulator).

4. **Create and boot a headless emulator** (optional — for on-device testing
   without a GUI):

   ```powershell
   $avdmanager = "$env:ANDROID_HOME\cmdline-tools\latest\bin\avdmanager.bat"
   "no" | & $avdmanager create avd -n civersify -k "system-images;android-36;google_apis;x86_64" -d pixel_7 --force

   & "$env:ANDROID_HOME\emulator\emulator.exe" -avd civersify -no-window -no-audio -no-boot-anim -no-snapshot   # long-running: start in background
   & "$env:ANDROID_HOME\platform-tools\adb.exe" wait-for-device
   # Booted when this prints 1:
   & "$env:ANDROID_HOME\platform-tools\adb.exe" shell getprop sys.boot_completed
   ```

   Windows note: the emulator needs virtualization — Windows Hypervisor
   Platform (WHPX) or Hyper-V enabled (*Turn Windows features on or off*).

5. **PATH** — add `platform-tools`, `emulator`, and
   `cmdline-tools\latest\bin` under `%ANDROID_HOME%` to PATH (see Path A
   step 5).

---

## Verification checklist

Run from `frontend-civic/` after either path (new terminal so env vars apply):

```powershell
java -version          # 21.x
adb version            # Android Debug Bridge ...
npm install            # once per clone/worktree
npm run android:sync   # web build + capacitor sync — must succeed
cd android; .\gradlew assembleDebug; cd ..   # first run downloads Gradle; APK at android\app\build\outputs\apk\debug\app-debug.apk
```

With an emulator/device attached (`adb devices` shows it):

```powershell
npm run android:reverse   # maps device localhost 5175/5050/5000 → this machine
npm run android:dev       # installs + launches with Vite live reload
```

If all of the above pass, hand off to [Android_Dev.md](Android_Dev.md) for the
daily loop (backends + Vite + emulator, debugging via chrome://inspect).

## Notes for Claude / automation

- Prefer **Path B**; nothing in it needs a GUI. Installing an APK and driving
  the app works entirely through `adb` (`adb install`, `adb shell am start -n
  com.civersify.app/.MainActivity`, `adb shell input`, `adb exec-out screencap`).
- Start the emulator with `run_in_background` (it never exits) and poll
  `sys.boot_completed`; a cold boot can take 1–3 minutes.
- `adb reverse` state dies with the emulator — rerun `npm run android:reverse`
  after every emulator (re)start, before testing anything that hits the APIs.
- Gradle's first `assembleDebug` downloads ~500 MB (wrapper + dependencies);
  use a 10-minute timeout.
- Never commit `local.properties` (gitignored — it hard-codes this machine's
  SDK path). Gradle finds the SDK via `ANDROID_HOME` when it's absent.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `SDK location not found` from Gradle | Set `ANDROID_HOME` (or create `frontend-civic/android/local.properties` with `sdk.dir=C:\\Users\\<you>\\AppData\\Local\\Android\\Sdk`) |
| `Failed to install ... licenses have not been accepted` | Rerun the `--licenses` pipe in Path B step 3 (or Android Studio → SDK Manager, which accepts on Apply) |
| Gradle: `invalid source release: 21` / toolchain errors | `JAVA_HOME` points at an older JDK — set it to a JDK 21 (Studio's `jbr` folder works) |
| `adb` not recognized | `%LOCALAPPDATA%\Android\Sdk\platform-tools` missing from PATH; new terminal after editing PATH |
| Emulator won't start / crawls | Enable Windows Hypervisor Platform (WHPX) in Windows features; reboot |
| App launches but every API call fails | `adb reverse` not active — `npm run android:reverse` (emulator restarts clear it) |
| Login works on web but not in the app | Arena backend (:5000) not running or its CORS origin list lacks `https://localhost` — auth lives there, not on :5050 |
