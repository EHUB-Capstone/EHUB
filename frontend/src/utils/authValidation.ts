import { PROGRAM_GROUPS } from '../constants/majors.ts';
import type { LoginPayload, RegisterPayload } from '../types/auth.ts';

export const AUTH_FIELD_LIMITS = {
  fullNameMin: 2,
  fullNameMax: 150,
  emailMax: 320,
  passwordMin: 6,
  passwordMax: 100,
} as const;

export const PUBLIC_REGISTER_ROLES = ['Student', 'Lecturer', 'Mentor'] as const;

export const BACKEND_MAJOR_CODES = PROGRAM_GROUPS.flatMap((group) =>
  group.majors.map((major) => major.code),
);

const backendMajorCodes = new Set(BACKEND_MAJOR_CODES.map((code) => code.toUpperCase()));

export const LOGIN_FIELDS = ['email', 'password'] as const;
export const REGISTER_FIELDS = [
  'fullName',
  'email',
  'password',
  'confirmPassword',
  'role',
  'majorCode',
] as const;

export type LoginField = (typeof LOGIN_FIELDS)[number];
export type RegisterField = (typeof REGISTER_FIELDS)[number];
export type AuthFieldErrors<Field extends string> = Partial<Record<Field, string>>;

export interface AuthValidationError<Field extends string> {
  field: Field;
  message: string;
  code: string;
}

const isEmpty = (value: string): boolean => value.trim().length === 0;

// FluentValidation 12 EmailAddress() uses its default ASP.NET Core-compatible
// check: exactly one '@', with text on both sides.
export function isBackendCompatibleEmail(value: string): boolean {
  const atIndex = value.indexOf('@');
  return atIndex > 0
    && atIndex < value.length - 1
    && atIndex === value.lastIndexOf('@');
}

export function normalizeLoginPayload(payload: LoginPayload): LoginPayload {
  return {
    email: payload.email.trim().toLowerCase(),
    password: payload.password,
  };
}

export function normalizeRegisterPayload(payload: RegisterPayload): RegisterPayload {
  const majorCode = payload.majorCode?.trim().toUpperCase();
  return {
    fullName: payload.fullName.trim(),
    email: payload.email.trim().toLowerCase(),
    password: payload.password,
    confirmPassword: payload.confirmPassword,
    role: payload.role,
    majorCode: majorCode || undefined,
  };
}

export function validateLoginPayload(payload: LoginPayload): AuthValidationError<LoginField>[] {
  const errors: AuthValidationError<LoginField>[] = [];

  if (isEmpty(payload.email)) {
    errors.push({ field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' });
  }
  if (!isBackendCompatibleEmail(payload.email)) {
    errors.push({ field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' });
  }
  if (payload.email.length > AUTH_FIELD_LIMITS.emailMax) {
    errors.push({ field: 'email', message: 'Email must not exceed 320 characters.', code: 'MaximumLengthValidator' });
  }

  if (isEmpty(payload.password)) {
    errors.push({ field: 'password', message: 'Password is required.', code: 'NotEmptyValidator' });
  }
  if (payload.password.length < AUTH_FIELD_LIMITS.passwordMin) {
    errors.push({ field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' });
  }
  if (payload.password.length > AUTH_FIELD_LIMITS.passwordMax) {
    errors.push({ field: 'password', message: 'Password must not exceed 100 characters.', code: 'MaximumLengthValidator' });
  }

  return errors;
}

export function validateRegisterPayload(payload: RegisterPayload): AuthValidationError<RegisterField>[] {
  const errors: AuthValidationError<RegisterField>[] = [];

  if (isEmpty(payload.fullName)) {
    errors.push({ field: 'fullName', message: 'Full name is required.', code: 'NotEmptyValidator' });
    errors.push({ field: 'fullName', message: 'Full name must not consist of only whitespace.', code: 'PredicateValidator' });
  }
  if (payload.fullName.length < AUTH_FIELD_LIMITS.fullNameMin) {
    errors.push({ field: 'fullName', message: 'Full name must be at least 2 characters.', code: 'MinimumLengthValidator' });
  }
  if (payload.fullName.length > AUTH_FIELD_LIMITS.fullNameMax) {
    errors.push({ field: 'fullName', message: 'Full name must not exceed 150 characters.', code: 'MaximumLengthValidator' });
  }

  if (isEmpty(payload.email)) {
    errors.push({ field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' });
  }
  if (!isBackendCompatibleEmail(payload.email)) {
    errors.push({ field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' });
  }
  if (payload.email.length > AUTH_FIELD_LIMITS.emailMax) {
    errors.push({ field: 'email', message: 'Email must not exceed 320 characters.', code: 'MaximumLengthValidator' });
  }

  if (isEmpty(payload.password)) {
    errors.push({ field: 'password', message: 'Password is required.', code: 'NotEmptyValidator' });
  }
  if (payload.password.length < AUTH_FIELD_LIMITS.passwordMin) {
    errors.push({ field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' });
  }
  if (payload.password.length > AUTH_FIELD_LIMITS.passwordMax) {
    errors.push({ field: 'password', message: 'Password must not exceed 100 characters.', code: 'MaximumLengthValidator' });
  }

  if (isEmpty(payload.confirmPassword)) {
    errors.push({ field: 'confirmPassword', message: 'Confirm password is required.', code: 'NotEmptyValidator' });
  }
  if (payload.confirmPassword !== payload.password) {
    errors.push({
      field: 'confirmPassword',
      message: 'Confirm password must match the password.',
      code: 'AUTH_PASSWORD_CONFIRMATION_MISMATCH',
    });
  }

  if (isEmpty(payload.role)) {
    errors.push({ field: 'role', message: 'Role is required.', code: 'NotEmptyValidator' });
  }
  if (!(PUBLIC_REGISTER_ROLES as readonly string[]).includes(payload.role)) {
    errors.push({
      field: 'role',
      message: 'Role is invalid. Only Lecturer, Student, Mentor roles are allowed for public registration.',
      code: 'AUTH_INVALID_ROLE',
    });
  }

  const majorCode = payload.majorCode ?? '';
  if (payload.role === 'Student' && isEmpty(majorCode)) {
    errors.push({
      field: 'majorCode',
      message: 'Major is required for Student role.',
      code: 'AUTH_STUDENT_MAJOR_REQUIRED',
    });
  }
  if (majorCode.length > 0 && !backendMajorCodes.has(majorCode.trim().toUpperCase())) {
    errors.push({
      field: 'majorCode',
      message: 'Selected major is invalid.',
      code: 'AUTH_INVALID_MAJOR',
    });
  }

  return errors;
}

export function toFieldErrorMap<Field extends string>(
  errors: readonly AuthValidationError<Field>[],
): AuthFieldErrors<Field> {
  return errors.reduce<AuthFieldErrors<Field>>((result, error) => {
    if (!result[error.field]) result[error.field] = error.message;
    return result;
  }, {});
}

export function mapApiFieldErrors<Field extends string>(
  source: Record<string, string>,
  allowedFields: readonly Field[],
): AuthFieldErrors<Field> {
  const canonicalFields = new Map(
    allowedFields.map((field) => [field.replace(/[^a-z0-9]/gi, '').toLowerCase(), field]),
  );

  return Object.entries(source).reduce<AuthFieldErrors<Field>>((result, [rawField, message]) => {
    const leafField = rawField.split('.').at(-1) ?? rawField;
    const field = canonicalFields.get(leafField.replace(/[^a-z0-9]/gi, '').toLowerCase());
    if (field && !result[field]) result[field] = message;
    return result;
  }, {});
}
