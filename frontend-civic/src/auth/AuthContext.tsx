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
  errorStatus,
  getFreshAccessToken,
  getRefreshToken,
  getTrustedDeviceToken,
  storeTokens,
  storeTrustedDeviceToken,
} from "./tokenManager";

export type AuthUser = {
  id: string;
  email: string;
  displayName: string | null;
  avatarUrl: string | null;
  plan: "Free" | "Premium";
  emailVerified: boolean;
  mfaEnabled?: boolean;
};

type AuthTokens = { accessToken: string; refreshToken: string };
type AuthResponse = AuthTokens & { user: AuthUser; trustedDeviceToken?: string | null };

/** Result of a password login: either a full session, or a 2FA challenge to complete. */
export type LoginResult = { status: "ok" } | { status: "mfa"; mfaToken: string };

type AuthContextValue = {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<LoginResult>;
  completeMfaChallenge: (mfaToken: string, code: string, rememberDevice: boolean) => Promise<void>;
  register: (
    email: string,
    password: string,
    displayName: string,
    inviteCode: string,
    dateOfBirth: string,
    acceptedTermsVersion: string,
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
    // the token manager). Null means no token, or the refresh failed.
    const token = await getFreshAccessToken();
    if (!token) {
      // Only treat this as "logged out" when the refresh token is gone too
      // (never signed in, or the refresh was definitively rejected and the
      // token manager cleared it). If a refresh token survives, the failure
      // was transient — offline, or the backend's 503 startup gate — so keep
      // it and let a later attempt restore the session.
      if (!getRefreshToken()) {
        clearTokens();
        setUser(null);
      }
      setIsLoading(false);
      return;
    }
    try {
      // The arena client's 401 backstop covers a token that fails for a non-
      // expiry reason; if it still throws 401, the session is genuinely over.
      const res = await arenaApi.get<AuthUser>("/profile/me");
      setUser(res.data);
    } catch (err) {
      // Anything but a 401 (503 cold start, network blip) is transient: keep
      // the tokens and whatever user state we had rather than logging out.
      if (errorStatus(err) === 401) {
        clearTokens();
        setUser(null);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshUser();
  }, [refreshUser]);

  const storeAuthResponse = (data: AuthResponse) => {
    storeTokens(data);
    if (data.trustedDeviceToken) storeTrustedDeviceToken(data.trustedDeviceToken);
  };

  const login = async (email: string, password: string): Promise<LoginResult> => {
    const trustedDeviceToken = getTrustedDeviceToken() ?? undefined;
    const res = await arenaApi.post<AuthResponse & { mfaRequired?: boolean; mfaToken?: string }>(
      "/auth/login",
      { email, password, trustedDeviceToken },
    );
    if (res.data.mfaRequired) {
      return { status: "mfa", mfaToken: res.data.mfaToken! };
    }
    storeAuthResponse(res.data);
    setUser(res.data.user);
    await postLinkAnonymous(getAnonymousUserId());
    return { status: "ok" };
  };

  const completeMfaChallenge = async (mfaToken: string, code: string, rememberDevice: boolean) => {
    const anonId = getAnonymousUserId();
    const res = await arenaApi.post<AuthResponse>("/auth/mfa/challenge", {
      mfaToken,
      code,
      rememberDevice,
    });
    storeAuthResponse(res.data);
    setUser(res.data.user);
    await postLinkAnonymous(anonId);
  };

  const register = async (
    email: string,
    password: string,
    displayName: string,
    inviteCode: string,
    dateOfBirth: string,
    acceptedTermsVersion: string,
  ) => {
    const anonId = getAnonymousUserId();
    const res = await arenaApi.post<AuthResponse>("/auth/register", {
      email,
      password,
      displayName,
      inviteCode,
      dateOfBirth,
      acceptedTermsVersion,
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
        completeMfaChallenge,
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
