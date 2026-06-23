import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { arenaApi } from "@/auth/arenaAuthClient";

type Status = "verifying" | "success" | "error";

export default function MagazineVerifyEmail() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const [status, setStatus] = useState<Status>("verifying");
  const [message, setMessage] = useState("");
  const ran = useRef(false);

  useEffect(() => {
    // Guard against StrictMode double-invoke — the token is single-use.
    if (ran.current) return;
    ran.current = true;

    if (!token) {
      setStatus("error");
      setMessage("This verification link is missing its token.");
      return;
    }
    arenaApi
      .get("/auth/verify-email", { params: { token } })
      .then(() => setStatus("success"))
      .catch((err) => {
        const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
        setStatus("error");
        setMessage(msg ?? "This verification link is invalid or has expired.");
      });
  }, [token]);

  return (
    <article className="mx-auto max-w-md" data-testid="magazine-verify-email">
      <p className="text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
        Account
      </p>
      {status === "verifying" && (
        <>
          <h1 className="display mt-2 text-4xl">Verifying your email…</h1>
          <p className="mt-3 text-sm text-[var(--fg-soft)]">One moment.</p>
        </>
      )}
      {status === "success" && (
        <>
          <h1 className="display mt-2 text-4xl">Email verified</h1>
          <p className="mt-3 text-sm text-[var(--fg-soft)]">
            Thanks — your email address is confirmed.
          </p>
          <p className="mt-6 text-sm text-[var(--fg-soft)]">
            <Link to="/" className="text-[var(--accent)] underline">
              Continue to Civersify
            </Link>
          </p>
        </>
      )}
      {status === "error" && (
        <>
          <h1 className="display mt-2 text-4xl">Verification failed</h1>
          <p className="mt-3 text-sm text-red-600">{message}</p>
          <p className="mt-3 text-sm text-[var(--fg-soft)]">
            You can request a new link from settings after signing in.
          </p>
        </>
      )}
    </article>
  );
}
