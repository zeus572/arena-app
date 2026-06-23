import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { verifyEmail } from "@/api/client";
import { MailCheck } from "lucide-react";

type Status = "verifying" | "success" | "error";

export default function VerifyEmail() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [status, setStatus] = useState<Status>("verifying");
  const [message, setMessage] = useState("");
  const ran = useRef(false);

  useEffect(() => {
    // Guard against React 18 StrictMode double-invoke — the token is single-use.
    if (ran.current) return;
    ran.current = true;

    if (!token) {
      setStatus("error");
      setMessage("This verification link is missing its token.");
      return;
    }
    verifyEmail(token)
      .then(() => setStatus("success"))
      .catch((err: unknown) => {
        const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
        setStatus("error");
        setMessage(msg ?? "This verification link is invalid or has expired.");
      });
  }, [token]);

  return (
    <main className="mx-auto max-w-sm px-4 py-16">
      <div className="rounded-xl border border-border bg-card p-6 text-center">
        <MailCheck size={28} className="text-primary mx-auto mb-4" />
        {status === "verifying" && (
          <p className="text-sm text-muted-foreground">Verifying your email…</p>
        )}
        {status === "success" && (
          <>
            <h1 className="text-lg font-bold text-card-foreground mb-2">Email verified</h1>
            <p className="text-sm text-muted-foreground mb-4">
              Thanks — your email address is confirmed.
            </p>
            <Link to="/" className="text-sm text-primary hover:underline">
              Continue to Political Arena
            </Link>
          </>
        )}
        {status === "error" && (
          <>
            <h1 className="text-lg font-bold text-card-foreground mb-2">Verification failed</h1>
            <p className="text-sm text-destructive mb-4">{message}</p>
            <p className="text-xs text-muted-foreground">
              You can request a new link from your profile after logging in.
            </p>
          </>
        )}
      </div>
    </main>
  );
}
