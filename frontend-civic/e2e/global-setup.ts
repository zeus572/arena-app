import { request } from "@playwright/test";

const BACKEND_HEALTH = "http://localhost:5050/health";
const TIMEOUT_MS = 5_000;

export default async function globalSetup() {
  const api = await request.newContext();
  const started = Date.now();
  let lastErr: unknown;

  while (Date.now() - started < TIMEOUT_MS) {
    try {
      const resp = await api.get(BACKEND_HEALTH);
      if (resp.ok()) {
        await api.dispose();
        return;
      }
      lastErr = new Error(`Health check responded ${resp.status()}`);
    } catch (err) {
      lastErr = err;
    }
    await new Promise((r) => setTimeout(r, 250));
  }

  await api.dispose();
  throw new Error(
    `Civic backend not reachable at ${BACKEND_HEALTH}. ` +
      `Start it with: dotnet run --project backend-civic --urls http://localhost:5050\n` +
      `Last error: ${lastErr instanceof Error ? lastErr.message : String(lastErr)}`,
  );
}
