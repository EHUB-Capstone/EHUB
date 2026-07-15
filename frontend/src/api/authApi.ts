import axiosClient from './axiosClient';
import type {
  ApiResponse,
  AuthTokens,
  CurrentUser,
  GoogleLoginPayload,
  LoginPayload,
  RegisterPayload,
  RegisterResult,
} from '../types/auth';

// ─── POST /api/auth/register ──────────────────────────────────────────────
export async function register(payload: RegisterPayload): Promise<RegisterResult> {
  const data = await axiosClient.post('/auth/register', payload);
  return data.data;
}

// ─── POST /api/auth/login ────────────────────────────────────────────────
export async function login(payload: LoginPayload): Promise<AuthTokens> {
  const data = await axiosClient.post('/auth/login', payload);
  return data.data;
}

// ─── POST /api/auth/google ────────────────────────────────────────────────
export async function googleLogin(payload: GoogleLoginPayload): Promise<AuthTokens> {
  const data = await axiosClient.post('/auth/google', payload);
  return data.data;
}

// ─── GET /api/auth/me  (requires Bearer token) ───────────────────────────
export async function getCurrentUser(): Promise<CurrentUser> {
  const data = await axiosClient.get('/auth/me');
  return data.data;
}

// ─── POST /api/auth/refresh-token ────────────────────────────────────────
export async function refreshToken(token: string): Promise<AuthTokens> {
  const data = await axiosClient.post('/auth/refresh-token', {
    refreshToken: token,
  });
  return data.data;
}

// ─── POST /api/auth/logout ────────────────────────────────────────────────
export async function logout(token: string): Promise<void> {
  await axiosClient.post('/auth/logout', { refreshToken: token });
}
