import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import { LogIn } from "lucide-react";

export default function Login() {
  const { login, completeMfaChallenge, loginWithGoogle, loginWithMicrosoft } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const redirect = searchParams.get("redirect") ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Second-factor step: set once /login reports MFA is required for this account.
  const [mfaToken, setMfaToken] = useState<string | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [rememberDevice, setRememberDevice] = useState(false);

  const errMsg = (err: unknown, fallback: string) =>
    (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const result = await login(email, password);
      if (result.status === "mfa") {
        setMfaToken(result.mfaToken);
      } else {
        navigate(redirect);
      }
    } catch (err: unknown) {
      setError(errMsg(err, "Login failed. Please try again."));
    } finally {
      setLoading(false);
    }
  };

  const handleMfaSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!mfaToken) return;
    setError(null);
    setLoading(true);
    try {
      await completeMfaChallenge(mfaToken, mfaCode, rememberDevice);
      navigate(redirect);
    } catch (err: unknown) {
      setError(errMsg(err, "Invalid code. Please try again."));
    } finally {
      setLoading(false);
    }
  };

  if (mfaToken) {
    return (
      <main className="mx-auto max-w-sm px-4 py-16">
        <div className="rounded-xl border border-border bg-card p-6">
          <div className="flex items-center gap-2 mb-2">
            <LogIn size={18} className="text-primary" />
            <h1 className="text-lg font-bold text-card-foreground">Two-Factor Authentication</h1>
          </div>
          <p className="text-xs text-muted-foreground mb-5">
            Enter the 6-digit code from your authenticator app, or one of your backup codes.
          </p>

          <form onSubmit={handleMfaSubmit} className="flex flex-col gap-4">
            <input
              type="text"
              inputMode="text"
              autoComplete="one-time-code"
              autoFocus
              placeholder="123456"
              value={mfaCode}
              onChange={(e) => setMfaCode(e.target.value)}
              required
              className="rounded-lg border border-border bg-background px-3 py-2 text-sm tracking-widest text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />

            <label className="flex items-center gap-2 text-xs text-muted-foreground cursor-pointer">
              <input
                type="checkbox"
                checked={rememberDevice}
                onChange={(e) => setRememberDevice(e.target.checked)}
                className="rounded border-border"
              />
              Remember this computer for 90 days
            </label>

            {error && <p className="text-xs text-destructive">{error}</p>}

            <Button type="submit" disabled={loading} className="w-full text-sm">
              {loading ? "Verifying..." : "Verify"}
            </Button>
          </form>

          <button
            type="button"
            onClick={() => { setMfaToken(null); setMfaCode(""); setError(null); }}
            className="text-xs text-muted-foreground hover:underline text-center w-full mt-4"
          >
            Back to login
          </button>
        </div>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-sm px-4 py-16">
      <div className="rounded-xl border border-border bg-card p-6">
        <div className="flex items-center gap-2 mb-6">
          <LogIn size={18} className="text-primary" />
          <h1 className="text-lg font-bold text-card-foreground">Log In</h1>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <input
            type="email"
            placeholder="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          />

          {error && (
            <p className="text-xs text-destructive">{error}</p>
          )}

          <Button type="submit" disabled={loading} className="w-full text-sm">
            {loading ? "Logging in..." : "Log In"}
          </Button>
        </form>

        <p className="text-xs text-muted-foreground text-center mt-3">
          <Link to="/forgot-password" className="text-primary hover:underline">
            Forgot your password?
          </Link>
        </p>

        <div className="relative my-5">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-border" />
          </div>
          <div className="relative flex justify-center">
            <span className="bg-card px-2 text-xs text-muted-foreground">or continue with</span>
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <Button variant="outline" size="sm" className="w-full text-xs gap-2" onClick={() => loginWithGoogle()}>
            Google
          </Button>
          <Button variant="outline" size="sm" className="w-full text-xs gap-2" onClick={() => loginWithMicrosoft()}>
            Microsoft
          </Button>
        </div>

        <p className="text-xs text-muted-foreground text-center mt-5">
          Don't have an account?{" "}
          <Link to={`/register?redirect=${encodeURIComponent(redirect)}`} className="text-primary hover:underline">
            Sign up
          </Link>
        </p>
      </div>
    </main>
  );
}
