import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Capacitor } from "@capacitor/core";
import { App as CapacitorApp } from "@capacitor/app";

/**
 * Routes Android App Links into the SPA. When the OS hands the app a verified
 * https://civersify.com/... URL (email verify, password reset, league invite —
 * see the intent-filter in AndroidManifest.xml), the WebView does NOT navigate
 * by itself; this listener maps the URL onto react-router. No-op on web.
 * Must render inside <BrowserRouter>.
 */
export default function DeepLinkListener() {
  const navigate = useNavigate();

  useEffect(() => {
    if (!Capacitor.isNativePlatform()) return;
    const listener = CapacitorApp.addListener("appUrlOpen", ({ url }) => {
      try {
        const parsed = new URL(url);
        navigate(parsed.pathname + parsed.search);
      } catch {
        // Not a parseable URL — ignore rather than crash the shell.
      }
    });
    return () => {
      void listener.then((l) => l.remove());
    };
  }, [navigate]);

  return null;
}
