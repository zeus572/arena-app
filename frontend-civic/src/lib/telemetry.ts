import { ApplicationInsights } from "@microsoft/applicationinsights-web";

// Basic, low-cost usage telemetry via Azure Application Insights.
//
// Runs COOKIELESS by default (disableCookiesUsage), so it collects page views
// and events without setting any non-essential cookies — no consent banner is
// required for this mode. If you later want cookie-based user/session
// correlation, gate enableCookies() on the "civersify:consent" event emitted by
// the cookie-consent banner.
//
// A no-op when VITE_APPINSIGHTS_CONNECTION_STRING is unset (e.g. local dev), so
// nothing is sent and there's nothing to configure to run locally.

let ai: ApplicationInsights | null = null;

export function initTelemetry(): void {
  if (ai) return;
  const connectionString = import.meta.env.VITE_APPINSIGHTS_CONNECTION_STRING;
  if (!connectionString) return;

  ai = new ApplicationInsights({
    config: {
      connectionString,
      disableCookiesUsage: true, // cookieless: consent-clean basic hits
      enableAutoRouteTracking: true, // count SPA route changes as page views
      disableFetchTracking: false,
    },
  });
  ai.loadAppInsights();
  ai.trackPageView();
}

/** Optional custom event helper for key actions (sign-up, share, etc.). */
export function trackEvent(name: string, properties?: Record<string, unknown>): void {
  ai?.trackEvent({ name }, properties);
}
