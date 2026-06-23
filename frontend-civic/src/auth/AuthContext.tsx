import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from "react";
import { arenaApi } from "./arenaAuthClient";
import { civicApi, getAnonymousUserId } from "@/api/client";
import {
  clearTokens,
  getFreshAccessToken,
  getRefreshToken,
  storeTokens,
} from "./tokenManager";

export type AuthUser = {
  id: string;
  email: string;
  displayName: string | null;
  avatarUrl: string | null;
  plan: "Free" | "Premium";
  emailVerified: boolean;
};

type AuthTokens = { accessToken: string; refreshToken: string };
type AuthResponse = AuthTokens & { user: AuthUser };

type AuthContextValue = {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (
    email: string,
    password: string,
    displayName: string,
    inviteCode: string,
  ) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
};

const AuthCtx = createContext<AuthContextValue | null>(null);

async function postLinkAnonymous(anonymousUserId: string) {
  try {
    await civicApi.post("/auth/link-anonymous", { anonymousUserId });
  } catch {
    // Non-fatal: link is a best-effort merge. The user is still authenticated
    // and can re-onboard if their anonymous data didn't make it across.
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const refreshUser = useCallback(async () => {
    // Proactively renew the token if it's expired/near-expiry (single-flight in
    // the token manager). Null means no token or the refresh failed → logged out.
    const token = await getFreshAccessToken();
    if (!token) {
      clearTokens();
      setUser(null);
      setIsLoading(false);
      return;
    }
    try {
      // The arena client's 401 backstop covers a token that fails for a non-
      // expiry reason; if it still throws, the session is genuinely over.
      const res = await arenaApi.get<AuthUser>("/profile/me");
      setUser(res.data);
    } catch {
      clearTokens();
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshUser();
  }, [refreshUser]);

  const login = async (email: string, password: string) => {
    const anonId = getAnonymousUserId();
    const res = await arenaApi.post<AuthResponse>("/auth/login", { email, password });
    storeTokens(res.data);
    setUser(res.data.user);
    await postLinkAnonymous(anonId);
  };

  const register = async (
    email: string,
    password: string,
    displayName: string,
    inviteCode: string,
  ) => {
    const anonId = getAnonymousUserId();
    const res = await arenaApi.post<AuthResponse>("/auth/register", {
      email,
      password,
      displayName,
      inviteCode,
      app: "civic",
    });
    storeTokens(res.data);
    setUser(res.data.user);
    await postLinkAnonymous(anonId);
  };

  const logout = async () => {
    const refreshToken = getRefreshToken();
    if (refreshToken) {
      try {
        await arenaApi.post("/auth/logout", { refreshToken });
      } catch {
        // ignore — clearing tokens still effectively signs out
      }
    }
    clearTokens();
    setUser(null);
  };

  return (
    <AuthCtx.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        isLoading,
        login,
        register,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthCtx.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthCtx);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}
