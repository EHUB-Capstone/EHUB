import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { TOKEN_KEYS } from '../api/axiosClient';
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

function saveTokens(access: string, refresh: string) {
  localStorage.setItem(TOKEN_KEYS.ACCESS,  access);
  localStorage.setItem(TOKEN_KEYS.REFRESH, refresh);
}

function clearTokens() {
  localStorage.removeItem(TOKEN_KEYS.ACCESS);
  localStorage.removeItem(TOKEN_KEYS.REFRESH);
}

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
  const [user,      setUser]      = useState<WorkspaceUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true); // true until initial check done

  // On mount: if we have an access token, verify it by calling /me
  useEffect(() => {
    const accessToken = localStorage.getItem(TOKEN_KEYS.ACCESS);
    if (!accessToken) {
      setIsLoading(false);
      return;
    }
    authApi.getCurrentUser()
      .then(u => setUser(toWorkspaceUser(u)))
      .catch(() => clearTokens())   // token invalid/expired — axiosClient handles redirect
      .finally(() => setIsLoading(false));
  }, []);

  // ── loginWithEmail ──────────────────────────────────────────────────────
  const loginWithEmail = useCallback(async (payload: LoginPayload): Promise<WorkspaceUser> => {
    const result = await authApi.login(payload);
    saveTokens(result.accessToken, result.refreshToken);
    const workspaceUser = toWorkspaceUser(result.user as CurrentUser);
    setUser(workspaceUser);
    return workspaceUser;
  }, []);

  // ── loginWithGoogle ─────────────────────────────────────────────────────
  const loginWithGoogle = useCallback(async (idToken: string): Promise<WorkspaceUser> => {
    const result = await authApi.googleLogin({ idToken });
    saveTokens(result.accessToken, result.refreshToken);
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
    if (!result.requiresApproval && result.accessToken && result.refreshToken && result.user) {
      saveTokens(result.accessToken, result.refreshToken);
      setUser(toWorkspaceUser(result.user as CurrentUser));
    }

    return { requiresApproval: result.requiresApproval, message: result.message };
  }, []);

  // ── logout ──────────────────────────────────────────────────────────────
  const logout = useCallback(async (): Promise<void> => {
    const refreshToken = localStorage.getItem(TOKEN_KEYS.REFRESH);
    try {
      if (refreshToken) await authApi.logout(refreshToken);
    } finally {
      clearTokens();
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
