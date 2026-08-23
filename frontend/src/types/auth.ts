// ─── Mirrors backend EHub.Contracts.Auth ────────────────────────────────────

export interface UserSummary {
  id: string;
  fullName: string;
  email: string;
  roles: string[];
  status: string;
  majorCode: string | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: UserSummary;
}

export interface RegisterResult {
  status: string;           // "PendingEmailVerification" | "Active" | "PendingApproval"
  requiresEmailVerification: boolean;
  requiresApproval: boolean;
  message: string;
  registrationId: string | null;
  maskedEmail: string | null;
  verificationExpiresAtUtc: string | null;
  resendAvailableAtUtc: string | null;
  user: UserSummary | null;
  accessToken: string | null;
  expiresAt: string | null;
}

export interface CurrentUser {
  id: string;
  fullName: string;
  email: string;
  roles: string[];
  status: string;
  majorCode: string | null;
}

/**
 * View-model used by the imported workspace UI. The API keeps its C# contract
 * (`fullName`, `roles`, and `id`); this adds the legacy presentation aliases
 * without changing the backend payload.
 */
export interface WorkspaceUser extends CurrentUser {
  _id: string;
  name: string;
  role: string;
  major: string | null;
  avatar?: string;
}

// ─── Request payloads ────────────────────────────────────────────────────────

export interface RegisterPayload {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
  role: string;
  majorCode?: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface VerifyRegistrationOtpPayload {
  registrationId: string;
  otp: string;
}

export interface ResendRegistrationOtpPayload {
  registrationId: string;
}

export interface GoogleLoginPayload {
  idToken: string;
}

export interface ForgotPasswordPayload {
  email: string;
}

export interface ResetPasswordPayload {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

// ─── Backend ApiResponse<T> wrapper ─────────────────────────────────────────

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  code: string | null;
  errors?: Array<{
    field: string;
    message: string;
    code: string;
  }> | null;
}

// ─── Error codes (mirrors backend ErrorCodes.cs) ─────────────────────────────

export const AUTH_ERROR_CODES = {
  INVALID_CREDENTIALS:      'AUTH_INVALID_CREDENTIALS',
  EMAIL_ALREADY_EXISTS:     'AUTH_EMAIL_ALREADY_EXISTS',
  ACCOUNT_PENDING_APPROVAL: 'AUTH_ACCOUNT_PENDING_APPROVAL',
  ACCOUNT_REJECTED:         'AUTH_ACCOUNT_REJECTED',
  USER_BLOCKED:             'AUTH_USER_BLOCKED',
  USER_INACTIVE:            'AUTH_USER_INACTIVE',
  ACCOUNT_NOT_REGISTERED:   'AUTH_ACCOUNT_NOT_REGISTERED',
  INVALID_GOOGLE_TOKEN:     'AUTH_INVALID_GOOGLE_TOKEN',
  GOOGLE_EMAIL_NOT_VERIFIED:'AUTH_GOOGLE_EMAIL_NOT_VERIFIED',
  REFRESH_TOKEN_INVALID:    'AUTH_REFRESH_TOKEN_INVALID',
  REFRESH_TOKEN_EXPIRED:    'AUTH_REFRESH_TOKEN_EXPIRED',
  REFRESH_TOKEN_REVOKED:    'AUTH_REFRESH_TOKEN_REVOKED',
  INVALID_ROLE:             'AUTH_INVALID_ROLE',
  INVALID_MAJOR:            'AUTH_INVALID_MAJOR',
  STUDENT_MAJOR_REQUIRED:   'AUTH_STUDENT_MAJOR_REQUIRED',
  PASSWORD_CONFIRMATION_MISMATCH: 'AUTH_PASSWORD_CONFIRMATION_MISMATCH',
  PASSWORD_RESET_TOKEN_INVALID: 'AUTH_PASSWORD_RESET_TOKEN_INVALID',
  PASSWORD_RESET_RATE_LIMITED:  'AUTH_PASSWORD_RESET_RATE_LIMITED',
  PASSWORD_RESET_FAILED:        'AUTH_PASSWORD_RESET_FAILED',
  EMAIL_VERIFICATION_REQUIRED:  'AUTH_EMAIL_VERIFICATION_REQUIRED',
  REGISTRATION_NOT_FOUND:       'AUTH_REGISTRATION_NOT_FOUND',
  VERIFICATION_CODE_INVALID:    'AUTH_VERIFICATION_CODE_INVALID',
  VERIFICATION_CODE_EXPIRED:    'AUTH_VERIFICATION_CODE_EXPIRED',
  VERIFICATION_ATTEMPTS_EXCEEDED: 'AUTH_VERIFICATION_ATTEMPTS_EXCEEDED',
  VERIFICATION_RESEND_TOO_SOON: 'AUTH_VERIFICATION_RESEND_TOO_SOON',
  VERIFICATION_RATE_LIMITED:    'AUTH_VERIFICATION_RATE_LIMITED',
  EMAIL_DELIVERY_FAILED:        'AUTH_EMAIL_DELIVERY_FAILED',
  REGISTRATION_ALREADY_COMPLETED: 'AUTH_REGISTRATION_ALREADY_COMPLETED',
} as const;
