import { useState } from "react";
import { Link } from "react-router-dom";
import { requestPasswordReset } from "@/api/client";
import { Button } from "@/components/ui/button";
import { KeyRound } from "lucide-react";

export default function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      // Always succeeds from the user's POV — the API never reveals whether the
      // address is registered.
      await requestPasswordReset(email);
    } catch {
      // Swallow: still show the neutral confirmation.
    } finally {
      setLoading(false);
      setSubmitted(true);
    }
  };

  return (
    <main className="mx-auto max-w-sm px-4 py-16">
      <div className="rounded-xl border border-border bg-card p-6">
        <div className="flex items-center gap-2 mb-6">
          <KeyRound size={18} className="text-primary" />
          <h1 className="text-lg font-bold text-card-foreground">Reset your password</h1>
        </div>

        {submitted ? (
          <div className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              If an account exists for <span className="text-foreground">{email}</span>, we've
              sent a link to reset your password. Check your inbox (and spam folder).
            </p>
            <Link to="/login" className="text-sm text-primary hover:underline">
              Back to log in
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              Enter the email on your account and we'll send you a reset link.
            </p>
            <input
              type="email"
              placeholder="Email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
            <Button type="submit" disabled={loading} className="w-full text-sm">
              {loading ? "Sending..." : "Send reset link"}
            </Button>
            <Link to="/login" className="text-xs text-muted-foreground text-center hover:underline">
              Back to log in
            </Link>
          </form>
        )}
      </div>
    </main>
  );
}
