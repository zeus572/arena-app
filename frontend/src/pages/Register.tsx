import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import { UserPlus } from "lucide-react";

export default function Register() {
  const { register, loginWithGoogle, loginWithMicrosoft } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const redirect = searchParams.get("redirect") ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [inviteCode, setInviteCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    if (!inviteCode.trim()) {
      setError("Invite code is required.");
      return;
    }
    setLoading(true);
    try {
      await register(email, password, displayName, inviteCode.trim());
      navigate(redirect);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? "Registration failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="mx-auto max-w-sm px-4 py-16">
      <div className="rounded-xl border border-border bg-card p-6">
        <div className="flex items-center gap-2 mb-6">
          <UserPlus size={18} className="text-primary" />
          <h1 className="text-lg font-bold text-card-foreground">Create Account</h1>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <input
            type="text"
            placeholder="Display name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
          <input
            type="text"
            placeholder="Invite code"
            value={inviteCode}
            onChange={(e) => setInviteCode(e.target.value)}
            required
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 font-mono tracking-wider uppercase"
          />
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
            placeholder="Password (min 8 characters)"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          />

          {error && (
            <p className="text-xs text-destructive">{error}</p>
          )}

          <Button type="submit" disabled={loading} className="w-full text-sm">
            {loading ? "Creating account..." : "Create Account"}
          </Button>
        </form>

        <div className="relative my-5">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-border" />
          </div>
          <div className="relative flex justify-center">
            <span className="bg-card px-2 text-xs text-muted-foreground">or continue with</span>
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <Button variant="outline" size="sm" className="w-full text-xs gap-2" onClick={loginWithGoogle}>
            Google
          </Button>
          <Button variant="outline" size="sm" className="w-full text-xs gap-2" onClick={loginWithMicrosoft}>
            Microsoft
          </Button>
        </div>

        <p className="text-xs text-muted-foreground text-center mt-5">
          Already have an account?{" "}
          <Link to={`/login?redirect=${encodeURIComponent(redirect)}`} className="text-primary hover:underline">
            Log in
          </Link>
        </p>
      </div>
    </main>
  );
}
