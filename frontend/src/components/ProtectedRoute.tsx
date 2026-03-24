import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { Crown } from "lucide-react";

interface ProtectedRouteProps {
  children: React.ReactNode;
  requiredPlan?: "Premium";
}

export function ProtectedRoute({ children, requiredPlan }: ProtectedRouteProps) {
  const { isAuthenticated, isPremium, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-16">
        <div className="h-32 rounded-xl border border-border bg-card animate-pulse" />
      </main>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to={`/login?redirect=${encodeURIComponent(location.pathname)}`} replace />;
  }

  if (requiredPlan === "Premium" && !isPremium) {
    return (
      <main className="mx-auto max-w-sm px-4 py-16 text-center">
        <div className="rounded-xl border border-border bg-card p-8">
          <Crown size={32} className="mx-auto mb-3 text-amber-500" />
          <h2 className="text-lg font-bold text-card-foreground mb-2">Premium Required</h2>
          <p className="text-sm text-muted-foreground">
            This feature is available to Premium members. Contact an admin to upgrade your account.
          </p>
        </div>
      </main>
    );
  }

  return <>{children}</>;
}
