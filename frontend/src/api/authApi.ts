import axiosClient from './axiosClient';
import type {
  AuthResponse,
  CurrentUser,
  ForgotPasswordPayload,
  GoogleLoginPayload,
  LoginPayload,
  RegisterPayload,
  RegisterResult,
  ResetPasswordPayload,
} from '../types/auth';

// ─── POST /api/auth/register ──────────────────────────────────────────────
export async function register(payload: RegisterPayload): Promise<RegisterResult> {
  const data = await axiosClient.post('/auth/register', payload);
  return data.data;
}

// ─── POST /api/auth/login ────────────────────────────────────────────────
export async function login(payload: LoginPayload): Promise<AuthResponse> {
  const data = await axiosClient.post('/auth/login', payload);
  return data.data;
}

// ─── POST /api/auth/google ────────────────────────────────────────────────
export async function googleLogin(payload: GoogleLoginPayload): Promise<AuthResponse> {
  const data = await axiosClient.post('/auth/google', payload);
  return data.data;
}

// ─── GET /api/auth/me  (requires Bearer token) ───────────────────────────
export async function getCurrentUser(): Promise<CurrentUser> {
  const data = await axiosClient.get('/auth/me');
  return data.data;
}

// ─── POST /api/auth/refresh-token ────────────────────────────────────────
export async function refreshToken(): Promise<AuthResponse> {
  const data = await axiosClient.post('/auth/refresh-token');
  return data.data;
}

// ─── POST /api/auth/logout ────────────────────────────────────────────────
export async function logout(): Promise<void> {
  await axiosClient.post('/auth/logout');
}

// ─── POST /api/auth/forgot-password ──────────────────────────────────────
export async function forgotPassword(payload: ForgotPasswordPayload): Promise<void> {
  await axiosClient.post('/auth/forgot-password', payload);
}

// ─── POST /api/auth/reset-password ───────────────────────────────────────
export async function resetPassword(payload: ResetPasswordPayload): Promise<void> {
  await axiosClient.post('/auth/reset-password', payload);
}
