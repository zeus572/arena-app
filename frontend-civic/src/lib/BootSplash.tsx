import { useEffect } from "react";
import { Capacitor } from "@capacitor/core";
import { useAuth } from "@/auth/AuthContext";

// Dismisses the branded boot splash (#boot-splash, markup lives in index.html so
// it paints before the bundle loads). Renders nothing — it just fades and
// removes the static element once the app is hydrated and auth has resolved.
//
// On the native shell we hold the splash for a short minimum so the logo's
// entrance actually reads instead of flashing; on web we drop it the instant
// auth is ready (a fast localStorage read), keeping load feeling instant.

// Captured at module-eval time — the closest proxy we have for "app boot".
const BOOT_AT = Date.now();
const NATIVE_MIN_MS = 1100;
const FADE_MS = 450; // must match #boot-splash opacity transition in index.html

function dismissSplash(): void {
  const el = document.getElementById("boot-splash");
  if (!el) return;
  el.classList.add("boot-splash--hide");
  // Remove after the fade so it's gone from the accessibility tree / tab order.
  window.setTimeout(() => el.remove(), FADE_MS);
}

export default function BootSplash() {
  const { isLoading } = useAuth();

  useEffect(() => {
    if (isLoading) return;
    if (!document.getElementById("boot-splash")) return;

    const minMs = Capacitor.isNativePlatform() ? NATIVE_MIN_MS : 0;
    const remaining = Math.max(0, minMs - (Date.now() - BOOT_AT));
    const id = window.setTimeout(dismissSplash, remaining);
    return () => window.clearTimeout(id);
  }, [isLoading]);

  return null;
}
