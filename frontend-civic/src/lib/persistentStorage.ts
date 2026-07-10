import { Capacitor } from "@capacitor/core";
import { Preferences } from "@capacitor/preferences";

// ---------------------------------------------------------------------------
// Durable key/value storage that works on both web and the Capacitor Android
// shell.
//
// localStorage stays the synchronous runtime store everywhere (the token
// manager and axios interceptors depend on sync reads). On a native platform
// every write is mirrored to Capacitor Preferences (Android SharedPreferences),
// which the OS never evicts, and `hydratePersistentStorage` copies Preferences
// back into localStorage at app launch. So if the WebView's storage gets
// cleared (OS storage pressure, "clear cache") the session and anonymous
// identity survive.
//
// On web this module is a plain localStorage passthrough — hydrate is a no-op
// and no Preferences calls are made.
// ---------------------------------------------------------------------------

const isNative = () => Capacitor.isNativePlatform();

export function getStoredItem(key: string): string | null {
  return localStorage.getItem(key);
}

export function setStoredItem(key: string, value: string): void {
  localStorage.setItem(key, value);
  if (isNative()) {
    // Fire-and-forget: Preferences is async but callers need sync semantics.
    // A write lost to a crash just means we fall back to the previous value
    // on next hydrate — never a corrupt state.
    void Preferences.set({ key, value });
  }
}

export function removeStoredItem(key: string): void {
  localStorage.removeItem(key);
  if (isNative()) {
    void Preferences.remove({ key });
  }
}

/**
 * Restore mirrored keys from native Preferences into localStorage. Call once
 * at startup, BEFORE anything reads tokens (i.e. before React renders).
 * localStorage wins when both have a value — it is at least as fresh as the
 * mirror. No-op on web.
 */
export async function hydratePersistentStorage(): Promise<void> {
  if (!isNative()) return;
  const { keys } = await Preferences.keys();
  for (const key of keys) {
    if (localStorage.getItem(key) !== null) continue;
    const { value } = await Preferences.get({ key });
    if (value !== null) localStorage.setItem(key, value);
  }
}
