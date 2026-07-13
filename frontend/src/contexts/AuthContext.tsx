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
  mfaEnabled?: boolean;
}

/** Result of a password login: either a full session, or a 2FA challenge to complete. */
export type LoginResult = { status: "ok" } | { status: "mfa"; mfaToken: string };

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  trustedDeviceToken?: string | null;
  user: UserProfile;
}

interface AuthState {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isPremium: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<LoginResult>;
  completeMfaChallenge: (mfaToken: string, code: string, rememberDevice: boolean) => Promise<void>;
  register: (email: string, password: string, displayName: string, inviteCode: string, dateOfBirth: string, acceptedTermsVersion: string) => Promise<void>;
  loginWithGoogle: (inviteCode?: string) => void;
  loginWithMicrosoft: (inviteCode?: string) => void;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

const BASE_URL = (import.meta.env.VITE_API_URL ?? "http://localhost:5000/api").replace(/\/api$/, "");

const TRUSTED_DEVICE_KEY = "arena-trusted-device-token";

function storeTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem("arena-access-token", accessToken);
  localStorage.setItem("arena-refresh-token", refreshToken);
}

// A "remember this computer" token deliberately survives logout — it identifies the
// device, not the session — so the second factor stays bypassed on next login.
function storeAuthResponse(data: AuthResponse) {
  storeTokens(data.accessToken, data.refreshToken);
  if (data.trustedDeviceToken) localStorage.setItem(TRUSTED_DEVICE_KEY, data.trustedDeviceToken);
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

  const login = async (email: string, password: string): Promise<LoginResult> => {
    const trustedDeviceToken = localStorage.getItem(TRUSTED_DEVICE_KEY) ?? undefined;
    const res = await api.post<AuthResponse & { mfaRequired?: boolean; mfaToken?: string }>(
      "/auth/login",
      { email, password, trustedDeviceToken },
    );
    if (res.data.mfaRequired) {
      return { status: "mfa", mfaToken: res.data.mfaToken! };
    }
    storeAuthResponse(res.data);
    setUser(res.data.user);
    return { status: "ok" };
  };

  const completeMfaChallenge = async (mfaToken: string, code: string, rememberDevice: boolean) => {
    const res = await api.post<AuthResponse>("/auth/mfa/challenge", { mfaToken, code, rememberDevice });
    storeAuthResponse(res.data);
    setUser(res.data.user);
  };

  const register = async (email: string, password: string, displayName: string, inviteCode: string, dateOfBirth: string, acceptedTermsVersion: string) => {
    const res = await api.post<{ accessToken: string; refreshToken: string; user: UserProfile }>("/auth/register", { email, password, displayName, inviteCode, dateOfBirth, acceptedTermsVersion });
    storeTokens(res.data.accessToken, res.data.refreshToken);
    setUser(res.data.user);
  };

  const loginWithGoogle = (inviteCode?: string) => {
    const params = inviteCode ? `?invite_code=${encodeURIComponent(inviteCode)}` : "";
    window.location.href = `${BASE_URL}/api/auth/google${params}`;
  };

  const loginWithMicrosoft = (inviteCode?: string) => {
    const params = inviteCode ? `?invite_code=${encodeURIComponent(inviteCode)}` : "";
    window.location.href = `${BASE_URL}/api/auth/microsoft${params}`;
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
        completeMfaChallenge,
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
