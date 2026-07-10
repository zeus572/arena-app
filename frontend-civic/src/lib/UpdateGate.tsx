import { useEffect, useState } from "react";
import { Capacitor } from "@capacitor/core";
import { App as CapacitorApp } from "@capacitor/app";
import { civicApi } from "@/api/client";

/**
 * Native-only launch check against GET /api/meta: when the installed app's
 * versionCode is below the backend's MinAndroidAppVersion, block the UI with
 * an update prompt. Fails open — a cold backend (503 warmup) or offline
 * launch must never lock a valid app out. No-op on web.
 */
export default function UpdateGate() {
  const [updateRequired, setUpdateRequired] = useState(false);

  useEffect(() => {
    if (!Capacitor.isNativePlatform()) return;
    void (async () => {
      try {
        const [{ data }, info] = await Promise.all([
          civicApi.get<{ minAndroidAppVersion: number }>("/meta"),
          CapacitorApp.getInfo(),
        ]);
        const installed = Number(info.build);
        if (Number.isFinite(installed) && installed < data.minAndroidAppVersion) {
          setUpdateRequired(true);
        }
      } catch {
        // Fail open (see docstring).
      }
    })();
  }, []);

  if (!updateRequired) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[var(--paper,#faf7f2)] p-8">
      <div className="max-w-sm text-center">
        <p className="display text-3xl">Update required</p>
        <p className="mt-3 text-sm opacity-80">
          This version of Civersify is too old to talk to the server. Please
          install the latest update to keep going.
        </p>
        <a
          className="mt-6 inline-block rounded border border-current px-5 py-2 text-sm font-semibold"
          href="market://details?id=com.civersify.app"
        >
          Open Play Store
        </a>
      </div>
    </div>
  );
}
