import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { TOKEN_KEYS, setAccessToken } from '../api/axiosClient';
import * as authApi from '../api/authApi';
import type { CurrentUser, LoginPayload, RegisterPayload, WorkspaceUser } from '../types/auth';

// ─── Context shape ───────────────────────────────────────────────────────────

interface AuthState {
  user: WorkspaceUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  /** Compatibility alias for pages imported from WDP. */
  loading: boolean;
}

interface AuthActions {
  loginWithEmail: (payload: LoginPayload) => Promise<WorkspaceUser>;
  loginWithGoogle: (idToken: string) => Promise<WorkspaceUser>;
  register: (payload: RegisterPayload) => Promise<{ requiresApproval: boolean; message: string }>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  updateUser: (changes: Partial<WorkspaceUser>) => void;
}

type AuthContextValue = AuthState & AuthActions;

export const AuthContext = createContext<AuthContextValue | null>(null);

// ─── Helper ──────────────────────────────────────────────────────────────────

function toWorkspaceUser(user: CurrentUser): WorkspaceUser {
  const roles = Array.isArray(user.roles) ? user.roles : [];
  const role = roles[0]?.trim().toUpperCase() || 'STUDENT';

  return {
    ...user,
    roles,
    _id: user.id,
    name: user.fullName,
    role,
    major: user.majorCode,
  };
}

// ─── Provider ────────────────────────────────────────────────────────────────

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<WorkspaceUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true); // true until initial check done

  // On mount: try to restore session by calling refresh-token
  useEffect(() => {
    // Legacy localStorage cleanup (runs once on bootstrap)
    localStorage.removeItem(TOKEN_KEYS.ACCESS);
    localStorage.removeItem(TOKEN_KEYS.REFRESH);

    authApi.refreshToken()
      .then(result => {
        setAccessToken(result.accessToken);
        setUser(toWorkspaceUser(result.user as CurrentUser));
      })
      .catch(() => {
        // Safe to ignore on mount (no active session cookie exists)
        setAccessToken(null);
        setUser(null);
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  // ── loginWithEmail ──────────────────────────────────────────────────────
  const loginWithEmail = useCallback(async (payload: LoginPayload): Promise<WorkspaceUser> => {
    const result = await authApi.login(payload);
    setAccessToken(result.accessToken);
    const workspaceUser = toWorkspaceUser(result.user as CurrentUser);
    setUser(workspaceUser);
    return workspaceUser;
  }, []);

  // ── loginWithGoogle ─────────────────────────────────────────────────────
  const loginWithGoogle = useCallback(async (idToken: string): Promise<WorkspaceUser> => {
    const result = await authApi.googleLogin({ idToken });
    setAccessToken(result.accessToken);
    const workspaceUser = toWorkspaceUser(result.user as CurrentUser);
    setUser(workspaceUser);
    return workspaceUser;
  }, []);

  // ── register ────────────────────────────────────────────────────────────
  const register = useCallback(async (
    payload: RegisterPayload,
  ): Promise<{ requiresApproval: boolean; message: string }> => {
    const result = await authApi.register(payload);

    // Student → auto-login (backend returns tokens)
    if (!result.requiresApproval && result.accessToken && result.user) {
      setAccessToken(result.accessToken);
      setUser(toWorkspaceUser(result.user as CurrentUser));
    }

    return { requiresApproval: result.requiresApproval, message: result.message };
  }, []);

  // ── logout ──────────────────────────────────────────────────────────────
  const logout = useCallback(async (): Promise<void> => {
    try {
      await authApi.logout();
    } finally {
      setAccessToken(null);
      setUser(null);
    }
  }, []);

  // ── refreshUser ──────────────────────────────────────────────────────────
  const refreshUser = useCallback(async (): Promise<void> => {
    const u = await authApi.getCurrentUser();
    setUser(toWorkspaceUser(u));
  }, []);

  const updateUser = useCallback((changes: Partial<WorkspaceUser>): void => {
    setUser(current => current ? { ...current, ...changes } : current);
  }, []);

  return (
    <AuthContext.Provider value={{
      user,
      isAuthenticated: !!user,
      isLoading,
      loading: isLoading,
      loginWithEmail,
      loginWithGoogle,
      register,
      logout,
      refreshUser,
      updateUser,
    }}>
      {children}
    </AuthContext.Provider>
  );
};

// ─── Internal hook (used by useAuth.ts) ─────────────────────────────────────

export function useAuthContext(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuthContext must be used inside <AuthProvider>');
  return ctx;
}
