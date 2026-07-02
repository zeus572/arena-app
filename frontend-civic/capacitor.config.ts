import type { CapacitorConfig } from '@capacitor/cli';

// The WebView serves the bundle from https://localhost (Capacitor's default
// Android scheme). That exact origin is allowlisted in both backends' CORS
// config — change androidScheme/hostname and CORS breaks.
const config: CapacitorConfig = {
  appId: 'com.civersify.app',
  appName: 'Civersify',
  webDir: 'dist',
};

// Live-reload dev loop: `npm run android:dev` sets CAP_SERVER_URL to the Vite
// dev server so the app hot-reloads inside the emulator (see
// docs/Android_Dev.md). Never set for release builds — the config is baked
// into the APK at `cap sync` time.
if (process.env.CAP_SERVER_URL) {
  config.server = {
    url: process.env.CAP_SERVER_URL,
    cleartext: true,
  };
}

// Bundled-dev mode: the baked bundle is served from https://localhost, so its
// calls to the http://localhost:* dev backends are mixed content the WebView
// blocks by default. CAP_DEV_HTTP=1 relaxes that for local testing only —
// a release sync (neither env var set) stays secure-by-default.
if (process.env.CAP_DEV_HTTP) {
  config.android = { allowMixedContent: true };
  config.server = { ...config.server, cleartext: true };
}

export default config;
