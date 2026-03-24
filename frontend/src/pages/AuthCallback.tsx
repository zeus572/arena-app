import { useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";

export default function AuthCallback() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { refreshUser } = useAuth();

  useEffect(() => {
    const accessToken = searchParams.get("access_token");
    const refreshToken = searchParams.get("refresh_token");

    if (accessToken && refreshToken) {
      localStorage.setItem("arena-access-token", accessToken);
      localStorage.setItem("arena-refresh-token", refreshToken);
      refreshUser().then(() => navigate("/"));
    } else {
      navigate("/login");
    }
  }, [searchParams, navigate, refreshUser]);

  return (
    <main className="mx-auto max-w-sm px-4 py-16 text-center">
      <p className="text-sm text-muted-foreground">Completing sign-in...</p>
    </main>
  );
}
