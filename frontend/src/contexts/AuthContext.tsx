import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from "react";
import api from "@/api/client";

export interface UserProfile {
  id: string;
  email: string;
  displayName: string | null;
  avatarUrl: string | null;
  politicalLeaning: string | null;
  plan: "Free" | "Premium";
  emailVerified: boolean;
  authProvider: string | null;
}

interface AuthState {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isPremium: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string, inviteCode: string) => Promise<void>;
  loginWithGoogle: () => void;
  loginWithMicrosoft: () => void;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

const BASE_URL = (import.meta.env.VITE_API_URL ?? "http://localhost:5000/api").replace(/\/api$/, "");

function storeTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem("arena-access-token", accessToken);
  localStorage.setItem("arena-refresh-token", refreshToken);
}

function clearTokens() {
  localStorage.removeItem("arena-access-token");
  localStorage.removeItem("arena-refresh-token");
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const refreshUser = useCallback(async () => {
    const token = localStorage.getItem("arena-access-token");
    if (!token) {
      setUser(null);
      setIsLoading(false);
      return;
    }
    try {
      const res = await api.get<UserProfile>("/profile/me");
      setUser(res.data);
    } catch {
      // Try refresh
      const refreshToken = localStorage.getItem("arena-refresh-token");
      if (refreshToken) {
        try {
          const res = await api.post<{ accessToken: string; refreshToken: string }>("/auth/refresh", { refreshToken });
          storeTokens(res.data.accessToken, res.data.refreshToken);
          const profileRes = await api.get<UserProfile>("/profile/me");
          setUser(profileRes.data);
        } catch {
          clearTokens();
          setUser(null);
        }
      } else {
        clearTokens();
        setUser(null);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshUser();
  }, [refreshUser]);

  const login = async (email: string, password: string) => {
    const res = await api.post<{ accessToken: string; refreshToken: string; user: UserProfile }>("/auth/login", { email, password });
    storeTokens(res.data.accessToken, res.data.refreshToken);
    setUser(res.data.user);
  };

  const register = async (email: string, password: string, displayName: string, inviteCode: string) => {
    const res = await api.post<{ accessToken: string; refreshToken: string; user: UserProfile }>("/auth/register", { email, password, displayName, inviteCode });
    storeTokens(res.data.accessToken, res.data.refreshToken);
    setUser(res.data.user);
  };

  const loginWithGoogle = () => {
    window.location.href = `${BASE_URL}/api/auth/google`;
  };

  const loginWithMicrosoft = () => {
    window.location.href = `${BASE_URL}/api/auth/microsoft`;
  };

  const logout = async () => {
    const refreshToken = localStorage.getItem("arena-refresh-token");
    if (refreshToken) {
      try {
        await api.post("/auth/logout", { refreshToken });
      } catch { /* ignore */ }
    }
    clearTokens();
    setUser(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        isPremium: user?.plan === "Premium",
        isLoading,
        login,
        register,
        loginWithGoogle,
        loginWithMicrosoft,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
